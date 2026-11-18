using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ArmWrestlingVR.EditorTools
{
    /// <summary>
    /// 命令行批处理入口（Unity.exe -batchmode -executeMethod ...）。
    ///
    /// 用于在没有界面的情况下完成：编译校验 → 清理旧资产 → 生成参数资源与场景 → 配置 XR。
    /// 退出码 0 表示全部成功。
    /// </summary>
    public static class BatchEntry
    {
        private static readonly string[] ObsoleteAssets =
        {
            "Assets/ArmWrestling/Scripts/ArmWrestlingPrototypeBootstrap.cs",
            "Assets/ArmWrestling/Scripts/ArmWrestlingDemoMotion.cs",
            "Assets/ArmWrestling/Editor/Pico4BuildUtility.cs",
            "Assets/Scenes/ArmWrestlingPrototype.unity",
            "Assets/Scenes/ArmWrestlingLite.unity",
            "Assets/Scenes/ArmWrestlingFull.unity",
            "Assets/Scenes/Diag_NoHud.unity",
            "Assets/Scenes/Diag_NoArms.unity"
        };

        /// <summary>批处理：配置新导入的美术资源（人物 Humanoid、HDRI）后退出。</summary>
        public static void ImportArt()
        {
            try
            {
                ArtImport.ConfigureAll();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("[ArmWrestling] 美术资源导入失败: " + exception);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>批处理：启用手部追踪特性后退出。</summary>
        public static void EnableHandTrackingBatch()
        {
            try
            {
                ArmWrestlingSetup.ConfigureXrGroups();
                ArmWrestlingSetup.EnableHandTracking();
                AssetDatabase.SaveAssets();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("[ArmWrestling] 启用手部追踪失败: " + exception);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>只做编译与资源导入，用于快速检查有没有编译错误。</summary>
        public static void CompileCheck()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                Debug.Log("[ArmWrestling] 编译检查完成，没有错误。");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("[ArmWrestling] 编译检查失败: " + exception);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>只配置 XR 与 OpenXR 功能（不重新生成场景），用于排查插件配置。</summary>
        public static void ConfigureXrOnly()
        {
            try
            {
                ArmWrestlingSetup.ConfigurePlayerSettings();
                ArmWrestlingSetup.ConfigureXrGroups();
                AssetDatabase.SaveAssets();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("[ArmWrestling] XR 配置失败: " + exception);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>完整初始化：字体、参数资源、玩家设置、XR 配置、场景生成。</summary>
        public static void SetupAndGenerate()
        {
            try
            {
                DeleteObsoleteAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

                ArmWrestlingSetup.EnsureFont();
                ArmWrestlingSetup.EnsureConfigAsset();
                ArmWrestlingSetup.ConfigurePlayerSettings();
                ArmWrestlingSetup.ConfigureXrGroups();
                ArmWrestlingSetup.GenerateScenes();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                bool mainSceneOk = System.IO.File.Exists(SceneGenerator.MainScenePath);
                Debug.Log("[ArmWrestling] 批处理完成。主场景存在=" + mainSceneOk
                          + "，预览场景=" + System.IO.File.Exists(SceneGenerator.PreviewScenePath)
                          + "，字体=" + System.IO.File.Exists(ArmWrestlingSetup.FontAssetPath)
                          + "，Android支持=" + ArmWrestlingSetup.IsAndroidSupported());

                EditorApplication.Exit(mainSceneOk ? 0 : 2);
            }
            catch (Exception exception)
            {
                Debug.LogError("[ArmWrestling] 批处理失败: " + exception);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>打开预览场景并检查引用完整性，用于排查“场景里有东西丢失”一类问题。</summary>
        public static void SceneSanityCheck()
        {
            try
            {
                var scene = EditorSceneManager.OpenScene(SceneGenerator.PreviewScenePath, OpenSceneMode.Single);
                int roots = scene.GetRootGameObjects().Length;
                int transforms = 0;
                int renderers = 0;
                int missingScripts = 0;
                int nullMaterials = 0;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        transforms++;
                        if (!IsFinite(transform.position) || !IsFinite(transform.localScale))
                        {
                            Debug.LogError("[ArmWrestling] 变换含非法数值: " + transform.name);
                        }

                        Renderer renderer = transform.GetComponent<Renderer>();
                        if (renderer != null)
                        {
                            renderers++;
                            if (renderer.sharedMaterial == null)
                            {
                                nullMaterials++;
                                Debug.LogWarning("[ArmWrestling] 材质为空: " + transform.name);
                            }
                        }

                        missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                    }
                }

                ArmWrestlingApp app = UnityEngine.Object.FindObjectOfType<ArmWrestlingApp>();
                Debug.Log("[ArmWrestling] 预览场景检查: 根物体=" + roots + " 变换=" + transforms + " 渲染器=" + renderers
                          + " 空材质=" + nullMaterials + " 丢失脚本=" + missingScripts
                          + " 主控制器=" + (app != null));

                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("[ArmWrestling] 场景检查失败: " + exception);
                EditorApplication.Exit(1);
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z)
                   && !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
        }

        private static void DeleteObsoleteAssets()
        {
            foreach (string path in ObsoleteAssets)
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null || System.IO.File.Exists(path))
                {
                    bool deleted = AssetDatabase.DeleteAsset(path);
                    Debug.Log("[ArmWrestling] 清理旧资产 " + path + " -> " + deleted);
                }
            }
        }
    }
}


