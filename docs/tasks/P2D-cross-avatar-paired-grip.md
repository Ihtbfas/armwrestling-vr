# P2-D｜跨人物通用握姿与自然接触，验证后直接集成

> 2026-11-20：一次有界联合约束未通过自然握姿验收，已恢复稳妥通用默认并停止继续调参；见[本轮结论与真实证据](../P2D-joint-constraints-result.md)。原工作流与历史资源保留。

> **状态：已直接接入主场景并完成两骨架 Editor 实测；自然扣握视觉验收未完成。** 详见[结果与限制](../P2D-cross-avatar-grip-result.md)及[真实公开画廊](../validation/p2d-visual/README.md)。 以 P2-C `3c125f1`（[PR #10](https://github.com/Ihtbfas/armwrestling-vr/pull/10)，尚未合并）为可回退基线，在本分支 `feat/p2d-cross-avatar-grip` 实施。本轮不是只做设计或技术验证：**找到可行方式后，在同一轮直接接入 `ArmWrestlingMain`、完成实际 Editor Play 和画面验收。**

## 最重要的目标

**不要再只为 Rocketbox 逐根写死手指旋转。** 实现一套“通用扳手腕握姿定义 + 人物 Humanoid 适配 + 现有动画/IK/接触限制”的工作流。换另一套合法 Humanoid 人物，原则上只改人物/配置、做必要的小量姿态校准，**不需要编辑核心 C# 控制代码**。

同时改进**实际零位双手掌根/虎口、拇指和四指**的自然性，不能只换一个数据结构而延续明显错误握姿。兼顾 −30°/0°/+30° 往返过程中对手的肩肘、肘垫和坐姿，避免为了手部减少碰撞而牺牲全身表现。

## 先读现有成果和已有成熟方案

- [P2-D 调研参考](../research/P2D-cross-avatar-grip-references.md)，优先 Unity 2022 Humanoid/HumanPoseHandler、UltimateXR 跨 Avatar Pose 编辑与 override、Meta object-relative Hand Grab 思路。可自主补充更合适的参考。
- [P2-C 真实画面画廊](../validation/p2c-visual/README.md)、[P2-C 结果](../P2C-physical-interaction-result.md)；`PairedGripPose.cs`、`ContactGripAuthoring.cs`、`AvatarCollisionExperiment.AuthoredContact.cs`、`PlayerArmVisual.cs`、`OpponentAvatar.cs`、`RuntimeBootstrap.cs`、`ArenaFactory.cs`。
- 保留 `AGENTS.md` 中的研究优先、Unity 真正运行、公开 PNG/GIF 证据、版权管理约定。

## 工作内容：同一轮完成

### 1. 通用握姿与模型适配（主线）

- 先把**握姿语义**与模型专属坐标分离：双方掌面和虎口的目标接触位置、拇指搭扣/四指包握、共同 Grip Anchor 下的两腕相对位置/朝向、open/closed 标准化手势。通用描述尽量不包含具体 FBX 路径、世界常量、Rocketbox 的骨骼 localRotation。
- 为具体 Humanoid 建立**简洁可编辑 Avatar Profile / Adapter**（字段由实现需要决定），包含 Prefab、缩放或实际尺寸、腕/手掌/虎口坐标基准、左右手骨骼映射、必要校准和碰撞代理尺寸。**允许少量模型专属覆盖，但不能让核心控制器分叉成“Rocketbox 版”和“Ch03 版”。**
- 优先复用 Unity Humanoid 的 `HumanPose` muscle 参数/Avatar 映射，或 UltimateXR 源码中真正适用的局部转换方法；把手指重定向结果用于现有 Animator/IK 而不是在错误的时序覆盖身体。运行时只保留必要计算，不必每帧重新做复杂 Avatar 解析。
- 对不同手型/骨架**按骨长和手掌尺寸建立初始比例适配**；在模型局部坐标定义掌心、虎口/指腹接触参考，避免直接复用 0.94 的统一缩放或固定 43 mm 上臂/10 mm 指节代理。对可达性不足的角度标明并回退，而非拉伸骨骼。
- 保留旧 `NeutralPairedGrip`、`ContactPairedGrip` 资产及旧模式回退；新方案不应损坏原有资源 GUID/meta、Unity scene 引用或历史测试入口。源码简单可读，不无谓建设新架构。

### 2. 必须用第二套真正不同的骨架验证

- **首选本机 Ch03** 作第二套 Humanoid 骨骼的重定向验证；它仅在本机运行，不公开 FBX、裁切网格及任何包含它的画面。若因许可/骨骼/导入问题不适用，可选择另一个具有不同局部骨轴和手型、许可明确的独立人物（例如别的生成器/资源），要记录真正的骨架差异，不得把同一 Rocketbox 仅缩放或换贴图冒充跨骨架验证。
- **必要的手掌/手指 Humanoid 映射检查**：`Animator.isHuman` / 有效 Avatar、两臂骨骼、每手需要的手指骨以及蒙皮是否正常。不同人手大小与指骨朝向应能以自动适配 + 少量可编辑配置获得合理结果。
- 在实际 Editor 中演示**选择人物/配置而不编辑任何 C# 源码**。可在 Inspector、ScriptableObject 或简易选择菜单完成，不要求复杂用户界面。若只验证“另一个对手 + 原玩家”，也应明确完成范围；理想情况下可以让玩家/对手分别选择不同人物。

### 3. 先真正纠正零位握姿，再保留接触约束

- 使用真实扳手腕标准握持参考：掌面相对、拇指与虎口形成自然搭扣、四指绕对方掌部而非抓手腕，腕部不过度折曲。
- **先修正 contact solver 起点已经存在的掌根/拇指深穿入**，再通过 open/closed 与 ContactGrip 限制剩余指节闭合。仅改 16 步/代理对数不够，必须能从正面、侧面、斜视与第一人称直接看到更自然的握持。
- 胸臂穿插在 P2-C 已有明显改善，保留有效坐姿调整，但谨慎处理负角端大侧移。不要为了改人物适配导致肘垫穿入、肘滑移、腕目标偏差或整个人离座明显恶化。
- 可根据实际困难自主决定“先一只手的自动映射 + 可编辑另一只手校准”“先固定 0° 再动态扫角”等实施次序，但**不得在小验证成功后停止等用户授权**。

## 实际验收与交付

**先有画面，再有指标。** 沿用 Unity 中国版 `2022.3.62f3c1`，实际进入 `Assets/Scenes/ArmWrestlingMain.unity` Editor Play。至少做：

1. 同一通用握姿定义下 **Rocketbox + 第二套真正不同 Humanoid** 的零位：同机位正面、侧面、斜视、玩家第一人称，拍摄不遮挡掌根/虎口的特写；展示换模型只通过资源/配置完成。
2. 两套人物各自 ±30° 三角度关键帧与约 181 帧往返，关键点和运动连续性可见。记录腕跟随共同 Grip Anchor、肘垫接触、胸臂相交、指腹/拇指穿插和求解性能；**旧代理峰值**、蒙皮采样与真实画面分开说。
3. **公开可查看 PNG/GIF**：合法素材的零位 Before/After、两套可合法公开人物（若有）的握姿对照及至少一个动态 GIF，按 `AGENTS.md` 放 `docs/validation/p2d-visual/`，单张尽量小于 1 MiB / 单 GIF 尽量 <8 MiB，并在结果文档/PR 中嵌入。Ch03 画面和文件严格本机留存，如第二骨架仅能用 Ch03，公开报告只写真实测试结果与本机证据路径。
4. 回归已有 P2-C ContactGrip、P2-A/P2-B（如资源可用）、P1-E 本地模式、默认胶囊离线选角/倒计时/暂停继续/结算；P2-C 原画廊不得被覆盖。故障时无授权模型应安全回退，不丢 GUID。
5. 文档 `docs/P2D-cross-avatar-grip-result.md` 必须明确：通用层数据字段、人物适配参数、实际选用的两套独立骨架与映射差异、如何更换人物（无代码修改）、自然握姿/残余穿模、性能/测试等级以及所有来源许可与证据链接。

## Codex 自主权与停止条件

- 快速调研 → 最小验证 → **确认可行就立即正式集成、继续优化和测试**。可以选择成熟框架的合理思路，但不要求整包安装 UltimateXR、Meta 或购买商业插件。允许修改必要 C#、资源/配置和 Editor 工具，不做与目标无关的大规模重构。
- **允许模型有适配文件**，不要为了追求“任意人物无需校准”而生成不自然握姿。跨模型可以复用**握姿含义和解算控制**，具体几何必须结合手型调整。
- 必须真实交付一个第二骨架有效的运行实例，而不是仅画架构图或在 Rocketbox 同骨架上缩放。若受客观阻断，明确证据、保持可运行的最优方案与可插拔配置接口，并写出具体下一步，但不能虚报跨骨架已通过。
- 不改变单自由度机械角度、Grip Anchor 轨迹、PC UDP V0.1、STM32、实物装置；不宣称完成 PICO/PCVR 联调。
- 交付 Git SHA、真实 Editor 截图/GIF、PR、差异和结果文档。提交推送 `feat/p2d-cross-avatar-grip`，**新 PR base = `feat/p2c-physical-grip-integration`**（P2-C PR #10 当前未合并）。不自行合并先前 PR 或 main。

**核心：以后换人物不重写控制器，同时这轮就要让双手握姿看起来更正确。**
