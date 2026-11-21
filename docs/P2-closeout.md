# P2 阶段交接

打开 `Assets/Scenes/ArmWrestlingMain.unity` 并点 Play 即可演示。默认双方使用公开的 Rocketbox Profile 与 `LegacyHumanoidGrip`。要看 P2-E 定姿，在 **Runtime Bootstrap** 勾选 **Use Authored Grip**。准备和对抗阶段分别使用 `ImagePairedGrip` 中的 Ready 与 Contest。演示结束后退出 Play；在 Edit 模式试改开关时，保留主场景原有默认值。[离线比赛操作](../README.md#最简运行windows) · [同镜头公开画廊与 GIF](validation/p2e-contact-finish/README.md)

## 可以复用的工程能力

- 玩家和对手分别选择 Humanoid Profile。两人共用单自由度 Grip Anchor，Animator、Effort Blend Tree 和 IK 连起肩、肘、腕。ContactGrip 约束局部闭合，Scene 视图可显示碰撞代理。
- 两种实际骨架已验证。Ch03 的中指校准同时匹配美术握姿与对面玩家的 Profile。第三套人物需检查 Humanoid 指骨映射、手型尺寸和配对校准。
- 关闭 **Use Humanoid Grip** 后，通过 **Use Paired Grip** 和 **Paired Collisions** 可回看 P2-C 接触、P2-B 诊断与修正或 P2-A 成对姿态。再关闭 **Use Paired Grip** 和 **Use Original Art** 即可运行胶囊。P1-E 本地人物仍依赖 Ch03 与派生手臂。[人物模式切换](../README.md)

## 视觉与几何遗留

| 情况 | 冻结版本中的观察 | 后续处理 |
|---|---|---|
| Rocketbox 可选握姿 | 对手拇指仍与玩家食指根局部相交。零位蒙皮抽样最深 3.63 mm，往返中该对代理峰值 0.24 mm。玩家侧全手蒙皮采样峰值由 6.69 增到 7.47 mm。 | 保留可选稿，在头显内判断它对实际第一人称遮挡的影响。 |
| Ch03 双人及混合配对 | 中指经 Profile 校准后，零位局部最小带符号距离分别为 +0.67 与 −0.18 mm。反侧仍可见邻指交叠。 | 沿用现有两组校准；新配对单独验证。 |
| Ch03 肘垫 | 演示几何下，肘部下缘在往返中约低于垫面 12–16 mm。 | 独立记录座椅、肩肘、垫面与握点实测数据后再处理。[实物测量指南](calibration/physical-measurement-guide.md) |

以上数值来自上一轮同镜头对照。本次收口沿用其场景、资产和代码。手部距离为有限的顶点抽样，碰撞代理使用简化体，两者测量的表面不同。[原始比较、采样范围与图像](P2E-contact-finish-result.md)

## 收口验证与资源

原 `main` 的 `8956e4f` 之后有 28 个 P2 提交。四个来源头提交逐层构成祖先链。

| 来源 | 原头提交 | 交付内容 |
|---|---|---|
| [PR #8](https://github.com/Ihtbfas/armwrestling-vr/pull/8) | `111e65d` | 公开人物与成对握姿 |
| [PR #9](https://github.com/Ihtbfas/armwrestling-vr/pull/9) | `6d2bd41` | 碰撞诊断与修正试验 |
| [PR #10](https://github.com/Ihtbfas/armwrestling-vr/pull/10) | `3c125f1` | 主场景 ContactGrip 与视觉证据 |
| [PR #11](https://github.com/Ihtbfas/armwrestling-vr/pull/11) | `82afcc0` | 跨人物、可选美术端点与局部校准 |

逐项检查 137 个公开 Assets GUID，主场景及其可静态追踪的 16 个依赖都能在公开文件内解析。另以实际 Play 验证 Resources 加载的动画和人物。最终画廊的 11 个 PNG/GIF 文件与校验清单一致。两张用户参考图、本机 Ch03、手臂网格、Profile、HDRI、字体及本地对照画面均受 Git 忽略保护。人物公开集为 Microsoft Rocketbox 的单个模型和三张贴图，随资源保留 Microsoft MIT 声明。[Rocketbox 来源及许可](P2A-avatar-and-paired-grip-result.md) · [原交付资源清单](third-party-assets.md)

### Windows Editor Play 回归

2026-11-21 使用 Unity 中国版 2022.3.62f3c1、Direct3D11 和主场景的离线模式。每次独立启动 Editor 进入 Play，自动触发现有 UI 回调，依次经过设备检查、标定页、选角、准备、倒计时、对抗、暂停、继续和结算。

| 本机工程测试组 | 用例 | 结果 |
|---|---|---|
| 跨人物与美术稿 | 默认稳定 Rocketbox；可选 Rocketbox、双 Ch03、Rocketbox 玩家对 Ch03 对手 | 4/4 通过；人物与 IK 有效，对抗中观察到 Contest 动画 |
| 历史演示 | P2-C ContactGrip 与 LegacyContactGrip、P2-B Correct、P2-A 成对姿态、本机 P1-E、胶囊 | 6/6 通过；启用碰撞的路径有实际求值 |
| 配置回退 | 美术端点缺失、无效对手 Profile、越界表面目标 | 3/3 通过，回退后比赛可结算 |

上述 13 次运行均记录 RuntimeErrors=0。原始 Play 日志与逐次结果在本机 `.p0-local/p2-closeout/`。切换到 main 后按 SHA-256 复核了 15 个私有资源、元文件和对照图，以及 19 个本机 MP4。静态导入审计无错误，原 HDRI 材质、历史 Preview 场景及私有 Ch03 Profile 的可选引用单列。

另从同一提交建立仅含 Git 跟踪文件的副本，首次导入后分别实跑默认和可选 Rocketbox。两次都使用 LegacyRuntime 字体回退和公开贴图，走完离线比赛，RuntimeErrors=0；日志分别记录 AuthoredEndpoints=False / True。首次编译有旧 Editor 设置 API 的 CS0618 警告。本轮共 15 次 Editor Play；PICO、PCVR、真实手柄、实物和 UDP 联调仍待验证。

## 下一轮 PICO 首测

1. 记录头显型号、系统版本与手柄型号。准备可传数据的 USB 线、开发者模式及调试授权，确认 `adb devices` 能识别设备。
2. 从仅含公开文件的工程构建离线 Development APK。本机已安装 Android Build Support、SDK、NDK 和 OpenJDK。当前工程使用 Unity `2022.3.62f3c1`、ARM64、IL2CPP、最低 API 29，构建列表已启用主场景。切换 Android 后检查 OpenXR Project Validation、PICO 功能组以及与实机匹配的控制器 Profile，先沿用锁定的 SDK。工程内有两个同名 HandTracking Android 子资源，启用状态不同，本机 Editor 导入曾自动改写二者的引用。制作 APK 前复核实际选中的功能与控制器输入。[工程固定的 PICO OpenXR SDK](https://github.com/Pico-Developer/PICO-Unity-OpenXR-SDK/tree/3aa3e62bff41df618529eeb60ff02c29a515dafe)
3. 先用 P2-D 默认跑完离线比赛。检查坐姿眼高、重置朝向、双眼桌面尺度、UI 中文可读性、射线与扳机点选、暂停和结算。再用相同座位与角度体验可选美术稿，比较双手遮挡和 Ready → Contest 过渡。保留头显录屏、帧时间及 logcat，并记录包对应的提交。
4. 头显输入与离线流程验收后，再安排实物零位、角度方向与握点标定，单独验证 PC/UDP 和机械同步。P2 的桌面 Editor 结果仅覆盖离线软件路径。
