# Unity/VR 项目架构调研与轻量方案（2026-11-18）

> **性质：参考方案，不是冻结设计。** 本文最初写于接收工程之前；P0 现已接管 `banshouwan/test1`，实际版本、入口和已有功能以 [P0 交接](P0-handover-result.md) 为准。任何目录移动、脚本拆分均以“先运行原工程、保留有效内容”为前提。

## 1. 调研依据（外部参考，不代表原工程已有这些功能）

1. **Unity 官方工程组织建议**：明确区分自有和第三方资源，重视 `.meta` 随资源移动，合理使用 Prefab；多成员协作时大场景可拆分，但并非所有项目都需要加法加载。https://unity.com/how-to/organizing-your-project
2. **Unity XR Interaction Toolkit Examples**：以可运行场景、XR Origin/交互预制体和按功能分组的资源展示 VR 输入、抓取和世界空间 UI。目前主线示例面向 Unity 6000.3；与旧 Unity 工程不能不经核验直接混用。https://github.com/Unity-Technologies/XR-Interaction-Toolkit-Examples
3. **Unity Open Project / Chop Chop**：较大型开放项目采用多场景加载、ScriptableObject 事件通道、状态机和资源加载架构，值得理解模块边界；对本项目 P0 而言整套照搬属于过度设计。https://github.com/UnityTechnologies/open-project-1/wiki/Game-architecture-overview
4. **PICO Unity Integration SDK**：面向 PICO 设备的渲染、追踪、输入与 OpenXR 支持，但具体 SDK 版本/头显型号/Unity 编辑器兼容性要逐项核对。https://developer.picoxr.com/document/unity/ ；https://developer-cn.picoxr.com/document/unity/hardware-and-software-requirements/
5. **Unity 元数据和版本控制**：`.meta` 内的 GUID 保持资源/脚本引用；实际 Unity 项目通常提交 `Assets`、`Packages`、`ProjectSettings` 并忽略 `Library`；参考 https://docs.unity.cn/Manual/AssetMetadata.html 和 https://github.com/github/gitignore/blob/main/Unity.gitignore 。

## 2. 结论：功能分离，但不过度框架化

该项目并非通用 VR 游戏。主要任务是固定空间中的第一人称扳手腕：**接收机械角度并驱动握把/手臂姿态，展示人物和比赛流程**。优先从现有工程整理出五类职责：

| 职责 | 做什么 | 不做什么 |
|---|---|---|
| Scene / XR | 桌面、头显/相机、空间位置、必要输入 | 不计算 PC 阻力 |
| Motion | 机械角度到单轴摆臂、手腕/骨骼的映射和必要的平滑/校准 | 不直接处理 UDP 套接字、不擅自增加机械自由度 |
| Character / Animation | 虚拟对手与用户表现、Animator/IK/微动作 | 不同时与 Motion 争夺同一主关节控制权 |
| GameFlow / UI | waiting、running、paused、finished、角色选择、结果提示 | 不重新设计 PC 的人物阻力库、胜负协议字段 |
| Networking | 按既有 UDP V0.1 发送角色/比赛状态，接收 angle_rad/mode/reason | 不改 STM32 协议或 PC 控制律 |

推荐数据流（概念示意）：

```text
PC UDP status
   -> VR 网络接收（校验/超时，保存最近有效状态）
   -> Motion（角度/转轴/零姿态/握持标定） -> 机械摆臂与相关骨骼
   -> GameFlow / UI（状态提示、通信异常提示）

VR 角色选择与比赛按钮
   -> GameFlow
   -> VR UDP vr_update（持续心跳）
   -> PC
```

VR 的网络接收与 Unity 对象更新应分开；不要从后台接收线程直接操作 GameObject。可用现有脚本的简单主线程处理方式，不需要引入复杂事件总线。

