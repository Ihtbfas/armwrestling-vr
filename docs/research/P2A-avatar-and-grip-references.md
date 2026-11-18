# P2-A 调研记录：正常人物模型与成对扳手腕握姿

> 2026-11-18 调研阶段记录。下述是**外部资料已确认的事实与候选方案**，并非已经在本工程中实测通过。执行时请按仓库 AGENTS.md 再核对具体来源和兼容性。

## 研究问题

P1-E 从 Ch03 中裁出玩家右臂；对手显示纯白人体，双方看起来像抓手腕而非掌心扣握。此前法线夹角、拇指关节距离和稀疏网格穿入代理改善，但**视觉握姿仍不对**。解决方向应从“手指角度微调”转为**人物模型质量 + 成对手腕姿态/手指握姿的人工设计**。

## 人物资源候选

### 首选实验：Microsoft Rocketbox Avatar Library

- 仓库：https://github.com/microsoft/Microsoft-Rocketbox
- 官方 README：https://github.com/microsoft/Microsoft-Rocketbox/blob/master/README.md
- 明确的 [MIT License](https://github.com/microsoft/Microsoft-Rocketbox/blob/master/LICENSE.md)，2020 年起人物库以 MIT 开放；复制较大部分时保留原版权与许可证。仓库 2026-10-02 被归档为只读，但仍可阅读与获取。调查基准 commit：`0943055db6ec570bcef9f2c8b41c9e5467c808f9`。
- 官方有 **115 个带骨骼人物**、有贴图和服装，`Assets/Avatars/Adults/Male_Adult_01/Export/` 有 FBX，`Textures/` 有贴图；`Assets/Editor/FixRocketboxMaxImport.cs` 提供导入/材质问题处理。
- **风险非常具体**：官方导入脚本的 `OnPostprocessModel()` 末尾将 `importer.animationType` 设为 `ModelImporterAnimationType.Generic`。本工程需要 Humanoid/完整手指映射，**不要直接把这个全局 AssetPostprocessor 原封不动放入工程，避免改坏原 Ch03 导入**。如果选择 Rocketbox，针对少数选中 FBX 单独设置 Humanoid、修材质/骨骼，先验证 `Animator.isHuman`、Avatar 有效、右手 15 个 HumanBodyBones 指骨、蒙皮/手指形变后再继续。
- 资产量：单个人物可有 7 张约 12～16 MB 的 TGA，**绝不克隆整个多 GB 素材库或者批量引入 115 人**。只获取 1～2 名候选人物及其依赖，必要时在清楚保留 MIT 归属的前提下转换为合理尺寸的 PNG/纹理，并报告模型 + 纹理的总体积/运行性能。文件上传以确实用到、有许可且体积可控为前提。
- 官方支持 Unity 人物导入，但 **15 指骨是否能正确绑定现有握姿和 IK、具体材质效果、是否适合 PICO，必须在当前 Unity 版本实际验证**，不能凭 README 预先宣布成功。

### 替代候选：MakeHuman Community

- 官方主页：https://www.makehumancommunity.org/
- 许可：https://www.makehumancommunity.org/makehuman/faq/are_makehuman_files_free.html
- 核心资产是 **CC0**，导出的人体可复用；额外下载的衣服/贴图需分别审核许可。
- 适合自制较真实的人体及第一人称手臂；实际肤色、衣物、FBX/骨架导入、Humanoid 手指映射需验证。若 Rocketbox 手骨/贴图不适合，可在合理范围内试这个备选，不无限转换人物库。

### 当前基线：Ch03

- 现有 `P1-E` 功能可恢复验证；Ch03 与衍生玩家裁切网格目前来源不明确，不得直接上传 Public GitHub。
- 用户已上传的四宫格画面显示白色对手和橙色玩家两只手的扣握关系不符合真实扳手腕；实际画面要作为优先验收标准，而非继续用法线夹角掩盖问题。

## 可参考的成熟手势与动画思路

- **Meta HandGrabPose** 官方概念：https://developers.meta.com/vr/documentation/unity/unity-isdk-hand-grab-interaction/ 。设计思想是：针对一个被握物体，预存**手腕相对物体的位姿 + 手指局部姿态**；不是单纯给手掌一个共享点。这里只借鉴方法，不引入整套面向 Meta 的 SDK。
- **Blender Pose Library** 官方文档：https://docs.blender.org/manual/zh-hans/4.5/animation/armatures/posing/editing/pose_library.html 。可以直接为两套手骨骼摆姿态、保存姿态资产，必要时再导出 Unity 能读取的片段/局部骨骼姿态。
- **Unity Animation Rigging 1.2.1** 与 Unity 2022.3 兼容：https://docs.unity.cn/Manual/com.unity.animation.rigging.html 。可作为控制肩/肘/腕的候选，但目前有能正常运行的 Animator IK，不必为技术名称而更换。
- **真实握姿参考**：两名选手以右手掌面对掌，拇指/四指包握而不是抓对方手腕；中立初始腕相对伸直、肘支撑在垫上。参考：https://www.tawf.ca/league/learn/arm-wrestling-rules-and-championships 。可用真实比赛的摄影资料辅助摆姿态，**照片只作参考，不复制进公共仓库，除非有允许的授权**。

## 初步推荐：先一个静态正确的握姿，再给它运动

1. 在中立零位，让两只可控手骨骼**面对面，人工设计一套相互配合的腕目标和 15 个指骨姿态**，保存可重复调用的 *paired grip pose*。先由人眼核对拇指、四指、掌面、腕与前臂，没有正确画面之前不要开始大范围轨迹扫角。
2. 将一套成对握姿存储在共同 `Grip Anchor` 的局部坐标系中：玩家和对手分别有腕部目标位姿及各自的指骨旋转。实时机械角度只负责整体握持框架运动；已有身体动画 + IK 负责调整两人的肩/肘/腕到该框架。
3. 先检查零位正/侧/第一人称与手部近景是否像在正常扳手腕，再扩到 `−30°/0°/+30°` 与往返，必要时给端点小的修正，而非另造三套互不相干的手势。
4. 留两个对照：旧 Ch03 + P1-E 和新人物 + 成对握姿。**不要把旧方案直接删掉，也不要把只导入模型就称为动作成功。**

## 调研尚不能证明的事项

- Rocketbox 导入当前 Unity 2022.3.62f3c1 后是否完全拥有所需 Humanoid 手骨骼映射、是否能在 PICO 端高性能显示，需要实测。
- 静态握姿是否能在 ±30° 全程保持自然、是否能完全消除两套皮肤网格穿模，需要实际 Editor 画面与测试。
- **真实机械坐标、头显对齐、PC UDP 仍未标定/联调**；本阶段不会因此阻塞视觉演示。
