# P2-E｜真人握姿参考与可视化定姿依据

> 2026-11-20 资料核对。本文件提供真实握法的**动作参考**和成熟编辑方法，**不是**已完成的本项目 Unity 测试结论。全部外部照片/视频只在原网站查看；不复制不明许可的图片、帧或运动数据到 Public GitHub。

## 为什么需要这一轮

- P2-E 已有 `AuthoredPairedGrip.asset`、Ready/Contest 两端、可以直接在 Unity 编辑骨骼的 Pose Preview、P2-D 跨 Rocketbox/Ch03 Humanoid 重定向、ContactGrip 和主场景选项。
- 但 `AuthoredGripAuthoring.CreateDraft()` 的起点仍由 `PoseDigits()` 的固定角度表及 `RefineContest()` 脚本旋转生成，主要**不是基于两位真实选手的相对手型、接触部位逐一摆好**。
- 现有 [P2-E 公开真实画面](../validation/p2e-authored-grip/README.md)：反侧包握略改善，第一人称松握、拇指/虎口关系不明；Rocketbox 手部代理峰值 3.64→9.61 mm。再写一组弯指角度不是下一步解决方法。

## 可靠的握姿接触依据

1. **Canadian Armwrestling Federation（CAWF）2023 规则**：https://cawf.ca/rulesfull
   - 规则明确允许拇指覆盖方式的不同选择，但应让双方拇指关节可见；**指尖平放在对方手背上**，不应该钩抓手腕或穿插对方手指。该规则包含坐式比赛的准备步骤，对本项目尤其有参考价值。
2. **AWE 官方规则 / THE GRIP**：https://www.awearmwrestling.com/ruleset
   - 裁判设置的起始姿态：先放肘、相对掌心，手掌不应大幅分离；**先合拇指、再合四指**，手腕相对较直，正面检查姿态。请区别裁判“开始握手的过程”与双方已经握住准备开赛的“最终状态”；我们机械项目通常从准备阶段就一直握住。
3. **Armwrestling 裁判定握短视频**（真实演示，可找出手指与拇指先后关系）：https://www.youtube.com/watch?v=wkr34Z7dHJE
4. **规则教学视频**（观察双方手掌中心、握姿与肘垫）：https://www.youtube.com/watch?v=lyBQBNtgFQ8
5. **真手近景静态摄影**（实际指腹/掌背层次，需确认是否适合无绑带握持）：https://www.pexels.com/photo/close-up-view-of-people-doing-an-arm-wrestling-match-7322494/
   - 参考照片是摄影素材，可能含训练/绑带/非标准角度，**不能直接作为具体双腕 3D 坐标的唯一依据**。至少结合两张不同视角/视频片段理解空间关系。

> 先按**自然、无绑带、右手对右手**的常见握法设计一个确定的版本。比赛规则存在不同合法握法，不要把某一张照片中的特殊高握、包拇指方式当成唯一正确姿态。项目目标是演示自然接触，不是做裁判判罚引擎。

## 在 Unity 中实际要摆出的关系（人眼/美术检查表）

**零位先摆“两个腕+两掌”，然后拇指，最后四指。**

- **双腕及掌根**：两位选手相向伸出右手；手掌面对面、共同握点位于掌部，不在一方手腕上；两个手掌有可见的真实贴近关系但不能大面积互相穿过。手腕在中立角相对顺直，不靠明显折腕换贴合。
- **虎口/拇指**：辨认每只手的拇指根、拇指背面、食指根间的虎口。双方拇指选择清楚的上下层与搭扣方式，不穿过对方手背/掌根；两个拇指关节从应有视角可辨认。确认**两只手各自**的拇指，并由反侧相机检查遮挡处。
- **四指**：对手的食/中/无名/小指应从对应掌侧自然卷到**另一只手的掌背**，指腹确实接近对方手背的指定区域。不能只让指尖向彼此靠拢成十指交错，也不能抓在前臂/手腕上。允许因手型宽度不同让指腹落点不完全相同；避免指关节反折和非生理扭转。
- **Ready/Contest**：Ready 也应为轻握、基本保持真实手与机械握点对应；Contest 表现略收紧，而不是 Ready 五指完全张开、Fighting 突然包住。
- **身体**：摆好的握姿必须能由原上臂、肘部与坐姿连接。不得为藏穿模任意移动机械 Grip Anchor、拉伸骨长、隐藏某根手指或移动验证相机。

建议选一组已经授权的人物在 Unity 近景中依次审查：① 双掌正面 ② 玩家第一人称 ③ 两个反侧斜视 ④ 双手上方 ⑤ 全身侧面。保存**原始/最终骨骼姿态**并在一份简短文档中说明拇指与各指腹究竟应该落在哪里；这比仅列出 30 个旋转值更有解释力。

## 使用已有工程而不重造一套工具

- **P2-E Unity Pose Preview**：`扳手腕 → P2-E → Open Authored Grip Preview`；Inspector 中选 Player/Opponent、Bone/Wrist、Show Ready/Contest、直接旋转骨骼、分别保存并 Reload。源代码在 `Assets/ArmWrestling/Editor/AuthoredGripAuthoring.cs`、`PairedContactPreviewInspector.cs`。
- **UltimateXR 官方 Hand Pose Editor**：https://www.ultimatexr.io/guides/hand-pose-editor
  - 明确支持 Fixed、Blend Pose、实际在 Scene 内旋骨、角色专属 Pose override/迁移；适配结果需人工检查。借用工作流程即可，不导入全部 SDK。
- **Blender Pose Library（官方）**：https://docs.blender.org/manual/en/4.5/animation/armatures/posing/editing/pose_library.html
  - 如 Unity 工具在实际使用中不够有效，可在本机已有 Blender 条件下用姿态库创建单帧参考，但需核对 FBX 原 Avatar 与导回 Unity 的坐标/动作。优先已有 Unity 编辑入口，避免换工具本身变成新项目。

## 怎样判断下一版是否有价值

- **先看真实画面**：零位能一眼看出是两个人握手扳手腕，不是钩爪、抓腕、十指交叉；反侧可辨认拇指及四指接触关系。
- **再看技术约束**：同一 `Grip Anchor`，较自然腕角、没有明显新增深掌根/指节穿模，肘部/身体姿态不过度退步。Collider 代理是辅助诊断，不是皮肤接触真值。
- **最后验证跨人物**：Rocketbox 定姿作为美术源；Ch03 用现有 Humanoid muscle / Avatar Profile 形成起点并作必要角色专属校准，不把源骨骼 localRotation 复制到新骨架。
- 若 Codex 在现有工具能力内无法真正依据可见参考完成合理定姿，应**如实说“仍缺人工美术审姿/实际姿态输入”**，保留当前稳定模式，不可再用一个脚本角度表伪称已经完成高质量人工定姿。
