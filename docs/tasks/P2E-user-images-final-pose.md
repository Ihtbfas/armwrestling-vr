# P2-E 定姿续作：使用用户提供的两张四视角参考图

> **待 Codex 直接实施。** 在现有 `feat/p2d-cross-avatar-grip` 上继续更新 [Draft PR #11](https://github.com/Ihtbfas/armwrestling-vr/pull/11)，上游 PR 未合并；不要新建串联 PR。本轮任务是**真正把用户新提供的参考图片转为可运行的自然双手握姿**，不是再次写参考资料或角度搜索器。

## 首先读取本地两张原图（不在 Git 中）

用户从 ChatGPT 下载的 `P2E_grip_reference_for_Codex.zip` 应解压到仓库根目录。启动之前确认以下文件存在，打开并认真观察像素内容：

- `.p0-local/grip-reference/01_hand_grip_four_views.png` —— **手部细节主参考**：上方、侧面、选手 A、选手 B 视角，研究虎口、两拇指、掌根和四指指腹的相对层次；
- `.p0-local/grip-reference/02_body_posture_four_views.png` —— **整体姿态主参考**：上方、侧面、选手 A、选手 B，研究双腕顺直、肘垫支撑、手臂-躯干关系和共同握点。
- `.p0-local/grip-reference/README_参考说明.md` —— 参考的边界与素材 SHA-256。

**提醒：** 两张图片的象限有不同人物、背景和位置，**不能假设这些角度必定是同一次握持的同步实拍**；也不把它们当作可直接还原三维关节角的标定数据。选定一个合理、全视角一致的**右手对右手、无绑带握姿**，用多视角交叉确认。若本地图片未到位，不要虚称已经看图，也不要把图偷偷上传 Public GitHub；先通过用户对 Codex 的附件或上述本地路径取得原图。

## 现有版本与目标

- 以最新 P2-E 真人参照尝试 [`a2b670f`](https://github.com/Ihtbfas/armwrestling-vr/commit/a2b670fc2640231ac30acb1f57d633d23fc25a5f) 为起点。`ReferencePairedGrip.asset` 与上一版 `AuthoredPairedGrip.asset` 均保存了双方 Ready/Contest 端点，`扳手腕 → P2-E → Open Authored Grip Preview` 可视化编辑手骨，并在 `ArmWrestlingMain` 的 `Use Authored Grip` 中选择。
- 目前 P2-D `LegacyHumanoidGrip` **继续是默认**。Rocketbox 参考姿态相比上一轮美术稿手部代理峰值 9.61→5.90 mm、反侧包握略改进，但拇指虎口遮挡与指腹未贴；本机 Ch03 从 10.26→15.37 mm 有回退，肘部局部穿垫约 12–16 mm。因此不能把“代码正常运行”当作视觉通过。
- P2-D 已实现 Rocketbox / 本机 Ch03 的 Humanoid Profile 跨模型选择与混合人物，原 Animator、IK、Grip Anchor 和 ContactGrip 均保留。**这轮不改 PC 控制、UDP、STM32，不进行真实机械或头显联调。**

## 一次性执行，真正使用图片

1. **先从图片明确三维接触关系**。记录哪只手掌压在哪一面、两拇指分别经过哪个虎口、四指沿什么方向包到对方手背、各自指腹落在什么位置。选择一套具有合理手腕位置、握掌相对和可解释的拇指层次的姿态。若两图角度相互矛盾，优先真实人体几何与可见接触，不生搬硬套。
2. **在实际 Unity 双人手骨编辑器里修改**。打开两只真实 Rocketbox 手，先摆两腕/掌根与虎口，再单独摆拇指、食/中/无名/小指 MCP/PIP/DIP。尽量使用当前 Scene View 关节旋转手柄保存实际骨骼快照；可按需改进编辑器相机或手势预览，但**不要只重新写 `PoseDigits()` 固定弯指角度表、改变接触阈值或做批量 muscle 搜索**。
3. **Ready 轻握、Contest 扣握都要自然**。先获得一对零位 Contest，反侧、顶视和第一人称同时通过视觉检查，避免十指交叉、抓住对方腕、某个拇指穿掌。再从已经扣住的状态制作 Ready 的轻握差异，不要准备阶段仍大张手指。保存、退出并重新打开工具验证资源确实有效。
4. **继续跨人物适配**。Rocketbox 是授权明确的美术源。Ch03 使用其 Humanoid/Avatar Profile、必要的本机手型/拇指/指腹覆盖人工校准；不能把 Rocketbox 的原始 `localRotation` 生搬过去，也不能修改共享控制器来针对 Ch03 写 if/else。Ch03 肘垫穿入是独立身体可达/模型几何问题，记录但不要让它遮蔽本轮手部目标。
5. **零位明显更合理时同轮集成主场景**，保持机械角度驱动的共同 Grip Anchor；随后用 `−30°/0°/+30°` 和 181 帧六秒往返验证，回归 Rocketbox/Ch03/混合、当前稳定版、胶囊及缺资产回退。检查腕折角、指节互穿、指腹接触、肘/胸关系、Ready→Contest 过渡、不抖动、不脱手；不能为视觉紧握拉伸骨长或破坏原机械握点。
6. **交付可外部审阅的真实证据**：零位 Before/After 顶视、两侧、反侧和第一人称 PNG；三角度拼图与一段实际 Unity 六秒 GIF。公开图片只能包含许可允许的 Rocketbox 与原创场景，不能包含这两张来源未核实的参考图、Ch03 或私有裁切网格。放在 `docs/validation/p2e-user-reference/`，更新 `docs/P2E-user-reference-result.md`、TASKS 和 PR #11，并记录新旧差异及局限。

## 成功与停止

**成功必须是可见的双手正常扣握，并且腕/肘/接触没有重要倒退。** 可以在这个条件成立时由 Codex 自主将新美术姿态设为默认；否则保留稳定 P2-D 默认、保存最好的可编辑候选并说明“哪个拇指、哪根指节、哪块皮肤仍未贴合”，不要再无限搜角。对于因参考图遮挡而确实无法辨认的空间关系，明确记录并采用可验证的自然握姿折中。

**本轮完整授权：参照原图 → 直接编辑/保存骨骼 → 接入 `ArmWrestlingMain` → 实际 Editor Play → PNG/GIF → 推送同一 Draft PR #11。不要仅交付研究报告或仅创建新的工具。**