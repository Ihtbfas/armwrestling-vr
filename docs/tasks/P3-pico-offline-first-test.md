# P3｜PICO 离线首测：从 Android APK 到真实头显体验，一轮完成

> **待 Codex 执行。** 基于 P2 已收口的 `main` SHA `3f30bdb0d0afd7f2c40567c885a99126e4e251d6`。工作分支 `feat/p3-pico-offline-first-test`，后续 PR base=`main`，**不得自行合并**。不再修改 P2 握姿、跨 Humanoid 框架或机械/UDP。本轮目标是把已验证的 Unity Editor 原型**真正运行在 PICO 头显上**，看到自然的双眼第一人称、可用的右手柄 UI 和完整离线比赛。

## 先确认实际设备、当前工程与官方配置

1. 先看 [PICO 官方与代码配置调研](../research/P3-pico-openxr-device-preflight.md)、[P2 收口报告](../P2-closeout.md)、`README.md`、`AGENTS.md`。检查本地 `git status` 和未跟踪的私有 Ch03、原 ZIP、视频；不覆盖/上传它们。
2. 如果本机已连接头显，通过 `adb devices -l`、设备系统设置和必要系统信息记录**PICO 型号、系统版本、手柄型号、调试授权**。如未连接/未授权，明确阻断并尽量继续准备 Development APK；不要臆造型号，也不要等用户回复才检查能做的任务。
3. 保留 **Unity 中国版 2022.3.62f3c1、PICO Unity OpenXR SDK 固定 Git commit、OpenXR 1.14.3、XRI 2.6.5**。PICO OpenXR SDK 1.4.0 的文档要求 PICO OS 5.13.0+；如果设备不满足，记录实际兼容阻断并调查最小解决方法，**未经同意不随意升级固件或整个 Unity 项目**。
4. 在 **XR Plug-in Management / OpenXR Android Settings / Project Validation** 核对实际运行配置：Android OpenXR Loader、PICO XR feature group、PICO Support/PICO OpenXR Features、对应手柄 Interaction Profile；当前 PICO4 Controller 启用、PICO4 Ultra 未启用，必须以**实际设备型号**选择。还要核查两个同名且启用不同的 `HandTracking Android` 对象，**不要按文本名字直接删除资源或 GUID**。首次只用手柄输入，无需强制启用 Hand Tracking。
5. 重点核查 `XRGeneralSettingsPerBuildTarget.asset` Android Providers 的 Automatic Loading/Running 标志与 `ArmWrestlingSetup.ConfigurePlayerSettings()` 设定的 OpenXR Manual Initialization。判断是否会导致 APK 中 `XRSettings.isDeviceActive == false`。如果确实需要，做最小修复和可观测日志；不要未经过实际运行就宣称初始化已成功。
6. 构建前确认 **Android IL2CPP + ARM64、min API、Graphics API、Build Scenes、SDK/NDK/OpenJDK、中文字形和字体许可**。当前构建工具 `BuildUtility.BuildPico4Apk` 存在，优先复用；如果目标为 Ultra/Neo3，修改设备特定配置与命名，不假装 PICO4 专用包已全部适配。维护旧 Windows 桌面回归与原 `.meta` GUID。

## 直接构建、安装与真机修复，不再拆成多轮

