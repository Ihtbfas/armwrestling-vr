# P2-F 调研：更多真实人物与场景，不重做动画系统

> 2026-11-22 调研和 GitHub 实码核对。下列人物/场景是**候选与建议**，不是已经导入本项目并通过 Editor 验证。PICO 设备在同学处，本轮仅要求真实 Windows Unity Editor 验证。

## 一、目前的实际情况（由本仓库确认）

- **当前条纹 Polo 衫男角色来源**：Microsoft Rocketbox `Male_Adult_01`。已从微软官方仓库固定 commit `0943055db6ec570bcef9f2c8b41c9e5467c808f9` 导入 `Assets/ArmWrestling/Resources/P2A/Male_Adult_01.fbx`、`m002_*_color` 贴图、材质及 MIT 许可；P2-A 报告 `docs/P2A-avatar-and-paired-grip-result.md` 有完整记录。原路径：https://github.com/microsoft/Microsoft-Rocketbox/tree/0943055db6ec570bcef9f2c8b41c9e5467c808f9/Assets/Avatars/Adults/Male_Adult_01
- 其他卡通/旧人物可能来自本地原交付 `Ch03`，该模型**不在公开仓库，授权也尚未确认**。无法从公开资源确认它就是用户所说的卡通女性；Codex 可在本机查看实际资源和角色绑定后再判断。
- 本项目已经实现 `GripAvatarProfile`、`HumanoidGripBuilder`、15 指骨映射、Ready/Contest 与 P2-D 默认 `LegacyHumanoidGrip`。新增外观应**沿用现有 Humanoid/Animator/IK/Grip Anchor 和 Profile**，不能每来一个人物就复刻控制器。
- `Assets/ArmWrestling/Scripts/Core/Enums.cs` 的 `OpponentRole`：`Balanced=1 / Defensive=2 / Rhythmic=3`。`HudPresenter` 界面显示“均衡型／防守型／节奏型”。**这是对抗策略，不是外观角色**；`ArmWrestlingApp` 通过 role 发布交互事件与 character_id，未来 PC/UDP 会使用。外观/场景选择必须独立，不要改动此枚举数字、通信协议、力矩语义，不能由性别或年龄隐含决定难度。
- 当前场景由 `ArenaFactory.BuildEnvironment` 程序生成：默认**黑暗霓虹竞技场**（10×12m，圆环光带/立柱/条幅）；选用本机未获公开许可 HDRI 才进入另一条开放场地路径。**目前不具有供用户在游戏内切换的场景目录**。

## 二、可以直接复用的人物候选

