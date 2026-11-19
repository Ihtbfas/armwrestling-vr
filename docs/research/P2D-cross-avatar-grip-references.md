# P2-D｜跨人物握姿复用：已调研的现成技术

**用途：** 给 Codex 选择实现路线，先读成熟案例、少量验证、**有效即直接集成**到 `ArmWrestlingMain`。以下只是外部技术依据，并不是本项目已跑通的结果。需适配 Unity 中国版 `2022.3.62f3c1`。

## 1. Unity Humanoid / HumanPose（引擎原生）

- [Unity 2022.3 HumanPose](https://docs.unity.cn/2022.3/Documentation/ScriptReference/HumanPose.html)：人物姿态使用与具体骨架层级解耦的 Humanoid 肌肉值。
- [HumanPoseHandler](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/HumanPoseHandler.html)：读取、设置 Humanoid 姿态。可以在**编辑器标定/离线转换**时对第二套 Avatar 应用一组标准肌肉参数，再读取其对应指骨局部旋转，供当前的手指动画与碰撞限位使用。先在目标编辑器真实测试 API 与骨骼映射。
- [Unity 人形动画重定向](https://docs.unity3d.com/2022.3/Documentation/Manual/Retargeting.html)：不同角色可复用人形动画，但手型/骨长不同意味着“能重定向”不代表“掌面无穿模”。
- **注意：** 当前 `OpponentAvatar` 的 Animator/OnAnimatorIK 与 `PlayerArmVisual` 手动求解同时存在，不能每帧无差别调用 `SetHumanPose` 覆盖整个人身体。推荐将跨骨架转换放在编辑/缓存阶段，运行时主要更新手指和腕部目标。

## 2. UltimateXR：跨 Avatar 握姿编辑与覆盖

- [UltimateXR Hand Pose Editor](https://www.ultimatexr.io/guides/hand-pose-editor)
- [MIT 源码](https://github.com/VRMADA/ultimatexr-unity)（其 `package.json` 自述 Unity 2021.2 起）。
- **可参考：** Fixed/Blend 两类姿态、从其他人物导入姿态、跨骨骼坐标系交换、Prefab 继承基础姿态以及人物专属 override。官方同样提醒：两套骨架有差异时，结果需要修正。
- **不要求：** 安装整个 UltimateXR、将现有 PICO/XRI 替换，或复制其大型架构。学习“通用握姿 + 每人物小量覆盖”的表达及编辑流程即可；如复用其 MIT 代码需按许可保留归属。

## 3. Meta Hand Grab：以握持对象为坐标系

- [Meta Interaction SDK Hand Grab](https://developers.meta.com/vr/documentation/unity/unity-isdk-hand-grab-interaction/)
- **可参考：** 每只手相对 Grip Anchor 的腕目标，标准的 open/closed 姿态，按实际手型修正空间对位。我们不是抓取静止物体，而是**两只人手形成一个共同握持关系**：通用接触语义应包括掌根/虎口/拇指搭扣、四指包握区域、共同参考坐标与必要的左右/前后偏置。
- 不导入 Meta 专用 SDK。

## 4. 当前 P2-C 真正的绑定点

- `PairedGripPose.AvatarResource = "P2A/Male_Adult_01"` 与 `AvatarScale = 0.94` 写死 Rocketbox。
- `PairedGripPose.Hand.fingerRotations/openFingerRotations` 存 **15 个目标骨架 localRotation**，不能无条件直接复制到另一套 Humanoid。
- `ContactGripAuthoring` 自动摆骨并调用 `HumanPoseHandler` 生成 Rocketbox 姿态，但没有完成跨人物转换与真实手型适配。
- `PlayerArmVisual`、`OpponentAvatar` 和 `AvatarCollisionExperiment` 也有独立的身材、腕偏置、碰撞代理尺寸、演示肘垫可达常数。
- 目前 P2-C 截图与 [画廊](../validation/p2c-visual/README.md) 清楚表明：虎口/拇指根部仍交叠，侧面包握有改善但不自然。当前 P2-C 的旧手部代理峰值约 15.27 mm；数据是**代理量，不是网格真实穿深**。

## 初步选型，不做教条式规定

- **通用握姿层**：描述共同握点、双方腕目标的相对语义、手指标准化 open/closed（例如 Humanoid muscle 参数）和接触区域。
- **Avatar 适配层**：由模型 Prefab/Animator/骨骼与手型尺寸自动建立初始映射；可编辑少量模型专属校准/override、碰撞体尺度和肩肘可达参数。
- **运行层**：现有角度 → Grip Anchor → 两手目标 → IK / 成对 ContactGrip → 人物骨骼。核心求解器不依赖固定资源名，也不直接把另一个模型的 `localRotation` 当成通用姿态。
- **真实性门槛**：换模型后不能改 C# 常量并重新编译才能使用；至少两套真正不同的 Humanoid 骨架上验证；自动得到合理初态后允许少量可记录的校准，不声称任意模型免调整。
- **简单优先**：如直接使用 `HumanPoseHandler` 的手指肌肉并配少量模型 profile 就足够，不需要制造庞大的通用 IK/人体形态优化框架。
