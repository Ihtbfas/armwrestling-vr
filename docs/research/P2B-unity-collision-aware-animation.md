# P2-B 调研笔记｜Unity 人体碰撞与骨骼动画结合

调研日期：2026-11-19。本文件记录已核实的官方方案与对当前工程的推断；**外部文档只说明接口能力，不表示我们已经实现或验证了防穿模效果。**

## 原问题与已有基线

2026-11-19 的 P2-A 用户实图：Rocketbox 外观已正常，但零位和动态时仍可见手—手及上臂—躯干穿插。P2-A 使用 `NeutralPairedGrip.asset` 保存两个腕目标和各 15 个指骨姿态，用共同 Grip Anchor 驱动，并通过 Animator IK / 双骨解算连接肩肘；**这不是物理接触求解器**。P2-A Editor 报告另指出 ±30° 下对手肘部水平活动约 96.9 mm、最大腕折角约 26.3°、肘部蒙皮最低采样相对垫面 −14.3 mm；不可用现有角度、静态握姿的成功替代动态碰撞验证。

## 技术参考：先利用 Unity 原生功能

1. [Unity 2022.3：Introduction to collision](https://docs.unity3d.com/2022.3/Documentation/Manual/CollidersOverview.html)。Unity 的 Mesh（可见蒙皮）与 Collider（物理代理）是两套概念；没有刚体的静态 collider、带刚体和运动学刚体处理不同。动画直接写骨骼并不自动因碰撞改变。
2. [Unity 2022.3：Physics.ComputePenetration](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Physics.ComputePenetration.html)。针对一对 Collider 输入位姿后返回是否重叠以及最小分离方向/距离。至少一方必须为 Box/Sphere/Capsule/凸 Mesh Collider；此接口仅计算，不推动骨骼或 Rigidbody。可用于自定义有限去穿入逻辑，通常不要求在 FixedUpdate 内调用。
3. [Unity 2022.3：Physics.SyncTransforms](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Physics.SyncTransforms.html)。在直接改变 Transform 后若要依赖物理世界中的空间查询，注意同步；`ComputePenetration` 显式输入位姿时与等待 FixedUpdate 的物理更新不同。不要无必要每帧反复同步大量层级。
4. [Meta Hand Tracking Hands Setup — Physics Capsules](https://developers.meta.com/vr/documentation/unity/unity-handtracking-hands-setup/)。成熟 VR 手部系统可用附在指骨上的 Capsule Collider 近似体积。参考**骨骼代理碰撞体的思路**，不把 Meta 的设备 SDK 引入当前 PICO/Unity 工程。
5. [Meta Hand Grab Interactions](https://developers.meta.com/vr/documentation/unity/unity-isdk-hand-grab-interaction/)。它明确是 physics-less grab 并使用预制抓姿。说明**预制姿态与物理碰撞不是二选一**，可以保留 P2-A 预制握姿，再为需要避免穿插的部位补充局部检测/修正。

## 当前推荐的最小方案

- **先建立诊断代理而不是整套全身物理系统**：从 Rocketbox 当前骨骼/蒙皮摆位为双方掌心、拇指与关键指段、前臂、上臂、对手胸腹及肘垫生成少量、尺寸有据的 Sphere/Capsule/Box Collider。统一开关，Editor 可视化，分组统计。
- **避免误判**：两只手掌本来就应接近接触；自然相邻骨段的物理代理可略有重叠；要排除同一骨链相邻段、与任务无关的相交组合。重点观察两手的跨人体重叠，以及上臂与自己/对手躯干重叠；记录哪一部位、哪一个角度、代理检测量、真实画面。
- **再加入小范围有约束修正**：在双手共同握持框架不动的情况下，允许有限手指局部关节旋转以减少穿入；如果是上臂穿躯干，优先小幅调整肩/躯干、肘提示点及可达支撑。必要时小范围调整单套成对预制握姿；不得只靠移开整只手来掩盖问题，不拉长骨骼，不引入动态 Ragdoll。
- **动画时序尤其重要**：目前不同脚本在 Update、OnAnimatorIK、LateUpdate 修改骨骼。碰撞代理必须对应**当帧最终渲染的姿态**，修正必须在 Animator 覆盖它之前/之后的正确位置被保留。若修正仅在下一帧生效或每帧重复累积，应视为实现问题，需调整更新顺序或采用显式后处理。
- **量测诚实**：代理 Collider 的重叠深度不是 skinned mesh 实际穿模深度。要结合零位近景、三个角度与连续动作判断；在手部网格的 BakeMesh/采样辅助可以保留，但不要为了降低某个代理数值而牺牲视觉上的自然握手。

## 暂不采用

- 直接将全身改成由 PhysX 驱动的 Active Ragdoll / 动态 Joint，既增加维护风险，也容易干扰唯一机械角度控制。
- 每帧烘焙真实人体变形网格后重建非凸 MeshCollider 进行“精确碰撞”；不值得在这个原型中支付开销和配置复杂度。
- 引入完整 Meta/Hurricane VR/Auto Hand 工具包。若本地 Unity 原生方式出现明确技术阻断，再针对性调研替代。

**结论：先做“原动画 + 少量骨骼 Collider 的碰撞诊断”，确认问题部位，再选择最简单的局部修正；最终判断以实际画面而不是指标为准。**
