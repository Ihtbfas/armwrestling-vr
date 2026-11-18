# P0 接管与运行交接（2026-11-18）

本轮已把同学工程的可公开部分整理为本仓库根目录 Unity 工程，并让原有 `ArmWrestlingMain` 在匹配版本 Editor 中实际进入 Play、显示场景并完成离线比赛流程。没有重建替代工程，没有修改同级 PC 控制仓库或 UDP 协议。

## 入口、基线与公开范围

- 来源：同级 `banshouwan.zip`，2.35 GB；38,141 条 ZIP 条目，总解压量约 8.66 GB。
- 真正入口：`banshouwan/test1`，原版本 **2022.3.62f3c1 (1623fc0bbb97)**。本机匹配编辑器：`D:\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe`。这是 Unity 中国版版本号，不把它写成另一个团结版本。
- 只提取了原 Assets / Packages / ProjectSettings 的 167 个文件作为基线；未导入 Library、Builds、旧 PC 仓库副本、压缩包内的报告/幻灯片和额外未使用模型。
- 原 ZIP 未覆盖；SHA-256、逐文件散列、原 GUID 见 [源清单](p0-source-inventory.json)。本机 `.p0-local/baseline/` 保存源文件副本，原说明保存在其 `Docs/` 下。
- 三个大资源（Ch03 FBX、HDRI、中文字体）及其 `.meta` 未取得清楚的公开权限，Git 忽略，仍可从原 ZIP 精确恢复。PICO SDK 由 UPM 下载，不把 SDK 代码或二进制复制到仓库。[资产与恢复说明](third-party-assets.md)。本轮不需要 LFS。

## 做过的最小修复

| 问题 | 改动和证据 |
|---|---|
| PICO 依赖指向同学的 `D:/EdgeDownload/...`，本机不存在；首次打开停在包解析 | manifest 改为 PICO 官方 Git 固定提交 `3aa3e62bff41df618529eeb60ff02c29a515dafe`；上游 package.json 为 1.4.1，与原说明一致。保留其余原包版本，重新解析 lock |
| 默认 Network + `10.33.1.159`，不适合独立展示 | 原配置资产改为 Simulated，手动主机地址改为 `127.0.0.1`；没有改通信字段、端口或 PC 代码 |
| 无 PC OpenXR runtime 时桌面启动仍尝试初始化 XR | Standalone 关闭自动初始化并取消默认 loader；初始化工具只给 Android 自动配置 loader。Android OpenXR/PICO 设置保留；PCVR 使用者须在 Standalone 手动启用 provider 和自动初始化 |
| 原美术开关是代码静态字段且默认关闭 | 在现有 RuntimeBootstrap 暴露 `useOriginalArt`，默认关闭，保留原胶囊展示；勾选时只启用本地确实存在的原人物/HDRI，先于手臂构建判定，缺模型时保留双方胶囊手臂 |
| 原人物打开后在桌面下，画面看不到 | Animator 更新后的 renderer 世界包围盒中心 y=-0.58、半高0.75，顶部约0.17；去掉重复扣除的站立胯高，局部 y 从 `-0.30-0.95*scale` 改为 `-0.30`。修正后中心约0.32、顶部约1.07，模型可见。已有负向回归和修复后检查；坐姿/手掌/材质效果仍未完善 |

场景及原 `.meta` GUID 保留，没有批量移动素材或重新生成场景。OpenXR 设置被 Editor 正常重序列化，未有意升级 SDK/引擎。

## 原工程实际完成情况

| 项目 | 现状 | 本轮确认范围 |
|---|---|---|
| 场景 | 有：Main 运行时程序化构建；Preview 烘焙几何体 | 两者能打开、无丢失脚本；Main 可运行；Preview 仅静态摆位 |
| 桌面与空间 | 有：桌面、肘垫、座椅、灯光/舞台、握持点 | 默认胶囊场景已渲染检查；尺寸来自原常量，未经真实机械标定 |
| 虚拟对手 | 有胶囊；本地有一个 Ch03 Humanoid | 胶囊用于默认展示；原模型可加载/显示，IK绑定有效，但坐姿和材质仍粗糙，不算最终人物表现 |
| 用户手臂 | 部分：程序化胶囊上臂/前臂/手、解析两骨 IK | 模拟角度能改变画面；不是完整第一人称写实人体，不证明肘部与实物固定点一致 |
| 骨骼动画 | 部分：OpponentAvatar 的 OnAnimatorIK、IK Pass 控制器 | `IdlePose.anim` 无动画曲线，只用于维持控制器状态；无成套对抗/庆祝动作库 |
| 动作与角度 | 有：ArmAnglePipeline、VirtualArmRig，包含映射/限位/滤波/超时逻辑 | 已验证持续模拟采样与视觉角度变化；真实负角度方向、机械零位、限位和握持位置未标定 |
| UI 与流程 | 有：设备检查、标定、3种角色、准备/倒计时、比赛、暂停/继续、结算 | Main 通过现有按钮 `Press()` 回调走完；三种角色是逻辑选项，不是三套人物模型；未做真实鼠标射线/VR指针输入测试 |
| 校准 | 部分：DeviceFrameCalibration、位置微调/持久化 | 桌面可以跳过；实物夹具、头显与空间对齐未验证 |
| VR SDK | 已有 OpenXR/XRI/Hands/PICO 配置与自定义 XrPlayerRig/VrPointer | 包与代码可编译；没有确认头显、手柄、手追踪或帧率 |
| 通信 | 已有当前 Network 路径 VrUdpClient；另保留旧 UdpDeviceConnection/WireProtocol | 只读盘点，不进行本轮正式 UDP 联调；旧二进制事件代码不是当前 PC 协议的定义 |