角度约定：PC 发送的是 **rad**，机械零位为 0，用户向获胜侧下压角度变负。Unity 的旋转轴、局部轴、零姿态与符号需在正式工程内标定，不能原封不动套用 MuJoCo 世界轴。收到 `angle_rad:null` 时保留最后有效姿态并提示无效，不能当作零位；`mode:stop` 时角度仍可能有效，不要求冻结画面。UDP V0.1 的完整语义以控制仓库文档为准：https://github.com/Ihtbfas/armwrestling/blob/main/docs/vr-udp-v01.md 。

## 3. 推荐目录（目标参考，非 P0 强制搬迁表）

```text
armwrestling-vr/
├── Assets/
│   ├── ArmWrestlingVR/          # 我们新写或整理的自有内容（名称可变）
│   │   ├── Scenes/
│   │   ├── Prefabs/
│   │   ├── Scripts/
│   │   │   ├── Motion/
│   │   │   ├── Networking/
│   │   │   ├── Characters/
│   │   │   ├── GameFlow/
│   │   │   ├── UI/
│   │   │   └── XR/
│   │   ├── Animations/
│   │   └── Art/
│   └── <现有项目/外部资源目录保持原样>
├── Packages/
├── ProjectSettings/
├── docs/
├── README.md
├── AGENTS.md
└── TASKS.md
```

注意：若交付工程原来已经有清晰的 `Assets/Scenes`、`Assets/Scripts`、Prefab 或包目录，**优先保留现状**；必要时只新增缺失功能。不要只为了符合目录树而大量移动资源、改变 GUID、重做场景或换渲染管线。Unity 主工程放仓库根目录比较直观，但若交付包有不可轻易拆分的外层目录，Codex 可选更稳妥的实际结构并在 README 明确入口。

## 4. 建议的轻量实现方式（依据真实工程选择）

- 尽量使用已有的 MonoBehaviour、Transform、Animator、Prefab 和 Inspector 绑定。先定位谁在驱动主机械轴，避免 Animator、IK、脚本同时覆盖同一关节。
- 单自由度角度驱动可单独用手动滑块/Inspector 测试值进行 Editor 测试；接入真实 UDP 再切换角度来源。只有确有必要时才引入抽象接口和多种 Provider。
- 角色选择在 VR 侧只维护 `demo`、`person_01`、`person_02`、`person_03` 等已约定 ID；“人物强弱”的参数由 PC 仓库管理。
- 游戏主流程可从一个简单的有限状态逻辑开始；尚未约定的 `winner`、`reset`、`countdown` 不应私自加入 UDP V0.1。动画/UI 的本地表现可以先做。
- XR 输入：有可用 XR Origin/XRI/PICO 组件就复用；没有则先让 Windows Editor 场景跑通，不为 P0 强制引入新版 XR SDK、完整 locomotion、依赖注入容器、ECS、Addressables、ScriptableObject 事件总线或多场景管理。
- 如果后期确实因为多人协作、编译时间或代码耦合需要，再考虑 `.asmdef`、功能子场景、更多预制体或更细的架构层。

## 5. P0 判断准则

**先完成：** 保留原始可恢复版本 -> 识别版本和依赖 -> 导入本仓库 -> 实际打开既有场景 -> 解决主要 Play 阻断 -> 交付可复现步骤和已有功能盘点。

**不要抢做：** 人物全部重建、复杂动画系统、强制升级 Unity/PICO SDK、改 PC 通信协议、模拟现实未提供的空间尺寸、宣称头显运行成功。

无法自动打开 Windows Editor 时，可完成静态导入与可复现指引，但需明确标记尚未进行 Editor Play 验证。

## 6. Public 仓库与资产授权

仓库根部 MIT LICENSE 不等于取得了同学工程中模型、音乐、纹理、插件或 Asset Store 资源的二次分发授权。首次公开导入前，应区分原创且有权公开的资源、受独立许可约束的资源、以及未知来源资源。未知或禁止再分发的内容不上传；应记录来源和本地依赖，必要时与同学确认后再决定可替换方案。
