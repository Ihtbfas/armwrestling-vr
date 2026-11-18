# VR 端代码地图

运行入口和本轮验证结论以仓库 [README](../../README.md) 与 [P0 交接](../../docs/P0-handover-result.md)为准。原说明的 Docs 目录、旧事件协议和既往真机结果保存在本机基线中，不作为本轮运行依据。

打开 `Assets/Scenes/ArmWrestlingMain.unity` 后 Play，使用默认离线模拟。无需先初始化或生成场景。依次经过设备检查、跳过标定、角色选择、准备和倒计时；空格开启自动演示，A/左箭头与D/右箭头手动发力，Esc暂停。

| 位置 | 当前作用 |
|---|---|
| `Scripts/App/RuntimeBootstrap.cs` | 原场景运行时构建入口；Use Original Art 为本地可选美术开关，默认关闭 |
| `Scripts/App/ArmWrestlingApp.cs` | 数据源、采样管线、手臂、UI与本地比赛状态 |
| `Scripts/Presentation/ArenaFactory.cs` | 原桌面、舞台、双方手臂、对手和UI的程序化搭建 |
| `Scripts/Presentation/VirtualArmRig.cs` | 单轴摆臂、握持点与胶囊两骨 IK |
| `Scripts/Presentation/OpponentAvatar.cs` | 可选原人物的 Animator IK；姿态与材质仍需完善 |
| `Scripts/Mapping/ArmAnglePipeline.cs` | 角度映射、过滤、外推和超时处理 |
| `Scripts/Comms/VrUdpClient.cs` | 当前 Network 数据源的 JSON UDP V0.1 客户端；本轮未正式联调 |
| `Scripts/Comms/SimulatedDeviceConnection.cs` | 离线模拟，用于当前展示和验证 |
| `Scripts/Comms/ControllerAngleSource.cs` / `HandAngleSource.cs` | 原有临时手柄/手追踪输入，未做设备验证 |
| `Scripts/Comms/UdpDeviceConnection.cs` / `WireProtocol.cs` | 保留的旧二进制事件协议代码，不是当前PC协议定义 |
| `Scripts/Calibration` / `Scripts/Xr` / `Scripts/UI` | 空间校准、追踪/自定义射线和世界空间UI |
| `Editor/P0Validation.cs` | 真实 Editor Play 集成检查与可选人物高度回归 |
| `Editor/BuildUtility.cs` | Windows/PICO构建入口；本轮未跑PICO真机 |

通信以控制仓库的 [UDP V0.1](https://github.com/Ihtbfas/armwrestling/blob/main/docs/vr-udp-v01.md)为准，不从原旧协议派生新字段。已有客户端的角色 ID 和序号回绕问题见交接文档。

模型、HDRI、字体的公开边界与恢复方式见 [资产说明](../../docs/third-party-assets.md)。恢复后需在主场景 Runtime Bootstrap 勾选 Use Original Art 才使用原人物/HDRI；原美术仍有粗糙之处，默认胶囊版本用于稳定展示。Preview仅做静态摆位，不代替Main完整Play。