**微软 Microsoft Rocketbox（MIT）原仓库**：https://github.com/microsoft/Microsoft-Rocketbox ，包含 115 个有骨骼的人物，`Assets/Avatars/Adults` 中 **17 个 `Female_Adult_01`～`17`、21 个 `Male_Adult_01`～`21`**，另有职业人物。官方下载的 TGA 贴图单张通常约 12～16MB，**不要整库下载或全部上传**。优先只选一个 FBX + 实际需要的 1K/512 压缩色图和透明贴图，保留 Microsoft MIT 版权。
- **普通成年女性（首选）**：`Female_Adult_01` 粉色衬衫 + 牛仔裤，预览与 FBX：https://github.com/microsoft/Microsoft-Rocketbox/tree/0943055db6ec570bcef9f2c8b41c9e5467c808f9/Assets/Avatars/Adults/Female_Adult_01 ；`Export/Female_Adult_01.fbx` ≈ 609 KB。预览人物确实是正常皮肤/服装风格。
- **运动风格女性（建议加入）**：`Sports_Female_02` 灰色背心 + 深色长裤，不是运动内衣款（`Sports_Female_01` 为泳装，不适合当前普通训练场景）。https://github.com/microsoft/Microsoft-Rocketbox/tree/0943055db6ec570bcef9f2c8b41c9e5467c808f9/Assets/Avatars/Professions/Sports_Female_02 ；`Export/Sports_Female_02.fbx` ≈ 601 KB。
- 两份 FBX 是否确实有 15 个有效右指骨、衣物材质是否适合当前 Demo、实际坐姿和肘垫碰撞必须在 Unity `2022.3.62f3c1` 中验证。现有 P2-A `RocketboxAssets.Prepare` 只适配 `Male_Adult_01` 的骨骼/材质，不应全局照搬官方 `FixRocketboxMaxImport.cs` 的 Generic/全项目后处理设置；新模型应做**按素材路径的统一导入器**，优先复用现有映射函数。
- **明确的年长人物**：本仓库 Rocketbox `Adults` / `Professions` 下没有名为 Elderly/Senior 的独立人物类别。不能因为某人留短发/光头就称为老年人。优先用 **MakeHuman 或 Blender MPFB** 的 Age slider、老年人物预设做一个真正具有年长外貌的男/女角色，并绑定完整 Humanoid 手部：  
  https://www.makehumancommunity.org/  
  https://static.makehumancommunity.org/oldsite/documentation/the_sliders.html  
  https://static.makehumancommunity.org/mpfb/docs/characters/loading.html  
  其**核心人体资产为 CC0**：https://www.makehumancommunity.org/about/license.html ，但第三方衣服/头发/贴图要逐项核对，不能默认全是 CC0。目标是自然的肤色、头发、衣服与年龄特征，**禁止仅将现有年轻脸部贴图灰化并声称老年人已完成**。若本机没有工具或公开许可衣服、且短时间内无法得到可用的可绑手老人，保留导入/试验结果，明确“老年人物未完成”，不拖垮已经成功的女性与场景。

## 三、可以借鉴的场景素材和设计

- **保留**当前暗色科技竞技场作为默认，可回退。
- **新增训练馆**：日间/明亮室内，运动地面、普通墙面/窗户、器材轮廓和自然灯光，视觉上应明显不同于暗色竞技场，尽量复用现有 `ArenaFactory` 地面/桌椅和简单道具；可参考 Poly Haven CC0 `Gym 01` HDRI https://polyhaven.com/a/gym_01 ，如下载只用 1K/2K 和必要纹理。HDRI 只能提供背景/环境照明，不是现实可碰撞几何，需搭配真正场景物体。
- **新增比赛场馆**：标准比赛台配色、比分牌或赛事标识、聚光灯/简化观众席等；以现有几何程序或小型 CC0 资源实现，**不能只给原霓虹大厅换个颜色就称为新场景**。不要下载无授权舞台/商用素材。
- 公共 CC0 光照与模型资源 https://polyhaven.com/ ，明确允许使用和分发。适配未来 PICO：不要为一个背景引入几十 MB 的高清贴图/多盏实时阴影或一次性激活多套场景。

## 四、架构与演示关键点

- **UI 分离三个概念：** `Opponent Appearance`（男/女/运动女/年长人物），`Arena Theme`（科技竞技场/训练馆/比赛场馆），`OpponentRole`（原均衡/防守/节奏）。这些维度应相互独立；选择不同人物不应悄悄改变对手力矩或角色编号。
- 使用`GripAvatarProfile`作为人物 Prefab、缩放、支撑与骨骼适配的入口；无需改写 `Grip Anchor`。独立设置外观与环境，保留 P2-D 默认和 `Use Authored Grip` 可选，按组合测试腕/肘可达和手指碰撞。不同手型允许小量 profile/美术 override，但不能仅因 15 指骨映射成功就宣布自然握持。
- 预览选择应在原 Lobby 可见且用**鼠标真实点击**，不是只能在 Inspector 编辑。场景与人物切换至少在**下一局准备前**生效，避免比赛中销毁人物导致 IK/输入异常。尽量提供简单缩略图或名称，不为此重构整个 UI。
- 尽量只使当前选中的 1 个对手及 1 个环境活动，合理控制场景材质、贴图尺寸、draw calls。保证 Unity Windows Editor Play 与公开 clone 可复现。未来 PICO 未实测，不要声称头显帧率已通过。
