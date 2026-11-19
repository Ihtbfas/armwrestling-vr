# P2-C 参考方案：动画目标与物理接触协同

本记录用于让 Codex **快速确定可实施方案，然后在同一轮任务中接入主场景**。列出的参考项目不是本项目运行结果，也不是要求强制安装其依赖。

## 已有可靠参考

1. **Unity 官方 XR Interaction Toolkit Examples / Physics Interactables**  
   https://github.com/Unity-Technologies/XR-Interaction-Toolkit-Examples/blob/main/Documentation/PhysicsInteractables.md  
   重点：XR/动画目标倾向于直接控制位姿，但物理刚体需要受力运动；示例使用 Velocity Tracking、关节和 Transform Joint，在被障碍阻挡时产生响应，而不是直接传送穿过碰撞体。**要检查例程针对的 Unity/XRI 版本**；当前官方最新工程不宜直接整体导入我们的 Unity 2022.3。
2. **Auto Hand（商业插件）技术思想**  
   https://earnest-robot.gitbook.io/auto-hand-docs/auto-hand/hand  
   重点：物理手跟随目标、预制握姿、指节接触式闭合。可参考设计；不要求购买，不得假设本机已安装插件。
3. **Hurricane VR：物理手与手势制作**  
   https://cloudwalker2020.github.io/HurricaneVR-Docs/manual/hands.html  
   https://cloudwalker2020.github.io/HurricaneVR-Docs/manual/hand_posing.html  
   重点：预制手势 + 手部刚体/关节互动，而不是从零推导肌肉动力学。不是本阶段必须引入的框架。
4. **Adaptive Hands（MIT 开源，作源码参考）**  
   https://github.com/jeremiah-stevens/adaptive-hands  
   已核查 README：逐指 Blend Tree、碰到障碍时停止闭合、目标手遇障可受阻；其要求 **Unity 6+**，本项目为 Unity 中国版 **2022.3.62f3c1**，不可未经兼容验证直接按包安装。MIT 可参考/复用许可允许的代码，保留许可归属。
5. **Unity 官方 PhysX、Collider / Rigidbody / 动画事件顺序**  
   https://docs.unity3d.com/2022.3/Documentation/Manual/CollidersOverview.html  
   https://docs.unity3d.com/2022.3/Documentation/Manual/ExecutionOrder.html  
   https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Physics.ComputePenetration.html  
   关键区别：ComputePenetration 只返回接触几何；给 Animator 驱动的手加 Collider 并不自动产生动态响应。采用动态 Rigidbody/Joint 时要注意 FixedUpdate/渲染更新时序、运动学驱动与碰撞力的争夺以及抖动。

## 对本工程的实际选择建议

- **先选一个最短路线完成端到端功能。** 推荐先试“预制配对手势 + 每指随接触受限的闭合”（按真实手指几何、实际角度约束），或者由 Codex 研究后确认更合适的轻量动态手跟随方案。
- 如采用**动态刚体/关节驱动的手部**：机械 Grip Anchor 仍是权威输入；物理身体只能在有限范围偏离视觉目标，记录跟随误差、接触穿透、延迟/振荡，不能让碰撞求解反向篡改机械角度。
- 如采用**动画主导的接触限制**：必须真实检测并让手指在接触时停止继续穿入，而不只是测量/打分或每帧事后随意转 14°。保留可以手动编辑的配对握姿作为自然性基础。
- 对**上臂—胸部**：碰撞代理只能指示不可行区域；若要消除穿模，需让肩/胸/肘的姿态约束协调。可用最小的骨骼/肩部姿态调整，优先自然坐姿、肘垫支撑和双腕目标；不强迫引入全身 active ragdoll。
- P2-B 的 44 个代理、Diagnose/Gizmos/CSV 可继续用于定位和 A/B。旧 Correct 在 P2-B 中改善有限且使肘部移动增加，**不应作为默认最终方案**。

## 已验证的项目基线与局限

[P2-B 实验](../P2B-collision-experiment-result.md) 的 `Diagnose` 与 `Correct` 在同一 ±30° 往返、各 181 帧中运行。零位上臂胸部重叠 45.1→33.6 mm，但全程峰值未改善；手—手代理对 30→28；肘骨活动 96.9→118.6 mm、单帧峰值 3.8→10.0 mm。**这验证了碰撞诊断能力，不能视为当前物理响应成功。**

下一轮结果要区分：`Rigidbody/Joint` 真实物理响应、碰撞约束的动画修正、纯碰撞诊断三种实现；不能把它们混称“物理手”。
