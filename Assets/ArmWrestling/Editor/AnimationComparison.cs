using System;
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
                if (Resources.Load<GameObject>("Characters/Ch03") == null)
                    throw new InvalidOperationException("Restore the local Ch03 asset first.");
                EditorSceneManager.OpenScene(SceneGenerator.MainScenePath, OpenSceneMode.Single);
                UnityEngine.Object.FindObjectOfType<RuntimeBootstrap>().useOriginalArt = true;
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

                string label = route == 0 ? "A" : "B";
                if (frame == 0)
                {
                    avatar.BlendBodyPoses = route == 1;
                    Directory.CreateDirectory(Path.Combine(output, label));
                }

                // Three poses at matching timestamps: frame 0 = -30, 45 = 0, 90 = +30.
                float angle = -30f * Mathf.Cos(2f * Mathf.PI * frame / SweepFrames);
                rig.ApplyAngle(angle);
                int countBefore = avatar.IkPassCount;
                animator.Update(1f / FrameRate);
                int calls = avatar.IkPassCount - countBefore;
                Record(label, angle, calls);
                if (frame == 0 || frame == 45 || frame == 90)
                {
                    Capture(front, Path.Combine(output, label, "front-" + frame.ToString("D3") + ".png"));
                    Capture(side, Path.Combine(output, label, "side-" + frame.ToString("D3") + ".png"));
                }
                if (!HasArgument("-p1stills"))
                {
                    Capture(front, Path.Combine(output, label, "f-" + frame.ToString("D3") + ".jpg"));
                    Capture(side, Path.Combine(output, label, "s-" + frame.ToString("D3") + ".jpg"));
                }
                if (calls == 0)
                    throw new InvalidOperationException("The Animator IK pass did not execute.");

                if (++frame > SweepFrames)
                {
                    Debug.Log("[P1] Captured " + label + " | callbacks=" + avatar.IkPassCount);
                    if (++route == 2) Finish(null);
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
            if (avatar == null) throw new InvalidOperationException("Ch03 was not instantiated.");
            animator = avatar.GetComponent<Animator>();
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
            output = Path.GetFullPath(Path.Combine(".p0-local", "p1-animation",
                DateTime.Now.ToString("yyyyMMdd-HHmmss")));
            Directory.CreateDirectory(output);
            target = new RenderTexture(768, 576, 24);
            pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            data = new StreamWriter(Path.Combine(output, "measurements.csv"));
            data.WriteLine("route,frame,seconds,angle,ik_calls,grip_to_wrist_mm,target_to_wrist_mm,grip_to_palm_proxy_mm,elbow_dx_mm,elbow_dy_mm,elbow_dz_mm,elbow_flex_deg,wrist_bend_deg,hips_x,hips_y,hips_z,shoulder_x,shoulder_y,shoulder_z,head_x,head_y,head_z,elbow_x,elbow_y,elbow_z,wrist_x,wrist_y,wrist_z");
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
            var pad = new Vector3(-ArenaFactory.ElbowPadX, 0.0425f, -ArenaFactory.ElbowPadZ);
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
                + Point(hips) + Point(shoulder) + Point(head) + Point(elbow) + Point(wrist));
            if (frame == 0 || frame == 45 || frame == 90) data.Flush();
        }

        private static Transform Bone(HumanBodyBones bone) => animator.GetBoneTransform(bone);

        private static string Point(Transform bone)
        {
            Vector3 p = rig.transform.InverseTransformPoint(bone.position);
            return string.Format(Format, ",{0:F4},{1:F4},{2:F4}", p.x, p.y, p.z);
        }

        private static void Capture(Camera camera, string path)
        {
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            try
            {
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
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
            if (target != null) target.Release();
            if (error != null) Debug.LogError("[P1] " + error);
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
