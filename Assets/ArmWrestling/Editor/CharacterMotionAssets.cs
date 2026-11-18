using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ArmWrestlingVR.EditorTools
{
    public static class CharacterMotionAssets
    {
        private const string Folder = "Assets/ArmWrestling/Resources/Characters/";

        public static void PrepareDemo()
        {
            CreateAuthoredMotion();
            PrepareLocalPlayerArm();
        }

        [MenuItem("扳手腕/P1-D/Prepare Local Player Arm")]
        public static void PrepareLocalPlayerArm()
        {
            const string path = Folder + "P1DLocalPlayerArm.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null && Array.IndexOf(Environment.GetCommandLineArgs(), "-p1d-recrop") < 0) return;
            var prefab = Resources.Load<GameObject>("Characters/Ch03");
            if (prefab == null) throw new InvalidOperationException("Restore the local Ch03 model first.");
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var animator = instance.GetComponent<Animator>();
                var arm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    Transform[] bones = skin.bones;
                    if (arm == null || Array.IndexOf(bones, arm) < 0) continue;
                    var armBones = new HashSet<int>();
                    for (int i = 0; i < bones.Length; i++)
                        if (bones[i] != null && (bones[i] == arm || bones[i].IsChildOf(arm))) armBones.Add(i);
                    var source = skin.sharedMesh;
                    var keep = new bool[source.vertexCount];
                    var weights = source.boneWeights;
                    for (int i = 0; i < weights.Length; i++)
                    {
                        var w = weights[i];
                        if (!armBones.Contains(w.boneIndex0)) w.weight0 = 0;
                        if (!armBones.Contains(w.boneIndex1)) w.weight1 = 0;
                        if (!armBones.Contains(w.boneIndex2)) w.weight2 = 0;
                        if (!armBones.Contains(w.boneIndex3)) w.weight3 = 0;
                        float sum = w.weight0 + w.weight1 + w.weight2 + w.weight3;
                        keep[i] = sum >= 0.55f;
                        if (keep[i])
                        {
                            w.weight0 /= sum;
                            w.weight1 /= sum;
                            w.weight2 /= sum;
                            w.weight3 /= sum;
                            weights[i] = w;
                        }
                    }
                    var triangles = source.triangles;
                    var crop = new List<int>();
                    for (int i = 0; i < triangles.Length; i += 3)
                        if (keep[triangles[i]] && keep[triangles[i + 1]] && keep[triangles[i + 2]])
                            crop.AddRange(new[] { triangles[i], triangles[i + 1], triangles[i + 2] });
                    if (crop.Count == 0) continue;
                    // Keep bind poses and indices. Cut torso triangles and remove
                    // residual torso weights on the exposed shoulder seam.
                    var mesh = UnityEngine.Object.Instantiate(source);
                    mesh.name = "P1DLocalPlayerArm";
                    mesh.subMeshCount = 1;
                    mesh.SetTriangles(crop, 0);
                    mesh.boneWeights = weights;
                    if (existing == null) AssetDatabase.CreateAsset(mesh, path);
                    else
                    {
                        EditorUtility.CopySerialized(mesh, existing);
                        EditorUtility.SetDirty(existing);
                        UnityEngine.Object.DestroyImmediate(mesh);
                    }
                    AssetDatabase.SaveAssets();
                    Debug.Log("[P1-D] Local cropped arm triangles: " + crop.Count / 3);
                    return;
                }
                throw new InvalidOperationException("No skinned right arm on the local Ch03 model.");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        // Original reusable muscle curves. These assets are independent of the local FBX.
        public static void CreateAuthoredMotion()
        {
            const string path = Folder + "OpponentMotion.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) return;
            var prepare = MakeClip("SeatedPrepare", 2.8f, 0.33f);
            var contest = MakeClip("SeatedContest", 1.8f, 1f);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Effort", AnimatorControllerParameterType.Float);
            var layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;
            AnimatorState state = layers[0].stateMachine.AddState("Seated motions");
            layers[0].stateMachine.defaultState = state;
            var blend = new BlendTree
            {
                name = "Prepare to contest", blendParameter = "Effort",
                blendType = BlendTreeType.Simple1D, useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(blend, controller);
            blend.AddChild(prepare, 0f);
            blend.AddChild(contest, 1f);
            state.motion = blend;
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(blend);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!prepare.humanMotion || !contest.humanMotion)
                throw new InvalidOperationException("Authored clips failed Humanoid curve import.");
            Debug.Log("[P1-D] Authored Humanoid prepare/contest clips and Effort Blend Tree.");
        }

        private static AnimationClip MakeClip(string name, float seconds, float intensity)
        {
            string path = Folder + name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null) return existing;
            var clip = new AnimationClip { name = name, frameRate = 30f };
            Curve(clip, "Spine Front-Back", seconds, intensity, 0, -0.10f, 0.03f, 0.07f);
            Curve(clip, "Chest Left-Right", seconds, intensity, 0, 0.07f, -0.05f, -0.07f);
            Curve(clip, "Chest Twist Left-Right", seconds, intensity, 0, -0.08f, 0.09f, 0.04f);
            Curve(clip, "Neck Nod Down-Up", seconds, intensity, 0, 0.10f, 0.05f, -0.04f);
            Curve(clip, "Head Turn Left-Right", seconds, intensity, 0, 0.08f, -0.11f, -0.03f);
            Curve(clip, "Right Shoulder Down-Up", seconds, intensity, 0, 0.035f, -0.025f, 0.025f);
            Curve(clip, "Left Shoulder Down-Up", seconds, intensity, 0, -0.055f, 0.04f, -0.02f);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static void Curve(AnimationClip clip, string muscle, float seconds, float intensity,
            float a, float b, float c, float d)
        {
            if (Array.IndexOf(HumanTrait.MuscleName, muscle) < 0)
                throw new InvalidOperationException("Unknown Humanoid muscle: " + muscle);
            var curve = new AnimationCurve(new Keyframe(0, a * intensity), new Keyframe(0.25f * seconds, b * intensity),
                new Keyframe(0.5f * seconds, c * intensity), new Keyframe(0.75f * seconds, d * intensity),
                new Keyframe(seconds, a * intensity));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), muscle), curve);
        }
    }
}
