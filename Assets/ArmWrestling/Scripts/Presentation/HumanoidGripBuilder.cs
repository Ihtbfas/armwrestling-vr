using System;
using UnityEngine;

namespace ArmWrestlingVR
{
    // Resolve each Avatar once. SetHumanPose is never called on the live Animator.
    public static class HumanoidGripBuilder
    {
        public struct Measurement
        {
            public float palmLength, palmWidth, thickness, forearmLength, palmarWebInset;
            public string thumbName, indexName;
            public int boneCount, leftFingers, rightFingers;
        }

        public static PairedGripPose Build(HumanoidGripDefinition definition, GripAvatarProfile player, GripAvatarProfile opponent,
            out Measurement playerSize, out Measurement opponentSize, bool useSurfaceTargets = true)
        {
            if (definition == null || player == null || opponent == null || !player.IsUsable || !opponent.IsUsable) throw new InvalidOperationException("Select two valid Humanoid profiles.");
            var pose = ScriptableObject.CreateInstance<PairedGripPose>();
            pose.hideFlags = HideFlags.DontSave;
            pose.name = "Resolved Humanoid paired grip";
            try
            {
                pose.player = Resolve(definition, player, true, out playerSize);
                pose.opponent = Resolve(definition, opponent, false, out opponentSize);
                pose.fingerStart = definition.fingerStart;
                if (useSurfaceTargets && definition.contactTargets != null) PairedSurfaceGrip.Resolve(pose,definition.contactTargets,player,opponent,definition.palmContactCenter);
                else PairedPalmContactFit.Fit(pose,definition.palmNormal,0.001f);
                if (!pose.HasContactEndpoints) throw new InvalidOperationException("Invalid resolved hand endpoints.");
                return pose;
            }
            catch { if (Application.isPlaying) UnityEngine.Object.Destroy(pose); else UnityEngine.Object.DestroyImmediate(pose); throw; }
        }

