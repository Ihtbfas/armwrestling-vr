using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArmWrestlingVR.EditorTools
{
    // Local-only evidence. Drive the existing rig in true Editor Play at fixed 30 Hz.
    [InitializeOnLoad]
    public static class AnimationComparison
    {
        private const string ActiveKey = "ArmWrestling.P1Comparison";
        private const int FrameRate = 30;
        private const int SweepFrames = FrameRate * 6;
        private static readonly CultureInfo Format = CultureInfo.InvariantCulture;
        private static string output;
        private static Animator animator;
        private static OpponentAvatar avatar;
        private static VirtualArmRig rig;
        private static Camera front;
        private static Camera side;
        private static RenderTexture target;
        private static Texture2D pixels;
        private static StreamWriter data;
        private static Transform elbowPad;
        private static Transform seat;
        private static SkinnedMeshRenderer skin;
        private static Mesh baked;
        private static int[] armVertices;
        private static readonly List<Vector3> Vertices = new List<Vector3>();
        private static bool contactSweep;
        private static bool gripSweep;
        private static bool motionSweep;
        private static bool motionBaseline;
        private static bool handContactSweep;
        private static bool pairedGripSweep;
        private static HandContactProbe handContactProbe;
        private static CollisionCapture collisionCapture;
        private static Camera playerCamera;
        private static Camera playerDetail;
        private static Quaternion headInitial, chestInitial;
        private static int[] fingerVertices;
        private static int[] palmVertices;
        private static Camera handFront;
        private static Camera handSide;
        private static SphereCollider playerSphere;
        private static int mappedFingerBones;
        private static int route;
        private static int frame;
        private static int errors;

        static AnimationComparison()
        {
            EditorApplication.update += Step;
            Application.logMessageReceived += (_, __, type) =>
            {
                if (SessionState.GetBool(ActiveKey, false) &&
                    (type == LogType.Exception || type == LogType.Error || type == LogType.Assert))
                    errors++;
            };
        }

        public static void Run()
        {
            try
            {
                bool paired = HasArgument("-p2a") || HasArgument("-p2b") || HasArgument("-p2c");
                if (!paired && Resources.Load<GameObject>("Characters/Ch03") == null)
                    throw new InvalidOperationException("Restore the local Ch03 asset first.");
                EditorSceneManager.OpenScene(SceneGenerator.MainScenePath, OpenSceneMode.Single);
                var bootstrap = UnityEngine.Object.FindObjectOfType<RuntimeBootstrap>();
                bootstrap.useOriginalArt = !paired;
                bootstrap.usePairedGrip = paired;
                bootstrap.pairedCollisions = PairedCollisionMode.Off;
                if (HasArgument("-p2b") || HasArgument("-p2c"))
                    bootstrap.pairedCollisions = HasArgument("-p2c") ? (HasArgument("-p2clegacy") ? PairedCollisionMode.LegacyContactGrip : PairedCollisionMode.ContactGrip) : HasArgument("-p2bcorrect") ? PairedCollisionMode.Correct : PairedCollisionMode.Diagnose;
                bootstrap.useCharacterMotion = HasArgument("-p1e") || (HasArgument("-p1d") && !HasArgument("-p1dbaseline"));
                bootstrap.useHandContact = HasArgument("-p1e") && !HasArgument("-p1ebaseline");
                SessionState.SetBool(ActiveKey, true);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception error) { Finish(error); }
        }

        private static void Step()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
            try
            {
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                    throw new InvalidOperationException("Capture needs a graphics device. Omit -nographics.");
                if (errors > 0) throw new InvalidOperationException("Unity reported runtime errors.");
                if (animator == null) Setup();
                if (animator == null) return;

                string label = collisionCapture != null ? (HasArgument("-p2c") ? (HasArgument("-p2clegacy") ? "legacy" : HasArgument("-p2cpose") ? "authored-closed" : "contact") : HasArgument("-p2bcorrect") ? "corrected" : "baseline")
                    : pairedGripSweep ? "p2a" : handContactSweep ? (HasArgument("-p1ebaseline") ? "p1d" : "p1e")
                    : motionSweep ? (motionBaseline ? "baseline" : "motion")
                    : gripSweep ? (route == 0 ? "baseline" : "grip")
                    : contactSweep || route == 1 ? "B" : "A";
                if (frame == 0)
                {
                    avatar.BlendBodyPoses = contactSweep || route == 1;
                    if (!motionSweep) avatar.useGripPose = gripSweep && route == 1;
                    Directory.CreateDirectory(Path.Combine(output, label));
                }

                // Three poses at matching timestamps: frame 0 = -30, 45 = 0, 90 = +30.
                float range = contactSweep && HasArgument("-p1bfullrange") ? 42f : 30f;
                if (pairedGripSweep && HasArgument("-p2aneutral")) range = 0f;
                float angle = -range * Mathf.Cos(2f * Mathf.PI * frame / SweepFrames);
                rig.ApplyAngle(angle);
                if (motionSweep && avatar.UsesAuthoredMotion)
                    animator.SetFloat("Effort", Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(15, 45, frame)));
                int countBefore = avatar.IkPassCount;
                // Keep automatic Animator evaluations from advancing the clips between captures.
                if (motionSweep) animator.speed = 1f;
                animator.Update(1f / FrameRate);
                if (motionSweep) animator.speed = 0f;
                int calls = avatar.IkPassCount - countBefore;
                avatar.ApplyPairedHandBones();
                Record(label, angle, calls);
                if (gripSweep || handContactSweep) PositionHandCameras();
                if (frame == 0 || frame == 45 || frame == 90)
                {
                    Capture(front, Path.Combine(output, label, "front-" + frame.ToString("D3") + ".png"));
                    Capture(side, Path.Combine(output, label, "side-" + frame.ToString("D3") + ".png"));
                    if (gripSweep || handContactSweep)
                    {
                        Capture(handFront, Path.Combine(output, label, "hand-front-" + frame.ToString("D3") + ".png"));
                        Capture(handSide, Path.Combine(output, label, "hand-side-" + frame.ToString("D3") + ".png"));
                    }
                    if (motionSweep)
                        Capture(playerCamera, Path.Combine(output, label, "player-" + frame.ToString("D3") + ".png"));
                    if (handContactSweep)
                        Capture(playerDetail, Path.Combine(output, label, "eye-" + frame.ToString("D3") + ".png"));
                    if (collisionCapture != null)
                    {
                        Capture(handSide, Path.Combine(output, label, "diagnostic-hand-" + frame.ToString("D3") + ".png"),
                            AvatarCollisionExperiment.Region.Hands);
                        Capture(front, Path.Combine(output, label, "diagnostic-body-" + frame.ToString("D3") + ".png"),
                            AvatarCollisionExperiment.Region.Torso);
                    }
                }
                if (!HasArgument("-p1stills"))
                {
                    if (collisionCapture != null)
                    {
                        Capture(front, Path.Combine(output, label, "wide-front-" + frame.ToString("D3") + ".jpg"));
                        Capture(side, Path.Combine(output, label, "wide-side-" + frame.ToString("D3") + ".jpg"));
                    }
                    Capture(gripSweep || handContactSweep ? handFront : front, Path.Combine(output, label, "f-" + frame.ToString("D3") + ".jpg"));
                    Capture(gripSweep || handContactSweep ? handSide : side, Path.Combine(output, label, "s-" + frame.ToString("D3") + ".jpg"));
                    if (motionSweep)
                        Capture(playerCamera, Path.Combine(output, label, "p-" + frame.ToString("D3") + ".jpg"));
                    if (handContactSweep)
                        Capture(playerDetail, Path.Combine(output, label, "e-" + frame.ToString("D3") + ".jpg"));
                }
                if (calls == 0)
                    throw new InvalidOperationException("The Animator IK pass did not execute.");

                if (++frame > SweepFrames || (pairedGripSweep && HasArgument("-p2aneutral")))
                {
                    Debug.Log("[P1] Captured " + label + " | callbacks=" + avatar.IkPassCount);
                    if (motionSweep || (contactSweep && !gripSweep) || ++route == 2) Finish(null);
                    frame = 0;
                }
            }
            catch (Exception error) { Finish(error); }
        }

        private static void Setup()
        {
            var app = UnityEngine.Object.FindObjectOfType<ArmWrestlingApp>();
            if (app == null || app.Pipeline == null) return;
            avatar = UnityEngine.Object.FindObjectOfType<OpponentAvatar>();
            if (avatar == null) throw new InvalidOperationException("The humanoid opponent was not instantiated.");
            animator = avatar.GetComponent<Animator>();
            if (avatar.CollisionExperiment != null) {
                avatar.CollisionExperiment.previewClosedPose = HasArgument("-p2cpose");
                avatar.CollisionExperiment.reverseContactOrder = HasArgument("-p2creverse");
            }
            if (!animator.isHuman || !animator.avatar.isValid)
                throw new InvalidOperationException("Ch03 needs a valid Humanoid Avatar.");

            rig = app.armRig;
            app.enabled = false; // Only this Play copy. Keep the regular match driven by its data source.
            app.rig.enabled = false;
            app.hud.canvas.enabled = false;
            app.GetComponent<VrPointer>().enabled = false;
            app.GetComponent<LineRenderer>().enabled = false;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);
            RenderSettings.fog = false;
            foreach (Light light in app.deviceRoot.GetComponentsInChildren<Light>())
                light.intensity *= 0.43f;
            var fill = new GameObject("Comparison fill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.4f;
            fill.transform.rotation = app.deviceRoot.rotation * Quaternion.Euler(28f, 155f, 0f);

            front = View(app.deviceRoot, new Vector3(-0.28f, 0.82f, -1.42f), new Vector3(0f, 0.18f, 0.34f));
            side = View(app.deviceRoot, new Vector3(-1.98f, 0.43f, 0.35f), new Vector3(-0.07f, -0.15f, 0.33f));
            gripSweep = HasArgument("-p1c");
            pairedGripSweep = HasArgument("-p2a") || HasArgument("-p2b") || HasArgument("-p2c");
            handContactSweep = pairedGripSweep || HasArgument("-p1e");
            motionSweep = handContactSweep || HasArgument("-p1d");
            motionBaseline = HasArgument("-p1dbaseline");
            contactSweep = gripSweep || motionSweep || HasArgument("-p1b");
            output = Path.GetFullPath(Path.Combine(".p0-local", HasArgument("-p2c") ? "p2c-contact" : HasArgument("-p2b") ? "p2b-collision" : pairedGripSweep ? "p2a-avatar" : handContactSweep ? "p1e-hand-contact" : motionSweep ? "p1d-motion"
                : gripSweep ? "p1c-grip" : contactSweep ? "p1b-contact" : "p1-animation",
                DateTime.Now.ToString("yyyyMMdd-HHmmss")));
            Directory.CreateDirectory(output);
            if (HasArgument("-p2b") || HasArgument("-p2c"))
                collisionCapture = new CollisionCapture(avatar.CollisionExperiment, output, rig.transform);
            target = new RenderTexture(768, 576, 24);
            pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            data = new StreamWriter(Path.Combine(output, "measurements.csv"));
            elbowPad = app.deviceRoot.Find("Opponent Elbow Pad");
            seat = app.deviceRoot.Find("Opponent Seat");
            const string header = "route,frame,seconds,angle,ik_calls,grip_to_wrist_mm,target_to_wrist_mm,grip_to_palm_proxy_mm,elbow_dx_mm,elbow_dy_mm,elbow_dz_mm,elbow_flex_deg,wrist_bend_deg,hips_x,hips_y,hips_z,shoulder_x,shoulder_y,shoulder_z,head_x,head_y,head_z,elbow_x,elbow_y,elbow_z,wrist_x,wrist_y,wrist_z";
            string contactColumns = ",skin_gap_mm,skin_overhang_mm,patch_vertices,skin_x,skin_y,skin_z,grip_x,grip_y,grip_z,pad_x,pad_top_y,pad_z,pad_width,pad_depth,seat_x,seat_top_y,seat_z,model_scale,right_upper_mm,right_fore_mm,left_upper_mm,left_fore_mm,left_shoulder_x,left_shoulder_y,left_shoulder_z,left_elbow_x,left_elbow_y,left_elbow_z,left_wrist_x,left_wrist_y,left_wrist_z";
            if (contactSweep) PrepareSurfaceProbe();
            if (gripSweep || handContactSweep)
            {
                handFront = View(app.deviceRoot, Vector3.back, Vector3.zero);
                handSide = View(app.deviceRoot, Vector3.left, Vector3.zero);
                handFront.nearClipPlane = handSide.nearClipPlane = 0.02f;
                if (gripSweep) PrepareGripProbe();
            }
            const string gripColumns = ",mapped_finger_bones,finger_vertices,sphere_inside,sphere_max_depth_mm,sphere_mean_intrusion_mm,sphere_min_clearance_mm,palm_vertices,palm_sphere_inside,palm_max_depth_mm,index_pip_deg,middle_pip_deg,thumb_ip_deg";
            const string motionColumns = ",skinned_player,player_palm_error_mm,palm_separation_mm,stable_elbow,body_shift_mm,head_local_delta_deg,chest_local_delta_deg,effort,authored_clip,normalized_state_time";
            if (motionSweep)
            {
                animator.speed = 0f;
                animator.Play(0, 0, 0f);
                playerCamera = Camera.main;
                if (!motionBaseline && (rig.playerVisual == null || !avatar.UsesAuthoredMotion))
                    throw new InvalidOperationException("P1-D requires the local player crop and authored motion controller.");
                headInitial = Bone(HumanBodyBones.Head).localRotation;
                chestInitial = Bone(HumanBodyBones.Chest).localRotation;
            }
            if (handContactSweep)
            {
                bool expectedContact = !HasArgument("-p1ebaseline");
                if (pairedGripSweep ? (!rig.playerVisual.UsesPairedGrip || !avatar.UsesPairedGrip)
                    : rig.playerVisual.UsesHandContact != expectedContact)
                    throw new InvalidOperationException("The requested hand contact mode did not start.");
                handContactProbe = new HandContactProbe(animator, skin, rig.playerVisual.Skin);
                playerDetail = View(app.deviceRoot, Vector3.back, Vector3.zero);
                playerDetail.nearClipPlane = playerCamera.nearClipPlane;
                playerDetail.fieldOfView = 26f;
            }
            data.WriteLine(header + (contactSweep ? contactColumns : "") + (gripSweep ? gripColumns : "")
                + (motionSweep ? motionColumns : "") + (handContactSweep ? HandContactProbe.Header : "")
                + (collisionCapture != null ? CollisionCapture.Header : ""));
            Debug.Log("[P1] " + Application.unityVersion + " | " + SystemInfo.graphicsDeviceType + " | " + output);
        }

        private static Camera View(Transform root, Vector3 from, Vector3 look)
        {
            var camera = new GameObject("P1 comparison camera").AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
            camera.transform.position = root.TransformPoint(from);
            camera.transform.LookAt(root.TransformPoint(look), root.up);
            camera.fieldOfView = 43f;
            return camera;
        }

        private static void Record(string label, float angle, int calls)
        {
            var elbow = Bone(HumanBodyBones.RightLowerArm);
            var wrist = Bone(HumanBodyBones.RightHand);
            var finger = Bone(HumanBodyBones.RightMiddleProximal);
            var shoulder = Bone(HumanBodyBones.RightUpperArm);
            var hips = Bone(HumanBodyBones.Hips);
            var head = Bone(HumanBodyBones.Head);
            var palm = Vector3.Lerp(wrist.position, finger.position, 0.55f);
            var elbowLocal = rig.transform.InverseTransformPoint(elbow.position);
            var pad = elbowPad.localPosition + 0.5f * elbowPad.localScale.y * Vector3.up;
            var offset = 1000f * (elbowLocal - pad);
            float flex = Vector3.Angle(elbow.position - shoulder.position, wrist.position - elbow.position);
            float wristBend = Vector3.Angle(wrist.position - elbow.position, finger.position - wrist.position);
            float[] values =
            {
                frame, (float)frame / FrameRate, angle, calls,
                1000 * Vector3.Distance(rig.GripWorldPosition, wrist.position),
                1000 * Vector3.Distance(avatar.DesiredRightWrist, wrist.position),
                1000 * Vector3.Distance(rig.GripWorldPosition, palm),
                offset.x, offset.y, offset.z, flex, wristBend
            };
            data.WriteLine(label + "," + string.Join(",", values.Select(v => v.ToString("F4", Format)))
                + Point(hips) + Point(shoulder) + Point(head) + Point(elbow) + Point(wrist)
                + (contactSweep ? ContactColumns(elbowLocal, pad, shoulder, elbow, wrist) : "")
                + (gripSweep ? GripColumns() : "") + (motionSweep ? MotionColumns(palm) : "")
                + (handContactSweep ? handContactProbe.Record(baked, Vertices) : "")
                + (collisionCapture != null ? collisionCapture.Record(frame, angle) : ""));
            if (frame == 0 || frame == 45 || frame == 90) data.Flush();
        }

        private static Transform Bone(HumanBodyBones bone) => animator.GetBoneTransform(bone);

        private static string Point(Transform bone)
        {
            return Point(rig.transform.InverseTransformPoint(bone.position));
        }

        private static string Point(Vector3 p)
        {
            return string.Format(Format, ",{0:F4},{1:F4},{2:F4}", p.x, p.y, p.z);
        }

        private static string MotionColumns(Vector3 opponentPalm)
        {
            var player = rig.playerVisual;
            Vector3 palm = player != null ? player.PalmPosition : rig.playerArm.hand.position;
            Vector3 targetPalm = player != null ? player.DesiredPalm
                : rig.GripWorldPosition - rig.transform.right * rig.handSeparation;
            string clip = "IdlePose";
            float effort = 0f;
            if (avatar.UsesAuthoredMotion)
            {
                var clips = animator.GetCurrentAnimatorClipInfo(0);
                clip = string.Join("+", clips.Where(c => c.weight > 0.001f).Select(c => c.clip.name));
                effort = animator.GetFloat("Effort");
            }
            return string.Format(Format, ",{0},{1:F4},{2:F4},{3},{4:F4},{5:F4},{6:F4},{7:F4},{8},{9:F5}",
                player != null ? 1 : 0, 1000f * Vector3.Distance(targetPalm, palm),
                1000f * Vector3.Distance(opponentPalm, palm), avatar.UsedStableElbow ? 1 : 0,
                avatar.BodyCorrection * 1000f, Quaternion.Angle(headInitial, Bone(HumanBodyBones.Head).localRotation),
                Quaternion.Angle(chestInitial, Bone(HumanBodyBones.Chest).localRotation), effort, clip, animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
        }

        private static void PrepareSurfaceProbe()
        {
            Transform upper = Bone(HumanBodyBones.RightUpperArm);
            Transform lower = Bone(HumanBodyBones.RightLowerArm);
            foreach (var candidate in avatar.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                int a = Array.IndexOf(candidate.bones, upper);
                int b = Array.IndexOf(candidate.bones, lower);
                if (a < 0 || b < 0) continue;
                var weights = candidate.sharedMesh.boneWeights;
                armVertices = Enumerable.Range(0, weights.Length)
                    .Where(i => ArmWeight(weights[i], a, b) >= 0.5f).ToArray();
                if (armVertices.Length == 0) continue;
                skin = candidate;
                baked = new Mesh();
                return;
            }
            throw new InvalidOperationException("Could not identify Ch03's weighted right arm surface.");
        }

        private static float ArmWeight(BoneWeight w, int a, int b)
        {
            return (w.boneIndex0 == a || w.boneIndex0 == b ? w.weight0 : 0)
                 + (w.boneIndex1 == a || w.boneIndex1 == b ? w.weight1 : 0)
                 + (w.boneIndex2 == a || w.boneIndex2 == b ? w.weight2 : 0)
                 + (w.boneIndex3 == a || w.boneIndex3 == b ? w.weight3 : 0);
        }

        private static void PrepareGripProbe()
        {
            var digitBones = new HashSet<int>();
            Transform[] bones = skin.bones;
            for (int i = (int)HumanBodyBones.RightThumbProximal; i <= (int)HumanBodyBones.RightLittleDistal; i++)
            {
                var joint = Bone((HumanBodyBones)i);
                if (joint == null) continue;
                mappedFingerBones++;
                int index = Array.IndexOf(bones, joint);
                if (index >= 0) digitBones.Add(index);
            }
            var weights = skin.sharedMesh.boneWeights;
            fingerVertices = WeightedVertices(weights, digitBones);
            int palm = Array.IndexOf(bones, Bone(HumanBodyBones.RightHand));
            palmVertices = WeightedVertices(weights, new HashSet<int> { palm });
            if (mappedFingerBones != 15 || fingerVertices.Length == 0 || palmVertices.Length == 0)
                throw new InvalidOperationException("Ch03 does not have all mapped, skinned right hand bones.");
            playerSphere = rig.playerArm.hand.GetComponent<SphereCollider>();
        }

        private static int[] WeightedVertices(BoneWeight[] weights, HashSet<int> bones)
        {
            return Enumerable.Range(0, weights.Length).Where(i =>
            {
                var w = weights[i];
                return (bones.Contains(w.boneIndex0) ? w.weight0 : 0f)
                    + (bones.Contains(w.boneIndex1) ? w.weight1 : 0f)
                    + (bones.Contains(w.boneIndex2) ? w.weight2 : 0f)
                    + (bones.Contains(w.boneIndex3) ? w.weight3 : 0f) >= 0.5f;
            }).ToArray();
        }

        private static string GripColumns()
        {
            // Reuse the posed mesh from ContactColumns. The player hand primitive is
            // uniformly scaled, so a signed distance to its visual sphere is sufficient.
            Transform proxy = playerSphere.transform;
            Vector3 centre = proxy.TransformPoint(playerSphere.center);
            float radius = playerSphere.radius * proxy.lossyScale.x;
            var fingers = SphereOverlap(fingerVertices, centre, radius);
            var palm = SphereOverlap(palmVertices, centre, radius);
            return string.Format(Format, ",{0},{1},{2},{3:F4},{4:F4},{5:F4},{6},{7},{8:F4},{9:F4},{10:F4},{11:F4}",
                mappedFingerBones, fingerVertices.Length, fingers.inside, fingers.peak * 1000f,
                fingers.total * 1000f / fingerVertices.Length, fingers.nearest * 1000f,
                palmVertices.Length, palm.inside, palm.peak * 1000f,
                FingerBend(HumanBodyBones.RightIndexProximal), FingerBend(HumanBodyBones.RightMiddleProximal),
                FingerBend(HumanBodyBones.RightThumbProximal));
        }

        private static (int inside, float peak, float total, float nearest) SphereOverlap(int[] vertexIds, Vector3 centre, float radius)
        {
            float peak = 0f;
            float total = 0f;
            float nearest = float.PositiveInfinity;
            int inside = 0;
            foreach (int i in vertexIds)
            {
                float depth = radius - Vector3.Distance(centre, skin.transform.TransformPoint(Vertices[i]));
                nearest = Mathf.Min(nearest, -depth);
                if (depth <= 0f) continue;
                inside++;
                peak = Mathf.Max(peak, depth);
                total += depth;
            }
            return (inside, peak, total, nearest);
        }

        private static float FingerBend(HumanBodyBones proximal)
        {
            int index = (int)proximal;
            Vector3 a = Bone(proximal).position;
            Vector3 b = Bone((HumanBodyBones)(index + 1)).position;
            Vector3 c = Bone((HumanBodyBones)(index + 2)).position;
            return Vector3.Angle(b - a, c - b);
        }

        private static void PositionHandCameras()
        {
            Vector3 grip = rig.GripWorldPosition;
            Transform frame = rig.transform;
            handFront.transform.position = grip + frame.TransformVector(new Vector3(-0.11f, 0.11f, -0.27f));
            handFront.transform.LookAt(grip, frame.up);
            handSide.transform.position = grip + frame.TransformVector(new Vector3(-0.30f, 0.10f, 0.07f));
            handSide.transform.LookAt(grip, frame.up);
            if (handContactSweep)
            {
                playerDetail.transform.position = playerCamera.transform.position;
                playerDetail.transform.LookAt(grip, playerCamera.transform.up);
            }
        }

        private static string ContactColumns(Vector3 elbowLocal, Vector3 pad, Transform shoulder,
            Transform elbow, Transform wrist)
        {
            // Measure the underside of the skinned elbow neighbourhood. This is a
            // sampled rendered surface, not a pressure/contact or triangle test.
            skin.BakeMesh(baked);
            baked.GetVertices(Vertices);
            Matrix4x4 toDevice = rig.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix;
            Vector3 underside = new Vector3(0f, float.PositiveInfinity, 0f);
            int samples = 0;
            foreach (int i in armVertices)
            {
                Vector3 vertex = toDevice.MultiplyPoint3x4(Vertices[i]);
                if ((vertex - elbowLocal).sqrMagnitude > 0.055f * 0.055f) continue;
                samples++;
                if (vertex.y < underside.y) underside = vertex;
            }
            if (samples == 0) throw new InvalidOperationException("No sampled surface near the right elbow.");
            Vector3 size = elbowPad.localScale;
            float outsideX = Mathf.Max(0, Mathf.Abs(underside.x - pad.x) - size.x * 0.5f);
            float outsideZ = Mathf.Max(0, Mathf.Abs(underside.z - pad.z) - size.z * 0.5f);
            float overhang = new Vector2(outsideX, outsideZ).magnitude;
            Transform ls = Bone(HumanBodyBones.LeftUpperArm);
            Transform le = Bone(HumanBodyBones.LeftLowerArm);
            Transform lw = Bone(HumanBodyBones.LeftHand);
            return string.Format(Format, ",{0:F4},{1:F4},{2}", 1000f * (underside.y - pad.y), 1000f * overhang, samples)
                + Point(underside) + Point(rig.transform.InverseTransformPoint(rig.GripWorldPosition))
                + Point(pad) + string.Format(Format, ",{0:F4},{1:F4}", size.x, size.z)
                + Point(seat.localPosition + 0.5f * seat.localScale.y * Vector3.up)
                + string.Format(Format, ",{0:F4},{1:F4},{2:F4},{3:F4},{4:F4}", avatar.transform.localScale.x,
                    1000f * Vector3.Distance(shoulder.position, elbow.position), 1000f * Vector3.Distance(elbow.position, wrist.position),
                    1000f * Vector3.Distance(ls.position, le.position), 1000f * Vector3.Distance(le.position, lw.position))
                + Point(ls) + Point(le) + Point(lw);
        }

        private static void Capture(Camera camera, string path, AvatarCollisionExperiment.Region? overlay = null)
        {
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            try
            {
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                if (overlay.HasValue) collisionCapture.DrawOverlay(camera, pixels, overlay.Value);
                pixels.Apply();
                File.WriteAllBytes(path, path.EndsWith(".png") ? pixels.EncodeToPNG() : pixels.EncodeToJPG(88));
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
            }
        }

        private static bool HasArgument(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        private static void Finish(Exception error)
        {
            SessionState.SetBool(ActiveKey, false);
            data?.Dispose();
            handContactProbe?.Dispose();
            collisionCapture?.Dispose();
            if (target != null) target.Release();
            if (baked != null) UnityEngine.Object.DestroyImmediate(baked);
            if (error != null) Debug.LogError("[P1] " + error);
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
