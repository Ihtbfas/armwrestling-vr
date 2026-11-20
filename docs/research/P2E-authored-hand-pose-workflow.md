# P2-E 调研｜美术定姿先行，保留跨 Humanoid 模型能力

当前 P2-D 在 Rocketbox/本机 Ch03 两套不同骨架上完成了通用握姿配置与运行回归，但“自然扳手腕扣握”一直未过视觉验收。最后一次有界几何/关节弧搜索失败，已恢复 `LegacyHumanoidGrip` 稳妥默认。**因此不是继续改更多自动碰撞/接触求解器，而是改为直接制作可编辑的成对美术姿态资产。**

## 成熟工作流，优先按最小实现参考

1. [UltimateXR Hand Pose Editor（官方）](https://www.ultimatexr.io/guides/hand-pose-editor)
   - 真正编辑人物手指骨骼，存储可复用 `.asset`。支持 Fixed Pose 与从 Open 到 Closed 的 Blend Pose，Prefab 继承/模型专属 override，并允许从其他 Avatar 姿态导入为起点。
   - 其说明同时承认骨架差异会造成转移误差，**不意味着免人工校准**。
   - 此工程不需要移植整套 UltimateXR、URP 或其 XR 运行时。复用当前 `PairedContactPreview`、`ContactGripAuthoring` 和 Unity Inspector/Scene Handles 的思想即可。
2. [Blender 4.5 LTS Pose Library（官方）](https://docs.blender.org/manual/en/4.5/animation/armatures/posing/editing/pose_library.html)
   - 姿态资产是单帧骨骼动作；可在 Pose Mode 中编辑，保存、混合、局部应用，并保留可复用的原始源文件。
   - 如果 Windows 本机确有 Blender、能可靠导入现有骨架，允许用其 Pose Mode/姿态库辅助；但要确认导出的姿态与 Unity Humanoid 重定向/坐标系统一致。别为了尝试新工具而损坏 Unity 工程。
3. [Unity 2022.3 Avatar Muscle & Settings](https://docs.unity3d.com/2022.3/Documentation/Manual/MuscleDefinitions.html) 与 [HumanPoseHandler](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/HumanPoseHandler.html)
   - 将已做好的姿态转换为目标 Avatar 的肌肉值或骨骼局部旋转，并对 Ch03 独立校准。不要把 Rocketbox 的骨骼 `localRotation` 直接拷给另一套 Rig。
   - 应用姿态时确保不被 Animator/OnAnimatorIK 的后续回写覆盖，不要每帧 `SetHumanPose` 破坏躯干/比赛动画。

## 项目中现有可复用能力

- `PairedContactPreview.cs` 与 `PairedContactPreviewInspector.cs` 已能实例化两只真实蒙皮手、编辑七组双人接触标记、重建并预览。**目前的问题是目标求解，不是缺可预览人物的入口**。可以在此增加直接编辑、保存每指关节的固定握姿及准备姿态的能力；不能只再次调整 7 个接触点和运行搜索器然后称“美术定姿”。
- `ContactGripAuthoring.cs` 已能读写 Open/Closed 15 指局部旋转，但旧版本直接与特定 Rocketbox 骨架绑定。可借鉴其编辑/保存形式，并通过 P2-D 的 `HumanoidGripDefinition` / `GripAvatarProfile` 保留统一语义、人物 override。
- `ArmWrestlingMain` 当前稳妥基线：`Use Humanoid Grip = true`、`Use Grip Surface Targets = false`，见 `docs/P2D-joint-constraints-result.md`，对应 PR #11 最新提交 `1014b7d`。
- `docs/validation/p2d-joint-constraints/README.md` 与 `docs/validation/p2d-contact-targets/README.md` 有真实 Before/After、失败候选的直接可查看 PNG/GIF。新定姿要与这些实际画面对照。

## 任务优先级与“已完成”的含义

**第一：零位真实握姿**——两只右手掌心相对、虎口与拇指自然搭扣、四指绕住对方掌部/手背，而不是互相钩住手指、抓住手腕或者十指交叉。用至少正面、斜侧、反侧和玩家第一人称观察真正蒙皮形变。必须有实际保存的 Closed Pose 与准备/放松 Pose；关节不反折、不轴向扭曲、皮肤无大块穿插。

**第二：跨人物**——共享语义和姿态工作流；Rocketbox 可有具体美术姿态资产，Ch03 用 Humanoid 重定向加少量单独校准，代码控制器不分叉。不承诺任意 FBX 无需适配。

**第三：动态和接触**——定好稳定零位后才接入 Grip Anchor、现有 Animator+IK，保留 ContactGrip 作为残余指腹接触约束（可禁用用于对照）。动态时不能为了“贴合指腹”让腕弯折、肘穿垫、胸臂相交严重恶化。

不要把“生成了编辑器面板”“自动搜索获得参数”“Collider 代理变小”“比赛无报错”单独当作握姿完成。这次研发的核心是从**实际人体美术姿态**获得自然运动，再最小化利用 IK/物理。