## 实际测试与证据

Editor Play 使用真实 Unity Editor 的 Play 生命周期，命令行批处理驱动，并启用 Direct3D11 渲染；不是仅静态检查，也不是人工点 GUI 的输入验证。

- 原版本首次导入和 C# 编译通过。
- 两个原场景静态脚本引用检查通过；原项目 GUID 保持，必要包 GUID 由 UPM 缓存解析。
- Main 自动测试通过：DeviceCheck → Calibration → Lobby → Ready → Countdown → Fighting → Paused → Fighting → Finished。由现有 UI 回调推动流程，准备/倒计时及胜负自然推进，没有用 ForceState 代替该测试。
- 公开场景的已验证角度范围约 **-0.1° 至 +42°**，模拟链路持续采样，结局 PlayerWins，运行时错误计数 0。具体结果与截图：[文字证据](validation/p0-public-editor-play.txt)、[结果画面](validation/p0-public-editor-play.png)。
- 本地恢复素材后另做人物/HDRI诊断：Humanoid、Avatar、控制器、IK目标有效；高度回归检查在修复前失败、修复后通过。画面复核仍发现人物姿态/材质较粗糙，默认不开启。模型图片只保留本机，不作为可公开资产上传。
- Preview 的 Play 实测在角色选择失败：编辑器构建时添加的按钮 lambda 不会成为持久化回调；原 SceneGenerator 明确把 Preview 定位为静态摆位预览。本轮保留它，不用于打包或宣称完整交互通过。
- Windows 最终独立构建成功，Player退出码0，10步原冒烟序列全部完成，最终Lobby/Connected；最终日志没有XR_ERROR或托管异常。证据见 [独立程序记录](validation/p0-windows-player.json)。旧 SmokeTestDriver 是强制切状态的加载/存活检查，不等同上面的 UI 流程测试。
- **没有执行：** PCVR、PICO设备运行、真实手柄/手追踪、实物机械标定、STM32与PC正式UDP联调。也不把 ZIP 内历史截图/既往声称当成本轮结果。

全部原始日志在本机 `.p0-local/`：原始导入、公开 Play、Preview 失败、Windows 构建/Player、模型诊断和负向回归分别保存。版本化证据仅保留简短结果、静态检查和公开胶囊场景截图。

## 给接手者的最短操作

默认运行见根 [README](../README.md)，无需一键初始化或重新生成场景。

本机可选原美术：关闭 Editor，执行 `python tools/restore_local_assets.py "..\banshouwan.zip"`，重新打开 Main，选中 Runtime Bootstrap，在 Inspector 勾选 **Use Original Art** 再 Play。只用于继续检查原人物；默认胶囊场景更适合当前展示。此勾选是本地场景设置，注意不要把个人演示勾选改动混入公共默认。

自动复查用 README 的命令；加 `-p0art` 可在 Play 副本启用已恢复原美术，不保存场景。所有这些检查都只使用离线模拟。

本轮可公开的主要内容：`Assets/ArmWrestling/`、`Assets/Scenes/`、原 XR/XRI 设置、`Packages/`、`ProjectSettings/`；新增 `P0Validation.cs`、恢复/静态检查脚本、源清单和本交接文档。

## 下一阶段最值得处理

1. **先标定动作而非重写。** 用现有管线给负/零/正三个固定机械角，记录握持点、肘垫与对手/用户手臂的关系；验证 `VrUdpClient` 的负 rad → 正视觉角转换与真实获胜侧一致。现有机械量程34.4°、视觉倍率1.2等都是原配置，不能视为实测尺寸。
2. **解决现有客户端兼容点。** 当前角色发 `opponent_01..03`，PC 人物库使用 `person_01..03`；未经修正会走未知人物回退。`outSequence` 是 ushort，200ms心跳约3.64小时回绕，而PC拒绝倒序包。下一轮只修客户端并针对既有V0.1做隔离测试，不改PC协议。
3. **完善一个可用的对手。** 先核人物许可，再修坐姿/根骨/IK/手掌方向和材质曝光，检查在0°和两侧限位的视觉效果；现在只有一个模型和空Idle片段，无需先搭复杂动画框架。
4. **有设备再做输入与 PICO。** 实测射线、视点/字体、OpenXR配置、打包/真机性能；将PCVR、PICO、硬件UDP链路各自单列验证。
