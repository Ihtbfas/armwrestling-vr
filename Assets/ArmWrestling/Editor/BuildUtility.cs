using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ArmWrestlingVR.EditorTools
{
    /// <summary>
    /// 打包工具。
    ///
    /// PICO 4 用 Android + IL2CPP + ARM64 出 APK，通过 adb 安装到一体机；
    /// Windows 桌面版用于没有头显时的快速验证（模拟数据 + 鼠标键盘即可跑完整流程）。
    /// </summary>
    public static class BuildUtility
    {
        public const string ApkPath = "Builds/PICO4/ArmWrestlingVR-PICO4.apk";
        public const string WindowsPath = "Builds/Windows/ArmWrestlingVR.exe";

        [MenuItem("扳手腕/打包/构建 PICO 4 APK", false, 40)]
        public static void BuildPico4Apk()
        {
            if (!ArmWrestlingSetup.IsAndroidSupported())
            {
                throw new BuildFailedException(
                    "当前 Unity 未安装 Android Build Support，无法产出 PICO 4 的 APK。\n" +
                    "请在 Unity Hub 中选择 2022.3.62f3c1 版本 → 添加模块 → 勾选 " +
                    "Android Build Support（含 Android SDK & NDK Tools、OpenJDK），安装完成后重新执行本菜单。");
            }

            ArmWrestlingSetup.ConfigurePlayerSettings();
            ArmWrestlingSetup.ConfigureXrGroups();

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { SceneGenerator.MainScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            };

            RunBuild(options, ApkPath);

            Debug.Log("[ArmWrestling] PICO 4 安装命令示例：\n" +
                      "  adb install -r " + Path.GetFullPath(ApkPath) + "\n" +
                      "  adb shell am start -n com.banshouwan.armwrestlingvr/com.unity3d.player.UnityPlayerActivity");
        }

        [MenuItem("扳手腕/打包/构建 Windows 桌面版", false, 41)]
        public static void BuildWindows()
        {
            ArmWrestlingSetup.ConfigurePlayerSettings();

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { SceneGenerator.MainScenePath },
                locationPathName = WindowsPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.Development
            };

            RunBuild(options, WindowsPath);
        }

        [MenuItem("扳手腕/打包/检查工程配置", false, 42)]
        public static void SwitchAndCheck()
        {
            Debug.Log("[ArmWrestling] Android 支持: " + ArmWrestlingSetup.IsAndroidSupported());
            Debug.Log("[ArmWrestling] 主场景存在: " + File.Exists(SceneGenerator.MainScenePath));
            Debug.Log("[ArmWrestling] 预览场景存在: " + File.Exists(SceneGenerator.PreviewScenePath));
            Debug.Log("[ArmWrestling] 参数资源: " + ArmWrestlingSetup.ConfigAssetPath);
            Debug.Log("[ArmWrestling] 中文字体: " + File.Exists(ArmWrestlingSetup.FontAssetPath));
            Debug.Log("[ArmWrestling] 构建场景列表: " + EditorBuildSettings.scenes.Length + " 个");
        }

        private static void RunBuild(BuildPlayerOptions options, string relativePath)
        {
            string directory = Path.GetDirectoryName(relativePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            BuildReport report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException("构建失败，请查看 Console 第一条红色错误。结果: " + report.summary.result);
            }

            Debug.Log("[ArmWrestling] 构建成功: " + Path.GetFullPath(relativePath)
                      + "  用时 " + report.summary.totalTime.TotalSeconds.ToString("F1") + " s");
        }

        /// <summary>供命令行批处理调用。</summary>
        public static void BatchBuildPico4()
        {
            try
            {
                BuildPico4Apk();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("[ArmWrestling] " + exception.Message);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>供命令行批处理调用：构建 Windows 桌面版。</summary>
        public static void BatchBuildWindows()
        {
            try
            {
                BuildWindows();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("[ArmWrestling] " + exception.Message);
                EditorApplication.Exit(1);
            }
        }
    }
}
