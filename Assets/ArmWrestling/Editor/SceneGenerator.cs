using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArmWrestlingVR.EditorTools
{
    /// <summary>
    /// 场景生成工具。
    ///
    /// 本工程采用“代码即场景”的做法，原因有两点：
    /// 1. 场景内容（桌面、摆臂、手臂、对手、面板）全部由 <see cref="ArenaFactory"/> 生成，
    ///    改尺寸只需要改常量，不会出现“场景里拖好了但代码里没同步”的错位；
    /// 2. 实测发现：把生成结果烘焙成 .unity 再打包时，本机 Unity 2022.3.62f3c1 会写出
    ///    损坏的 level0（播放器启动报 “level0 is corrupted / Position out of bounds”），
    ///    而运行时构建的同一个场景完全正常。因此**打包用的主场景只放一个 RuntimeBootstrap**。
    ///
    /// 另外提供一份“预览场景”用于在编辑器里查看烘焙好的几何体，它**不加入 Build Settings**，
    /// 只用于检查比例和摆位。
    /// </summary>
    public static class SceneGenerator
    {
        public const string SceneFolder = "Assets/Scenes";
        public const string MainScenePath = SceneFolder + "/ArmWrestlingMain.unity";
        public const string PreviewScenePath = SceneFolder + "/ArmWrestlingPreview.unity";

        [MenuItem("扳手腕/生成场景/主场景（运行时构建，用于打包）", false, 21)]
        public static void GenerateMainScene()
        {
            GenerateAll(ArmWrestlingSetup.EnsureConfigAsset());
        }

        [MenuItem("扳手腕/生成场景/预览场景（仅编辑器查看，勿打包）", false, 22)]
        public static void GeneratePreviewScene()
        {
            EnsureFolder();
            ArmWrestlingSetup.EnsureFont();
            GeneratePreview(ArmWrestlingConfig.Load());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void GenerateAll(ArmWrestlingConfig config)
        {
            EnsureFolder();
            ArmWrestlingSetup.EnsureFont();

            GenerateMain();
            GeneratePreview(config);
            RegisterBuildScenes();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>主场景：只有一个 RuntimeBootstrap，进入 Play / 打包运行时自动搭建整个场景。</summary>
        private static void GenerateMain()
        {
            try
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                GameObject bootstrap = new GameObject("Runtime Bootstrap");
                bootstrap.AddComponent<RuntimeBootstrap>();
                EditorSceneManager.SaveScene(scene, MainScenePath);
                Debug.Log("[ArmWrestling] 已生成主场景: " + MainScenePath);
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[ArmWrestling] 生成主场景失败: " + exception);
            }
        }

        /// <summary>预览场景：把几何体烘焙成真实对象，方便在编辑器里检查比例与摆位。</summary>
        private static void GeneratePreview(ArmWrestlingConfig config)
        {
            try
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                ArmWrestlingApp app = ArenaFactory.Build(config);
                if (app == null)
                {
                    Debug.LogError("[ArmWrestling] ArenaFactory 未能创建主控制器。");
                    return;
                }

                EditorSceneManager.SaveScene(scene, PreviewScenePath);
                Debug.Log("[ArmWrestling] 已生成预览场景: " + PreviewScenePath + "（请勿用于打包）");
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[ArmWrestling] 生成预览场景失败: " + exception);
            }
            finally
            {
                ArenaFactory.ClearMaterialCache();
            }
        }

        private static void RegisterBuildScenes()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScenePath, true) };
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(SceneFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }
        }
    }
}
