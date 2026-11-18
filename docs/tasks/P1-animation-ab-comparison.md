# P1-A｜虚拟对手动画路线对照实验（Animator IK vs 姿态混合 + 局部 IK）

**状态：Windows Editor 对照实验已执行。** 见 [实测结果](../P1-animation-ab-result.md)。 本任务从已验证的 P0 工程 `feat/p0-import` / `117b6e1` 出发；与尚未合并的 [PR #1](https://github.com/Ihtbfas/armwrestling-vr/pull/1) 分离。目标不是重建场景，而是**用同一人物、同一机械角度输入做出两条可比较的动画路线，判断哪种更适合扳手腕。**

## 为什么开展本轮

P0 已在 Unity 中国版 **2022.3.62f3c1** 完成 `ArmWrestlingMain` 的离线 Editor Play 和 Windows 构建。但默认展示胶囊人；本机可恢复的 `Ch03` Humanoid 虽能加载，动作质量尚未验证。

已知代码入口：
- `Assets/ArmWrestling/Scripts/Presentation/OpponentAvatar.cs`：Humanoid 的 `OnAnimatorIK`，试图让右手跟随 grip，左手/脚保持姿态。
- `Assets/ArmWrestling/Resources/Characters/OpponentIK.controller` 与 `IdlePose.anim`：现存 controller 的 `m_IKPass: 0`，IdlePose 没有动作曲线；先验证 IK Pass 是否真的生效以及 `OnAnimatorIK` 是否执行。检查 `ArtImport.cs` 对 `AnimatorController.layers` 的修改是否持久化。
- `Assets/ArmWrestling/Scripts/Presentation/VirtualArmRig.cs`：胶囊两骨 IK、握持点与单轴旋转。
- `ArenaFactory.cs`、`ArmAnglePipeline.cs`、`ArmWrestlingConfig.asset`：人物实例化、角度数据和标定；`ArmWrestlingApp.cs` 的 Update 驱动场景。
- [P0 交接记录](../P0-handover-result.md)；本机艺术资源恢复见 [许可记录](../third-party-assets.md)。

## 本轮要回答的三个问题

1. **现有路线 A：** 修复并正确验证 Humanoid/Animator IK 后，能否让 Ch03 以合理坐姿，在实时角度变化时持续握住共同握持点？哪些问题是 IK 激活/目标摆位问题，哪些是肩、肘、腕几何或姿态问题？
2. **候选路线 B：** 选取少量（建议 3 个）有代表性的对抗姿态，通过角度驱动插值 / Blend Tree，并只对需要准确握持的位置使用 IK 或等效约束，是否比 A 的视觉表现更自然、更稳定？不要求为此采购插件或重新建模。
3. **决策：** 按同一输入、相同相机/模型/场景，比较握持漂移、肘垫关系、关节反折/穿模、连续性、实现成本，明确是选 A、B、组合，还是已有资产/骨架限制导致暂时无法决定。

## 最小实施建议：方法可以自主变更

**准备：** 先 `git status`，保护未提交改动；确认当前从 `feat/p1-animation-spike` 工作。用户的原始 `banshouwan.zip` 不修改。可用 `python tools/restore_local_assets.py "..\\banshouwan.zip"` 恢复本地 Ch03，必要时按工程真实路径调整。不擅自提交许可不明的 `Ch03.fbx`、HDRI、字体、第三方动画，或包含这些资产的公开截图/视频。

**A——修复已有基线：**
- 先检查 Humanoid Avatar、Animator Controller、IK Pass、`OnAnimatorIK` 实际调用情况，必要时做最小修复。注意 `AnimatorController.layers` 的读写方式，不能只看代码里出现 `iKPass=true` 就声称生效。
- 对坐姿、模型方向/高度、左右手目标与肩/肘/腕做小范围调整，得到**一个可观察的连续动作基线**。不要通过大范围挪动相机或隐藏关节来伪装正确性。
- 机械主轴、握持点及人物身体位置不必照搬 MuJoCo 坐标；但要记录使用了什么视觉坐标/方向，不将演示参数当实物标定。

**B——尝试姿态混合 + 必要的手部约束：**
- 基于本机现有合法资源，尝试 **对手占优 / 中立 / 玩家占优** 的少量基础姿态（Unity 预制动画、Blend Tree、Animator 可控骨骼姿态或等效简单实现均可）。
- 由**同一角度输入**驱动动作过渡；主手约束以同一 grip 为目标，次级身体动作不要反过来改写实测角度。
- 可自行试用当前 Unity 版本兼容的 Animation Rigging / Two Bone IK，但先复用现有包与组件；不要求购买插件、制作大型动作库或全身物理驱动。如 Blend Tree 不适合现有 Ch03 骨骼，允许选更简单的插值方法，记录原因。
- 尽量让 A/B 可在同一调试场景或配置开关中切换；避免复制整个工程两份。

**对比：** 固定同一模型、同一参考几何、同一镜头、同一角度输入。至少检查角度 `-30° / 0° / +30°` 的演示**视觉角**（明确其与机械 rad 的换算尚待标定），以及一次连续往返运动。记录手/握持点偏差（如能测）、肘与肘垫的相对关系、穿模/反折和视觉主观差异；给出正面与侧面结果。可以自由增加一个最有信息量的指标，不要求完整测试矩阵。

## 交付、自由度与停止条件

- 优先让 **A 可运行**，再尝试 B；B 若需要完全重做人物或非法公开资产才能实现，可选轻量姿态原型或明确标出客观阻断，并报告当前最佳可用方案，不无限扩展任务。
- 截图/短视频和结果说明必须**真实来自 Unity Editor**；如无法自动捕获，可给用户精确的手动观察步骤。未经许可的人物画面保存在本机 `.p0-local/` 或其他 Git 忽略目录，不公开推送。
- 保持 `ArmWrestlingMain` 的 P0 离线选角、准备、倒计时、暂停、结算不回退。对修复运行真正必要的代码可自主改动和迭代，不要做无关大规模重构。
- `armwrestling` PC 仓库、UDP V0.1、人物阻力参数、STM32/制动策略不改；本轮不做正式 UDP/头显/实物联调，也不把物理引擎模拟全身角色作为必做任务。
- 不预设 A/B 谁胜。结果应包括：运行入口及版本、方案实现区别、至少三个姿态的验证（通过/失败与证据）、连续运动观感、尚存问题、推荐路线；更新必要的 README / TASKS 和一份简明结果文档。
- 完成后提交推送本分支 `feat/p1-animation-spike`，可创建**以 `feat/p0-import` 为 base** 的 PR（P0 尚未合并 main），附 SHA。用户确认 P0 合并后可再调整 PR base。

**原则：一次有意义的实验 > 大量预先审查。由 Codex 自行尝试、修复、取舍；只在缺少必须的本地文件/授权或涉及不可逆风险时询问用户。**
