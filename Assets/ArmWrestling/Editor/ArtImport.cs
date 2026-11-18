using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ArmWrestlingVR.EditorTools
{
    /// <summary>
    /// 把下载来的美术资源设成工程需要的导入参数：
    /// 人物 FBX → Humanoid 骨骼（这样才能用 Animator IK 驱动手臂）；
    /// HDRI → 2D 全景贴图（给 Skybox/Panoramic 用）。
    /// 批处理入口：BatchEntry.ImportArt()
    /// </summary>
    public static class ArtImport
    {
        public const string Ch03Path = "Assets/ArmWrestling/Resources/Characters/Ch03.fbx";
        public const string HdriPath = "Assets/ArmWrestling/Resources/HDRI/circus_arena.hdr";

        public static void ConfigureAll()
        {
            ConfigureCharacter(Ch03Path);
            ConfigureHdri(HdriPath);
            EnsureSkyboxMaterial();
            EnsureIkController();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ArmWrestling] 美术资源导入参数已配置");
        }

        private static void ConfigureCharacter(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning("[ArmWrestling] 找不到人物模型: " + path);
                return;
            }

            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning("[ArmWrestling] 不是模型文件: " + path);
                return;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.isReadable = false;

            importer.SaveAndReimport();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Avatar>().FirstOrDefault();
            Debug.Log("[ArmWrestling] 人物导入完成: " + path
                      + "，模型=" + (prefab != null)
                      + "，Avatar=" + (avatar != null)
                      + (avatar != null ? "（" + avatar.name + "）" : string.Empty));
        }

        /// <summary>
        /// 生成一个"空但开启 IK Pass"的 AnimatorController。
        /// 没有 Controller 时 Unity 不会回调 OnAnimatorIK，模型会一直保持 T-pose。
        /// </summary>
        private static void EnsureIkController()
        {
            const string path = "Assets/ArmWrestling/Resources/Characters/OpponentIK.controller";

            UnityEditor.Animations.AnimatorController existing =
                AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path);

            if (existing == null)
            {
                existing = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(path);
            }

            if (existing.layers.Length > 0)
            {
                existing.layers[0].iKPass = true;

                // 关键：层里必须有状态和片段，否则骨骼会被重置，模型直接"消失"
                var machine = existing.layers[0].stateMachine;
                if (machine.states == null || machine.states.Length == 0)
                {
                    const string clipPath = "Assets/ArmWrestling/Resources/Characters/IdlePose.anim";
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                    if (clip == null)
                    {
                        clip = new AnimationClip();
                        clip.name = "IdlePose";
                        AssetDatabase.CreateAsset(clip, clipPath);
                    }

                    var state = machine.AddState("Idle");
                    state.motion = clip;
                    machine.defaultState = state;
                }
            }

            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            Debug.Log("[ArmWrestling] IK 控制器已就绪: " + path);
        }

        /// <summary>生成一个用 Panoramic 着色器的天空盒材质资产，运行时从 Resources 加载。</summary>
        private static void EnsureSkyboxMaterial()
        {
            const string materialPath = "Assets/ArmWrestling/Resources/HDRI/SkyboxPanoramic.mat";

            Texture2D hdri = AssetDatabase.LoadAssetAtPath<Texture2D>(HdriPath);
            if (hdri == null)
            {
                Debug.LogWarning("[ArmWrestling] 天空盒素材：HDRI 还没导入完");
                return;
            }

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (existing == null)
            {
                Shader shader = Shader.Find("Skybox/Panoramic");
                if (shader == null)
                {
                    Debug.LogWarning("[ArmWrestling] 编辑器里也找不到 Skybox/Panoramic");
                    return;
                }

                existing = new Material(shader);
                AssetDatabase.CreateAsset(existing, materialPath);
            }

            existing.SetTexture("_MainTex", hdri);
            existing.SetFloat("_Exposure", 1.15f);
            existing.SetFloat("_Rotation", 120f);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            Debug.Log("[ArmWrestling] 天空盒材质已就绪: " + materialPath);
        }

        private static void ConfigureHdri(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning("[ArmWrestling] 找不到 HDRI: " + path);
                return;
            }

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("[ArmWrestling] 不是贴图: " + path);
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            Debug.Log("[ArmWrestling] HDRI 导入完成: " + path);
        }
    }
}

