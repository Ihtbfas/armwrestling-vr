# P3 技术核对｜PICO OpenXR 离线真机首测

调研及代码核对：2026-11-21。此文件区分 **当前 GitHub 已配置内容**、**PICO 官方要求**和 **必须在设备上实际验证的未知项**。不是 PICO 测试成功报告。

## 项目冻结基线

- P2 主分支 `main`：`3f30bdb0d0afd7f2c40567c885a99126e4e251d6`，详见 [P2-closeout](../P2-closeout.md)。15/15 Windows Unity Editor Play 通过；**没有做过 PICO 真机测试**。
- Unity 中国版 `2022.3.62f3c1`。主要包版本：`com.unity.xr.openxr 1.14.3`、`XR Interaction Toolkit 2.6.5`、`XR Hands 1.4.3`、`XR Management 4.7.0`。
- `Packages/manifest.json` 中 PICO Unity OpenXR SDK 固定到 Git commit `3aa3e62bff41df618529eeb60ff02c29a515dafe`（PICO Unity OpenXR SDK 1.4.0）。**保留锁定版本，先修配置/验证，勿无故升级 Unity/SDK 或混入另一套 Integration SDK。**
- PICO 官方 1.4.0 版本说明需要 PICO OS **5.13.0 或更高**：https://developer.picoxr.com/document/updates-unity-openxr/ 。**必须读取实机型号和系统版本**；不能假设设备已经符合。
- `ProjectSettings` 当前 Android: IL2CPP、ARM64、min API 29，Graphics API 为 OpenGLES3；`Assets/Scenes/ArmWrestlingMain.unity` 已加入 Build Settings。
- `Assets/ArmWrestling/Editor/BuildUtility.cs` 现有 `BuildPico4Apk` 与 `BatchBuildPico4`，会先调用 `ArmWrestlingSetup.ConfigurePlayerSettings/ConfigureXrGroups`。该脚本会写 XR/Android 配置，测试前先检查变更，不能为了试运行无意覆盖 P2 资源。

## 当前仓库最应优先排查的 XR 风险（来自 YAML 与 C# 静态观察）

1. `Assets/XR/Settings/OpenXR Package Settings.asset`：
   - `PICO4 Touch Controller Profile Android`：启用；
   - `PICO4 Ultra Touch Controller Profile Android`：未启用，Neo3/G3 也未启用；
   - `PICO Support`：启用；
   - `PICO OpenXR Features`：未启用；
   - 两个同名 `HandTracking Android` 子资源：一个 `m_enabled=1`、另一个 `m_enabled=0`，应在实际 Unity OpenXR Feature 设置和依赖链中确认各自 GUID/类型与生效情况。**不要直接以名字相同删除资产或批量修改 YAML。**
2. `Assets/XR/XRGeneralSettingsPerBuildTarget.asset`：Android `InitManagerOnStart=1`，已引用 OpenXRLoader；但 Android Providers 内的 `m_AutomaticLoading=0`、`m_AutomaticRunning=0`。此外 `ArmWrestlingSetup.ConfigurePlayerSettings()` 设了 `openxr.manualInitialization=true`。此组合需在**实际 Android 启动和 `XRSettings.isDeviceActive` / 追踪日志**中检查是否正常；不要只以 Build 成功推断 XR 渲染已启动。
3. `XrPlayerRig.cs` 直接通过 `InputDevices.GetDeviceAtXRNode` 读取头显、双手柄位置和按钮，`VrUi.cs` 的 `VrPointer` 以右手柄 forward 射线 + trigger 点击 UI。**不是 XRI Action Preset**；若无射线，要分别判断 XR loader、追踪节点、按钮映射、方向及物理 UI Collider，而不是盲装一套 XRI 预制体。
4. `FontProvider` 缺少本机非公开字体时回退 `LegacyRuntime.ttf`。**在 Android 上必须实际检查中文 UI 是否有字形**；如果缺字，使用公开授权明确、体积合理的中文字体/字形子集，保留许可与来源，不上传原 Ch03 字体文件。
5. 当前主场景在 Edit 模式只挂 `RuntimeBootstrap`，真正的 Camera、XR Rig、UI 与人物在 Awake 中建立；Project Validation 的静态检查不一定能看到运行时相机。测试应检查**真正构建出的设备和输入**。

## 官方参考及版本边界

- [PICO OpenXR：Complete project settings](https://developer.picoxr.com/document/unity-openxr/complete-project-settings/)：打开 Android OpenXR、PICO XR Feature Group、处理 Project Validation；Controller Interaction Profile **按设备型号选择**。例如 PICO 4→PICO4 Touch；PICO 4 Ultra→PICO4 Ultra Touch。官方还要求复核 PICO OpenXR Features 与 PICO Support。
- [PICO Developer Center quickstart](https://developer.picoxr.com/document/unity/pdc-basic-info/)：头显开发者模式/USB Debug、设备与 ADB 连接、录屏/日志/性能数据。Windows 本机应确认 `adb devices` 为 `device`，不是 unauthorized/offline。
- [PICO Unity OpenXR 1.4.0 release notes](https://developer.picoxr.com/document/updates-unity-openxr/)：最低系统版本要求以及已知手部 Pinch/UI 问题；**本轮优先手柄扳机，不把裸手追踪当作首测必备**。
- [Unity OpenXR 文档](https://docs.unity3d.com/2022.3/Documentation/Manual/com.unity.xr.openxr.html)：平台插件/交互 Profiles/Validation。请按本机安装版本检查，不盲点 Fix All、升级 SDK 或重建场景。

## 首测验证层级

A. **仅静态/Windows Editor**：配置审核、构建 API 能运行，但没有 Android 真机证据。
B. **Android Development APK 已生成**：可回溯 SHA、包名与构建日志；仍不代表头显已启动。
C. **ADB 已安装 + PICO 启动渲染**：实机 XR 初始化/双眼画面/跟踪成功，收集 `logcat`。
D. **完整 PICO 离线比赛**：同一戴头显用户使用手柄完成设备检查/跳过标定/选角/准备/倒计时/对抗/暂停/继续/结算；比较默认 P2-D 和可选 P2-E 的第一人称距离/遮挡/Ready→Contest。
E. **PC UDP/机械/真实力反馈**：另一条任务线，**本轮不包含**。

**只有实际达到相应层级时才在结论中打勾。** 如果用户尚未把头显接到 Codex 可操作的电脑，也应尽量完成 B 并留下最短安装/验收说明；不要虚报 C/D。
