using System;
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
                    var mesh = ArmMeshCrop.RightArm(skin, arm);
                    if (mesh == null) continue;
                    mesh.name = "P1DLocalPlayerArm";
                    int triangleCount = mesh.GetIndexCount(0) > 0 ? (int)mesh.GetIndexCount(0) / 3 : 0;
                    if (existing == null) AssetDatabase.CreateAsset(mesh, path);
                    else
                    {
                        EditorUtility.CopySerialized(mesh, existing);
                        EditorUtility.SetDirty(existing);
                        UnityEngine.Object.DestroyImmediate(mesh);
                    }
                    AssetDatabase.SaveAssets();
                    Debug.Log("[P1-D] Local cropped arm triangles: " + triangleCount);
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