        private static PairedGripPose.Hand Resolve(HumanoidGripDefinition definition, GripAvatarProfile profile, bool player, out Measurement size)
        {
            var instance = UnityEngine.Object.Instantiate(profile.prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            instance.transform.localScale = Vector3.one * profile.modelScale;
            try
            {
                var animator = instance.GetComponent<Animator>(); animator.enabled = false;
                foreach (var required in new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Neck,
                    HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder })
                    if (animator.GetBoneTransform(required) == null)
                        throw new InvalidOperationException("Collision diagnostics require the mapped bone: " + required);
                var fingers = PairedGripPose.MapFingers(animator);
                var wrist = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (Array.Exists(fingers, f => f == null)) throw new InvalidOperationException("Humanoid requires all 15 right finger bones.");
                var hand = new PairedGripPose.Hand();
                float thickness, palmarInset;
                Vector3 surfaceNormal;
                Vector3[] palmPoints;
                using (var handler = new HumanPoseHandler(animator.avatar, instance.transform))
                {
                    var human = new HumanPose(); handler.GetHumanPose(ref human);
                    var extended = new Vector4[5];
                    for (int d = 0; d < 5; d++) extended[d] = new Vector4(1f,0f,1f,1f);
                    Muscles(ref human,extended,null); handler.SetHumanPose(ref human);
                    Vector3 f = (fingers[6].position-wrist.position).normalized;
                    Vector3 n = Vector3.Cross(fingers[3].position-fingers[12].position,f).normalized;
                    surfaceNormal = HandSurfaceGeometry.PalmNormal(animator,f,n,Vector3.Distance(wrist.position,fingers[6].position));
                    Debug.Log($"[P2-D] Bone-to-skin palm normal angle={Vector3.Angle(n,surfaceNormal):F2}");
                    f = Vector3.ProjectOnPlane(f,surfaceNormal).normalized;
                    thickness = PalmThickness(animator,wrist,f,surfaceNormal,Vector3.Distance(wrist.position,fingers[6].position),out palmPoints,out palmarInset);
                    Muscles(ref human, definition.open, profile.muscleOffsets); handler.SetHumanPose(ref human);
                    hand.openFingerRotations = Read(fingers, profile.thumbOpposition, definition.thumbOppositionTwist);
                    Muscles(ref human, definition.closed, profile.muscleOffsets); handler.SetHumanPose(ref human);
                    hand.fingerRotations = Read(fingers, profile.thumbOpposition, definition.thumbOppositionTwist);
                }
                Vector3 forward = (fingers[6].position - wrist.position).normalized;
                Vector3 dorsal = surfaceNormal;
                forward = Vector3.ProjectOnPlane(forward,dorsal).normalized;
                size = new Measurement {
                    palmLength = Vector3.Distance(wrist.position,fingers[6].position),
                    palmWidth = Vector3.Distance(fingers[3].position,fingers[12].position),
                    forearmLength = Vector3.Distance(wrist.position,animator.GetBoneTransform(HumanBodyBones.RightLowerArm).position),
                    boneCount = instance.GetComponentsInChildren<Transform>().Length,
                    thumbName = fingers[0].name, indexName = fingers[3].name, rightFingers = 15 };
                for (int i = 0; i < 15; i++)
                    if (animator.GetBoneTransform((HumanBodyBones)((int)HumanBodyBones.LeftThumbProximal+i)) != null) size.leftFingers++;
                size.thickness = thickness; size.palmarWebInset = palmarInset;
                hand.palmDorsal = Quaternion.Inverse(wrist.rotation)*dorsal;
                hand.palmContactPoints = palmPoints;
                hand.collisionRadiusScale = profile.contactRadiusScale;
                hand.palmLength = size.palmLength; hand.palmWidth = size.palmWidth; hand.palmThickness = size.thickness;
                Vector3 desiredForward = player ? definition.playerForward : definition.opponentForward;
                Vector3 desiredDorsal = (player ? 1f : -1f) * definition.palmNormal.normalized;
                Quaternion basis = Quaternion.LookRotation(forward,dorsal);
                Quaternion desired = Quaternion.LookRotation(desiredForward,desiredDorsal);
                Quaternion delta = desired * Quaternion.Inverse(basis);
                delta = Quaternion.AngleAxis(player ? definition.palmRoll.x+profile.playerRoll : definition.palmRoll.y+profile.opponentRoll,desiredForward) * delta;
                hand.wristRotation = delta * wrist.rotation;
                // Align palm centres; forcing opposite right-hand webs to coincide offsets the palms tangentially.
                for (int i = 0; i < 15; i++) fingers[i].localRotation = Quaternion.Slerp(hand.openFingerRotations[i],hand.fingerRotations[i],definition.fingerStart);
                Vector3 oldWeb = Vector3.Lerp(fingers[0].position,fingers[3].position,0.65f);
                Vector3 web = Vector3.Lerp(wrist.position,fingers[6].position,0.55f);
                size.palmarWebInset += Vector3.Dot(web-oldWeb,dorsal);
                Vector3 correction = (player ? profile.playerWebCorrection : profile.opponentWebCorrection) * size.palmLength;
                Vector3 target = definition.palmContactCenter + desiredDorsal * (size.palmarWebInset + definition.surfaceGap * 0.5f)
                    + Vector3.up * (player ? definition.thumbStagger * 0.5f : -definition.thumbStagger * 0.5f)
                    + desired * correction;
                hand.wristPosition = target - delta * (web - wrist.position);
                for (int i = 0; i < 15; i++) fingers[i].localRotation = Quaternion.Slerp(hand.openFingerRotations[i],hand.fingerRotations[i],definition.fingerStart);
                Quaternion inverseWrist = Quaternion.Inverse(wrist.rotation);
                hand.thumbRootStart = inverseWrist*(Vector3.Lerp(fingers[0].position,fingers[1].position,0.17f)-wrist.position);
                hand.thumbRootEnd = inverseWrist*(Vector3.Lerp(fingers[0].position,fingers[1].position,0.83f)-wrist.position);
                hand.thumbRootRadius = Vector3.Distance(fingers[0].position,fingers[1].position)*0.27f*profile.contactRadiusScale;
                Debug.Log($"[P2-D] {profile.name} {(player ? "player" : "opponent")}: bones={size.boneCount}, fingers={size.rightFingers}/{size.leftFingers}, palm={size.palmLength:F4}/{size.palmWidth:F4}/{size.thickness:F4}, forearm={size.forearmLength:F4}, thumb={size.thumbName}, index={size.indexName}");
                return hand;
            }
            finally
            {
                instance.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(instance); else UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static Quaternion[] Read(Transform[] fingers, Vector3 opposition, float twist)
        {
            var result = new Quaternion[15];
            for (int i = 0; i < 15; i++) result[i] = fingers[i].localRotation;
            Vector3 axis = fingers[0].InverseTransformDirection(fingers[1].position - fingers[0].position).normalized;
            result[0] *= Quaternion.AngleAxis(twist,axis) * Quaternion.Euler(opposition);
            return result;
        }

        private static void Muscles(ref HumanPose pose, Vector4[] values, Vector4[] offsets)
        {
            string[] digits = { "Thumb","Index","Middle","Ring","Little" };
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                string name = HumanTrait.MuscleName[i]; if (!name.Contains("Right")) continue;
                for (int d = 0; d < 5; d++)
                {
                    if (!name.Contains(digits[d])) continue;
                    Vector4 v = values[d] + (offsets != null && offsets.Length == 5 ? offsets[d] : Vector4.zero);
                    float value = name.Contains("Spread") ? v.y : name.Contains("1") ? v.x : name.Contains("2") ? v.z : v.w;
                    pose.muscles[i] = Mathf.Clamp(value,-1f,1f);
                }
            }
        }

        private static float PalmThickness(Animator animator, Transform wrist, Vector3 forward, Vector3 dorsal, float length, out Vector3[] contactPoints, out float palmarInset)
        {
            foreach (var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var handBones = HandSurfaceGeometry.BoneIndices(animator,skin); if (handBones.Count < 5) continue;
                var points = HandSurfaceGeometry.WorldVertices(skin); var weights = skin.sharedMesh.boneWeights;
                var indices = HandSurfaceGeometry.BoneIndices(animator,skin);
                Vector3 across = Vector3.Cross(forward,dorsal).normalized;
                float width = Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.RightIndexProximal).position,
                    animator.GetBoneTransform(HumanBodyBones.RightLittleProximal).position);
                float min = float.PositiveInfinity,max = float.NegativeInfinity;
                int candidates = 0;
                var hull = new System.Collections.Generic.List<Vector3>();
                Quaternion inverse = Quaternion.Inverse(wrist.rotation);
                for (int i = 0; i < points.Length; i++)
                {
                    if (HandSurfaceGeometry.Weight(weights[i],indices) < 0.75f) continue;
                    Vector3 p = points[i]-wrist.position;
                    float along = Vector3.Dot(p,forward);
                    if (along >= length*0.15f && along <= length*0.95f && Mathf.Abs(Vector3.Dot(p,across)) <= width*0.65f)
                        hull.Add(inverse*p);
                    if (along < length*0.25f || along > length*0.7f || Mathf.Abs(Vector3.Dot(p,across)) > width*0.5f) continue;
                    float depth = Vector3.Dot(p,dorsal); min = Mathf.Min(min,depth); max = Mathf.Max(max,depth); candidates++;
                }
                Debug.Log($"[P2-D] Palm central skin samples={candidates}, thickness={max-min:F4}");
                if (max > min && hull.Count >= 8)
                {
                    Vector3 web = Vector3.Lerp(animator.GetBoneTransform(HumanBodyBones.RightThumbProximal).position,
                        animator.GetBoneTransform(HumanBodyBones.RightIndexProximal).position,0.65f);
                    palmarInset = Vector3.Dot(web-wrist.position,dorsal) - min;
                    Debug.Log($"[P2-D] Web-to-palmar surface={palmarInset:F4}, depths={min:F4}/{max:F4}");
                    contactPoints = hull.ToArray(); return max-min;
                }
            }
            throw new InvalidOperationException("No weighted palm surface for Humanoid calibration.");
        }
    }
}
