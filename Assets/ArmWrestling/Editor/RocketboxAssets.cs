using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ArmWrestlingVR.EditorTools
{
    public static class RocketboxAssets
    {
        private const string Folder = "Assets/ArmWrestling/Resources/P2A/";
        private const string ModelPath = Folder + "Male_Adult_01.fbx";

        [MenuItem("扳手腕/P2-A/Prepare Rocketbox Model")]
        public static void Prepare()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
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
            Remap(importer, "body", ".jpg");
            Remap(importer, "head", ".jpg");
            Remap(importer, "opacity", ".png");
            AssetDatabase.SaveAssets();
            importer.SaveAndReimport();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
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
                Debug.Log($"[Rocketbox] Valid Humanoid; 15/15 weighted right-hand joints; {skin.sharedMesh.vertexCount} vertices; "
                    + string.Join(", ", skin.sharedMaterials.Select(m => m.mainTexture.name)));
            }
            finally { UnityEngine.Object.DestroyImmediate(sample); }
        }

        private static void Remap(ModelImporter importer, string part, string extension)
        {
            string name = "m002_" + part;
            string path = Folder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + name + "_color" + extension);
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
