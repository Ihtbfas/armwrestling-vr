using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ArmWrestlingVR.EditorTools
{
    public static class RocketboxAssets
    {
        private const string ResourceRoot = "Assets/ArmWrestling/Resources/";

        [MenuItem("扳手腕/P2-A/Prepare Rocketbox Model")]
        public static void Prepare() => PrepareModel("P2A/", "Male_Adult_01", "m002");

        [MenuItem("扳手腕/P2-F/Prepare Rocketbox Opponents")]
        public static void PrepareOpponents()
        {
            PrepareOpponent("Female_Adult_01", "f001");
            PrepareOpponent("Sports_Female_02", "f013");
            PrepareOpponent("Male_Adult_03", "m004");
            AssetDatabase.SaveAssets();
        }

        private static void PrepareOpponent(string modelName, string materialPrefix)
        {
            string folder = "P2F/" + modelName + "/";
            var model = PrepareModel(folder, modelName, materialPrefix);
            string profilePath = ResourceRoot + folder + "Profile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<GripAvatarProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<GripAvatarProfile>();
                profile.modelScale = 0.94f;
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            profile.prefab = model;
            EditorUtility.SetDirty(profile);
            if (!profile.IsUsable)
                throw new InvalidOperationException("Unusable Rocketbox profile: " + profilePath);
        }

        private static GameObject PrepareModel(string relativeFolder, string modelName, string materialPrefix)
        {
            string folder = ResourceRoot + relativeFolder;
            string modelPath = folder + modelName + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.optimizeGameObjects = false;
            importer.isReadable = true;
            var description = importer.humanDescription;
            (HumanBodyBones, string)[] map =
            {
                (HumanBodyBones.Hips, "Pelvis"), (HumanBodyBones.Spine, "Spine"),
                (HumanBodyBones.Chest, "Spine1"), (HumanBodyBones.UpperChest, "Spine2"),
                (HumanBodyBones.Neck, "Neck"), (HumanBodyBones.Head, "Head"),
            };
            var humanoid = map.Select(entry => Mapped(entry.Item1, entry.Item2)).ToList();
            foreach (var side in new[] { "Left", "Right" })
            {
                var prefix = side == "Right" ? "R " : "L ";
                (string, string)[] limbs =
                {
                    ("Shoulder", "Clavicle"), ("UpperArm", "UpperArm"), ("LowerArm", "Forearm"), ("Hand", "Hand"),
                    ("UpperLeg", "Thigh"), ("LowerLeg", "Calf"), ("Foot", "Foot"), ("Toes", "Toe0"),
                };
                foreach (var entry in limbs)
                    humanoid.Add(Mapped(Enum.Parse<HumanBodyBones>(side + entry.Item1), prefix + entry.Item2));
                var digits = new[] { "Thumb", "Index", "Middle", "Ring", "Little" };
                var joints = new[] { "Proximal", "Intermediate", "Distal" };
                for (int finger = 0; finger < digits.Length; finger++)
                    for (int joint = 0; joint < joints.Length; joint++)
                        humanoid.Add(Mapped(Enum.Parse<HumanBodyBones>(side + digits[finger] + joints[joint]),
                            prefix + "Finger" + finger + (joint == 0 ? "" : joint.ToString())));
            }
            description.human = humanoid.ToArray();
            importer.humanDescription = description;
            Remap(importer, folder, materialPrefix, "body", ".jpg");
            Remap(importer, folder, materialPrefix, "head", ".jpg");
            Remap(importer, folder, materialPrefix, "opacity", ".png");
            AssetDatabase.SaveAssets();
            importer.SaveAndReimport();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var sample = UnityEngine.Object.Instantiate(model);
            try
            {
                var animator = sample.GetComponent<Animator>();
                if (animator == null || !animator.isHuman || !animator.avatar.isValid)
                    throw new InvalidOperationException("Rocketbox Humanoid import failed.");
                var skin = sample.GetComponentInChildren<SkinnedMeshRenderer>();
                var weights = skin.sharedMesh.boneWeights;
                var skinBones = skin.bones;
                for (int i = 0; i < PairedGripPose.FingerCount; i++)
                {
                    var joint = animator.GetBoneTransform(PairedGripPose.Finger(i));
                    int index = Array.IndexOf(skinBones, joint);
                    if (joint == null || index < 0 || !weights.Any(w =>
                        (w.boneIndex0 == index && w.weight0 > 0f) || (w.boneIndex1 == index && w.weight1 > 0f)
                        || (w.boneIndex2 == index && w.weight2 > 0f) || (w.boneIndex3 == index && w.weight3 > 0f)))
                        throw new InvalidOperationException("Unweighted or missing joint: " + PairedGripPose.Finger(i));
                }
                if (skin.sharedMaterials.Any(m => m == null || m.mainTexture == null))
                    throw new InvalidOperationException("Rocketbox material remap failed.");
                Debug.Log($"[Rocketbox] {modelName}: valid Humanoid; 15/15 weighted right-hand joints; {skin.sharedMesh.vertexCount} vertices; "
                    + string.Join(", ", skin.sharedMaterials.Select(m => m.mainTexture.name)));
            }
            finally { UnityEngine.Object.DestroyImmediate(sample); }
            return model;
        }

        private static void Remap(ModelImporter importer, string folder, string materialPrefix, string part, string extension)
        {
            string name = materialPrefix + "_" + part;
            string path = folder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + name + "_color" + extension);
            material.SetFloat("_Glossiness", 0.19f);
            if (part == "opacity")
            {
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.SetFloat("_Mode", 1);
                material.SetFloat("_Cutoff", 0.45f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            EditorUtility.SetDirty(material);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), material);
        }

        private static HumanBone Mapped(HumanBodyBones bone, string suffix) => new HumanBone
        {
            boneName = "Bip01 " + suffix,
            humanName = HumanTrait.BoneName[(int)bone],
            limit = new HumanLimit { useDefaultValues = true }
        };
    }
}
