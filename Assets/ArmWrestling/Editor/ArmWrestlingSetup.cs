using System;
using System.IO;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace ArmWrestlingVR.EditorTools
{
    /// <summary>
    /// 工程初始化工具：一键完成「中文字体 → 参数资源 → 玩家设置 → XR 与 OpenXR → 场景生成」。
    ///
    /// 这些配置分散在五六个面板里，漏一项都会导致打包后黑屏、手柄无响应或 Vulkan 崩溃，
    /// 所以做成脚本由构建流程每次调用，而不是让人手动点。
    /// 菜单在 Unity 顶部「扳手腕」下。
    /// </summary>
    public static class ArmWrestlingSetup
    {
        public const string ConfigAssetPath = "Assets/ArmWrestling/Resources/ArmWrestlingConfig.asset";
        public const string FontAssetPath = "Assets/ArmWrestling/Resources/ArmWrestlingFont.ttf";
        public const string BundleIdentifier = "com.banshouwan.armwrestlingvr";
        public const string ProductName = "VR-ArmWrestling";

        private const string XrSettingsFolder = "Assets/XR";
        private const string XrGeneralSettingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";

        private static readonly BuildTargetGroup[] XrGroups =
        {
            BuildTargetGroup.Android,
            BuildTargetGroup.Standalone
        };

        // ------------------------------------------------------------------
        // 菜单
        // ------------------------------------------------------------------

        [MenuItem("扳手腕/一键完成工程初始化", priority = 10)]
        public static void RunFullSetup()
        {
            EnsureFont();
            EnsureConfigAsset();
            ConfigurePlayerSettings();
            ConfigureXrGroups();
            GenerateScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[ArmWrestling] 工程初始化完成");
        }

        [MenuItem("扳手腕/只配置 XR", priority = 20)]
        public static void RunConfigureXr()
        {
            ConfigurePlayerSettings();
            ConfigureXrGroups();
            AssetDatabase.SaveAssets();
            Debug.Log("[ArmWrestling] XR 配置完成");
        }

        [MenuItem("扳手腕/打开手部追踪特性", priority = 30)]
        public static void EnableHandTrackingMenu()
        {
            EnableHandTracking();
        }

        // ------------------------------------------------------------------
        // 资源
        // ------------------------------------------------------------------

        /// <summary>确保参数资源存在。已有资源不会被覆盖，改参请直接改那个 .asset。</summary>
        public static ArmWrestlingConfig EnsureConfigAsset()
        {
            ArmWrestlingConfig existing = AssetDatabase.LoadAssetAtPath<ArmWrestlingConfig>(ConfigAssetPath);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder("Assets/ArmWrestling/Resources");

            ArmWrestlingConfig created = ScriptableObject.CreateInstance<ArmWrestlingConfig>();
            AssetDatabase.CreateAsset(created, ConfigAssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[ArmWrestling] 已创建参数资源: " + ConfigAssetPath);
            return created;
        }

        /// <summary>
        /// 确保中文字体存在并设为动态字体（动态才能在运行时按需生成大字号汉字）。
        /// 字体文件缺失时给一个明确的警告，而不是让界面变成一排方块。
        /// </summary>
        public static void EnsureFont()
        {
            if (!File.Exists(FontAssetPath))
            {
                Debug.LogWarning("[ArmWrestling] 缺少中文字体: " + FontAssetPath
                                 + "。请放入一个支持简体中文的 .ttf（例如 Noto Sans SC）。");
                return;
            }

            TrueTypeFontImporter importer = AssetImporter.GetAtPath(FontAssetPath) as TrueTypeFontImporter;
            if (importer == null)
            {
                return;
            }

            bool changed = false;
            if (importer.fontTextureCase != FontTextureCase.Dynamic)
            {
                importer.fontTextureCase = FontTextureCase.Dynamic;
                changed = true;
            }

            if (!importer.includeFontData)
            {
                importer.includeFontData = true;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
                Debug.Log("[ArmWrestling] 字体已设为动态: " + FontAssetPath);
            }
        }

        // ------------------------------------------------------------------
        // 玩家设置
        // ------------------------------------------------------------------

        /// <summary>
        /// 玩家与打包设置。安卓侧固定为 IL2CPP + ARM64 + 仅 GLES3：
        /// PICO 4 上用 Vulkan 会在 qglinternal::vkCreateFramebuffer 处崩溃，这条不能改回去。
        /// </summary>
        public static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Banshouwan";
            PlayerSettings.productName = ProductName;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.gpuSkinning = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, BundleIdentifier);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, BundleIdentifier);
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Android, ApiCompatibilityLevel.NET_Standard_2_0);
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_Standard_2_0);

            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.Android.forceSDCardPermission = false;

            // 只留 GLES3：PICO 4 上 Vulkan 会崩，见上面的注释。
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });

            // OpenXR 走"手动初始化"，由 XR General Settings 在启动时拉起。
            PlayerSettings.SetPropertyBool("openxr.manualInitialization", true, BuildTargetGroup.Android);
        }

        // ------------------------------------------------------------------
        // XR
        // ------------------------------------------------------------------

        /// <summary>确保 XR General Settings 存在，并把 OpenXR 加载器挂到安卓与桌面上。</summary>
        public static void ConfigureXrGroups()
        {
            XRGeneralSettingsPerBuildTarget perTarget = EnsureXrGeneralSettingsAsset();

            foreach (BuildTargetGroup group in XrGroups)
            {
                if (!perTarget.HasSettingsForBuildTarget(group))
                {
                    perTarget.CreateDefaultSettingsForBuildTarget(group);
                }

                XRGeneralSettings general = perTarget.SettingsForBuildTarget(group);
                if (general == null)
                {
                    Debug.LogWarning("[ArmWrestling] " + group + " 没有 XR General Settings");
                    continue;
                }

                // P0 defaults to a desktop demo without an installed PC OpenXR runtime.
                // PCVR users can opt in under XR Plug-in Management > Standalone.
                general.InitManagerOnStart = group == BuildTargetGroup.Android;

                XRManagerSettings manager = general.Manager;
                if (manager == null)
                {
                    manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                    manager.name = group + " XR Manager";
                    AssetDatabase.AddObjectToAsset(manager, perTarget);
                    general.Manager = manager;
                }

                // Keep the desktop provider opt-in too: its native pre-init runs in
                // standalone builds even when InitManagerOnStart is disabled.
                if (group == BuildTargetGroup.Android
                    && !XRPackageMetadataStore.AssignLoader(manager, "UnityEngine.XR.OpenXR.OpenXRLoader", group))
                {
                    Debug.LogWarning("[ArmWrestling] " + group + " 挂载 OpenXR Loader 失败");
                }

                EditorUtility.SetDirty(general);
                EditorUtility.SetDirty(manager);
            }

            EditorUtility.SetDirty(perTarget);
            AssetDatabase.SaveAssets();
        }

        private static XRGeneralSettingsPerBuildTarget EnsureXrGeneralSettingsAsset()
        {
            XRGeneralSettingsPerBuildTarget perTarget;
            if (EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out perTarget) && perTarget != null)
            {
                return perTarget;
            }

            EnsureFolder(XrSettingsFolder);

            perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(XrGeneralSettingsPath);
            if (perTarget == null)
            {
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, XrGeneralSettingsPath);
            }

            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            return perTarget;
        }

        /// <summary>
        /// 打开 OpenXR 的手部追踪特性（Unity XR Hands 提供的 HandTracking）。
        /// 手部输入要在 OpenXR 侧真的开启特性，否则运行时找不到手部子系统。
        /// 命令行：-executeMethod ArmWrestlingVR.EditorTools.ArmWrestlingSetup.EnableHandTracking
        /// </summary>
        public static void EnableHandTracking()
        {
            foreach (BuildTargetGroup group in XrGroups)
            {
                OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                if (settings == null)
                {
                    Debug.LogWarning("[ArmWrestling] " + group + " 没有 OpenXR 设置");
                    continue;
                }

                UnityEngine.XR.Hands.OpenXR.HandTracking existing =
                    settings.GetFeature<UnityEngine.XR.Hands.OpenXR.HandTracking>();

                if (existing == null)
                {
                    // 工程里还没有这个特性实例时，按 OpenXR 编辑器面板的同样做法新建一个挂进设置资源。
                    UnityEngine.XR.Hands.OpenXR.HandTracking created =
                        ScriptableObject.CreateInstance<UnityEngine.XR.Hands.OpenXR.HandTracking>();
                    created.name = "HandTracking " + group;

                    SerializedObject settingsObject = new SerializedObject(settings);
                    SerializedProperty features = settingsObject.FindProperty("features");
                    if (features == null)
                    {
                        Debug.LogWarning("[ArmWrestling] " + group + " 找不到 features 属性");
                        continue;
                    }

                    AssetDatabase.AddObjectToAsset(created, settings);
                    features.arraySize++;
                    features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = created;
                    settingsObject.ApplyModifiedPropertiesWithoutUndo();
                    existing = created;
                }

                existing.enabled = true;
                EditorUtility.SetDirty(existing);
                EditorUtility.SetDirty(settings);
                Debug.Log("[ArmWrestling] " + group + " 已启用 HandTracking 特性");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[ArmWrestling] 手部追踪特性配置完成");
        }
        // ------------------------------------------------------------------
        // 其它
        // ------------------------------------------------------------------

        public static void GenerateScenes()
        {
            SceneGenerator.GenerateAll(EnsureConfigAsset());
        }

        public static bool IsAndroidSupported()
        {
            return BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}