1. 在公开仓库可复现的状态构建 **Development Android APK**（带适量调试日志），记录 Unity 版本、包名、构建 SHA、APK 文件 SHA256、大小、构建日志、是否通过 OpenXR Validation。APK 放本机 Builds 或 `.p0-local/p3-pico/`，**不要随意将体积大的 APK 或带许可不明私有资源的包提交 Public GitHub**。
2. 若头显已在可操作 Windows 电脑上 ADB 联机：执行 `adb devices`、允许头显上的 USB debugging、`adb install -r` 安装并启动，实际采集 `logcat` 和设备录屏。发现闪退、黑屏、只显示单眼、没有 XR Tracking、启动后落入 Desktop 模式、UI 无中文字形或控制器射线错位，**在本轮自主定位、修复、重新打包并复测**，不止步于“已生成 APK”。仅在必要时参考 PICO 官方示例，避免大范围引入新包/替换项目架构。
3. **优先通过真实手柄完成离线比赛**：先用 P2-D 默认 Rocketbox，戴头显检查双眼立体画面、坐姿眼高与视点、桌面/对手比例、玩家手臂遮挡、头部旋转与重置朝向。确认右手柄姿态/射线及扳机点击能真实操作设备检查、跳过空间标定、角色选择、Ready/倒计时、Fighting、暂停/继续、结算/再来一局，不能用 Editor 的自动 UI 回调或 ADB 触摸模拟冒充头显手柄输入通过。
4. 再启用当前 P2-E **Use Authored Grip**，在相同坐姿观察 Ready 是轻握、Contest 是较紧扣握，是否突然跳变，双手重叠、虎口和上臂是否挡住现实可用视野。与默认 P2-D 简单对照即可，**不在首测顺便重做手部姿态**。不要绑定真实机械，所有角度用现有离线模拟。
5. 检查异常与使用体验：中文 UI 可读性、按钮距离/大小、空场景位置恢复方式（项目已有右手柄 A 按键置前功能）、暂停后的流程恢复、帧时间/帧率及明显卡顿；至少采集一份设备日志和一段能证明**真实头显运行**的画面。可以用 PICO Developer Center 或 adb 获取实际数据；无法量化则写“未测”，不可编造数字。
6. 实际测试中可以自主调整**PICO 专用 XR 配置、输入映射、UI 字体/尺寸、视点重置和必要性能参数**。不改 `Grip Anchor` 物理角语义、不重新调指骨、不改变 PC UDP V0.1/STM32/刹车。不能为了“看上去头显里动了”而破坏桌面正常运行。

## 验收与交付

- **必须清楚区分验证级别**：Windows Editor、Android Build、ADB 连接、PICO 实际启动 XR、手柄真实输入、完整离线比赛、PC/STM32/机械联调互不替代。
- 若有真实 PICO：记录设备与 OS 版本、Build SHA/APK Hash、启动方式、OpenXR 功能/Controller Profile、`XRSettings.isDeviceActive` 与 HMD/Controller 跟踪状态、成功经过的各页面、中文字体是否显示、性能采样、真实录屏/截图路径及日志。带头显的体验问题尽量具体，例如“手柄射线偏离按钮 10cm”，不要泛泛写“体验不佳”。
- **公开可审核证据**：合法素材的少量 PNG/GIF 和关键日志（注意截图若包含室内环境、个人信息或未授权素材，应本机保留/做安全筛选后再公开）。记录构建与设备实测的区别，必要时本机保留原始录像。
- 新增 `docs/P3-pico-offline-result.md`，写最短部署命令、设备/固件信息、试验结果及真实阻断；更新必要的 README/TASKS/AGENTS。实际 Unity 编译、Editor 回归后提交 `feat/p3-pico-offline-first-test` 并创建 base=`main` 的 PR，不自行合并。
- **没有实机可用的现实限制**：尽力交付能安装的 Development APK、Android XR 配置审查和操作说明；明确“APK 已构建但 PICO 实机未验证”，留下唯一最短的 USB/ADB/安装步骤。不可对用户宣称已经完成头显首测。

## 最终停止条件

成功路径是“设备实际启动并完成一次**真实头显 + 手柄**操作的离线完整比赛，且有可复现的录屏/日志与实际帧时间/性能信息”，不是“Editor Play 通过”或“APK Build 成功”两者之一。若仅有 Android 包/因实机缺失或固件不兼容而阻断，也如实交付部分成果，不反复重建 XR 架构或虚报完成。

**仍遵循：先参考官方、实际尝试、能够修复就直接集成并复测。**
