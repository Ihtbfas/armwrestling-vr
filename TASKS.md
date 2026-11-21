# TASKS

## P2 阶段收口

- [x] 将 PR #8～#11 的原始提交整合到 `main`，冻结 P2-D 默认与 P2-E 可选美术稿。
- [x] 核对人物许可、场景 GUID 与本机私有文件，回归公开人物、私有人物及回退。
- [ ] PICO 实机首轮验收。先走离线比赛，记录第一人称尺度、遮挡、输入与时序。Ch03 肘垫偏差另做实物与人物几何标定。

[运行方式、验证范围与遗留问题](docs/P2-closeout.md)

## P2-E 用户四视角握姿（已接入可选模式）

- [x] 打开两张四视角附件，在 Unity 双手骨骼预览里编辑双腕、拇指与四指，分别保存并重载 Ready / Contest。
- [x] 接入主场景可选握姿，在 Rocketbox、Ch03 和混合人物上测试拇指重定向及 ±30° 往返。
- [x] 以实际 Editor Play 录制同镜头对比和比赛回归，公开 Rocketbox 画面。
- [x] 局部收尾：修正 Rocketbox 对手拇指末端与玩家食指根接触；Ch03 按对方掌型分别校准中指。[运行实测与剩余交叠](docs/P2E-contact-finish-result.md)
- [ ] 自然扣握验收。第一人称四指包握改善；拇指与指根仍有相交，保留稳定默认。

[本轮画廊](docs/validation/p2e-user-reference/README.md) · [运行结果](docs/P2E-user-reference-result.md) · [任务书](docs/tasks/P2E-user-images-final-pose.md) · [上轮结果](docs/P2E-real-grip-result.md)

## P2-E 续作｜真实握法参照与骨骼定姿（已接入可选模式）

- [x] 查阅 CAWF/AWE 规则、比赛照片与教学视频预览帧，明确掌面、拇指和四指的接触对应。
- [x] 在 Unity 的双手预览中直接摆腕和指骨，保存、重载 Ready 轻握与 Contest 扣握。
- [x] 同轮接入主场景。Rocketbox、Ch03 和混合角色扫角，并回归比赛与回退。
- [x] 保存公开的同机位 PNG / GIF 与技术结果。Ch03 画面只留本机。
- [ ] 自然握持及默认切换。Rocketbox 优于上一稿；Ch03 拇指和指节仍有交叠，保留稳定默认。

[本轮实测结果](docs/P2E-real-grip-result.md) · [执行任务书](docs/tasks/P2E-reference-guided-paired-pose.md) · [真实握法/姿态工具参考](docs/research/P2E-real-armwrestling-grip-reference.md)

## P2-E 双人美术握姿制作与主场景集成（已保存并接入可选模式）

- [x] 保存两侧可重新编辑的 Ready 与 Contest 骨骼姿态及 Grip Anchor 相对腕姿。
- [x] 用零位多视角及 ±30° 往返比较旧默认和美术端点，在 Rocketbox、Ch03 与混合角色上实际运行。
- [x] 交付主场景切换、Editor 验证和可公开的 PNG/GIF。保留稳定默认及以往模式。
- [ ] 自然扣握验收。零位拇指与部分手指还有空隙和相交，Ch03 肘部穿垫仍需解决。

[本轮实际结果](docs/P2E-authored-grip-result.md) · [完整执行任务](docs/tasks/P2E-authored-paired-grip.md) · [已有成熟工具和路线](docs/research/P2E-authored-hand-pose-workflow.md)

## P2-D 跨人物通用握姿与自然接触（有界实验收口；默认稳妥回退，自然扣握未通过）

- [x] 从 P2-C Rocketbox 专属 wrist/finger localRotation 中拆出可在不同 Humanoid 上表达的握姿与接触语义。
- [x] 建立简洁可编辑的 Avatar 适配配置；更换模型不需要改核心 C#，允许手型差异引起的小量配置/姿态覆盖。
- [x] 用**第二套不同骨架**真实测试（本地 Ch03 或许可明确且不同骨骼的模型），不能仅用 Rocketbox 缩放代替。
- [ ] 自然扣握视觉验收：零位根部先接触及分关节限位已实现，但仍松握、指节交错；不能以约 1 mm 凸包指标代替画面。
- [x] 同一轮从小验证直接集成，完成两个骨架在零位、±30° 和实际比赛的 Editor Play，以及合规 PNG/GIF 可查看证据。

- [x] 完成一次整掌/关节弧/腕姿/碰撞预算的有界候选验证；未达自然验收，按停止条件恢复稳妥通用默认，保留实验和跨模型适配。

[本轮收口结果](docs/P2D-joint-constraints-result.md) · [真实 PNG/GIF](docs/validation/p2d-joint-constraints/README.md)

[执行任务书](docs/tasks/P2D-cross-avatar-paired-grip.md) · [调研技术参考](docs/research/P2D-cross-avatar-grip-references.md) · [P2-C 画廊基线](docs/validation/p2c-visual/README.md)

- [x] PR #11 修订：新增实际蒙皮表面的可视接触标记、双端点生成和主场景接入；保留首轮模式并以修正后的测量工具重录对照。

[最新接触目标修订](docs/P2D-contact-targets-revision.md) · [新 PNG/GIF](docs/validation/p2d-contact-targets/README.md) · [实际结果与限制](docs/P2D-cross-avatar-grip-result.md) · [公开 PNG/GIF](docs/validation/p2d-visual/README.md)。Ch03 画面只留本机；Rocketbox 负角端胸臂及两模型肘垫表面仍待改善。

## 当前阶段：P2-C 动画与物理接触一轮集成（已集成主场景）

- [x] 从 Unity 官方 XRI、Auto Hand/Hurricane VR、Adaptive Hands 等成熟思路中筛选兼容方案并做最小验证。
- [x] 一旦找到实际可行的接触限制/物理响应方法，**同一轮直接接入 ArmWrestlingMain、调试并回归**，不要止步于孤立原型。
- [x] 优先改善双手指节/掌面与上臂—躯干的明显穿模，不破坏原单轴握点、预制动画、肘部支撑与人体自然性。
- [x] 同机位记录 0°、±30° 和往返真实画面、碰撞/物理响应数据及负面代价，保留 P2-A/P2-B 诊断与胶囊回退。
- [x] 交付可切换模式、结果文档、Editor Play 回归和新 PR；不要求对每个小决策再授权。

[完整任务](docs/tasks/P2C-physics-animation-integrated-implementation.md) · [现成方案参考](docs/research/P2C-physical-interaction-patterns.md)

- [ ] 自然扣握验收：虎口、拇指根部和部分指节交叠仍待修正，不能将集成完成等同于视觉完成。
- [x] 公开合规视觉证据：零位和三角度 PNG、双手及身体正侧视 GIF 已整理到 [P2-C 画廊](docs/validation/p2c-visual/README.md)，保留 MIT 归属及原始本机记录。后续视觉修改按 AGENTS 同步更新可查看证据。

[本轮结果与复现](docs/P2C-physical-interaction-result.md)。本 PR 修订已使用可编辑端点、同步闭合和坐姿适配，胸臂相交减轻；虎口仍有交叠、手部旧代理峰值有回退。自然扣握仍待完善，保留 LegacyContactGrip 及原模式对照。

## 当前阶段：P2-B 碰撞感知人物动画（已完成 Editor 实验）

- [x] 验证 Rocketbox 的手—手、上臂—胸部及肘垫穿插，建立合理尺寸的骨骼碰撞代理和 Editor 可见诊断。
- [x] 先在零位复核碰撞与真实画面，再对 1～2 处明显穿模尝试有限骨骼/身体姿态修正。
- [x] 不改变共同 Grip Anchor、既有预制动画/IK 主流程；保留不带碰撞修正的 P2-A 回退。
- [x] 同机位对比 0°、±30° 与往返视频、碰撞诊断和肘/腕/掌心回退；实际 Editor Play 回归。
- [x] 提交简明实验结论与真实证据，不宣称已经实现完整人体物理仿真或全网格无穿模。

[任务书](docs/tasks/P2B-collision-aware-animation.md) · [参考与调研](docs/research/P2B-unity-collision-aware-animation.md) · [实验结果与本机证据](docs/P2B-collision-experiment-result.md)

零位的右上臂与胸部交叠减轻，局部指节有改善。极端角和手指仍有交叠，保留可切换的 P2-A 原姿态。

## 当前阶段：P2-A 正常人物与成对扳手腕握姿（已完成 Editor 原型）

- [x] 先调研并试用 MIT Rocketbox、CC0 MakeHuman 等真实人物，验证贴图、Humanoid 与全部手指骨骼。
- [x] 保留 P1-E 基线和胶囊回退，完成一名外观正常的对手与第一人称玩家手臂。
- [x] 人工设计一个正确的**成对双手掌面对掌**握姿，保存相对共同 Grip Anchor 的两个腕目标和手指姿态。
- [x] 用实际 Editor 零位近景与连续 ±30° 往返验证姿态自然性，保持肘部稳定和比赛流程。
- [x] 留下研究来源、资产许可与真正的画面证据；不要求一轮上线 PICO/PC UDP。

[执行任务书](docs/tasks/P2A-avatar-paired-grip-prototype.md) · [调研与技术参考](docs/research/P2A-avatar-and-grip-references.md) · [运行与证据](docs/P2A-avatar-and-paired-grip-result.md)

Rocketbox 和配对姿态已跑通。近景仍有局部穿插，端点手腕和肘垫接触留待实际视角与实物标定复核。

## P1 阶段收口

- [x] 将 P0～P1-E 原提交快进整合到 `main`，保留阶段分支、任务文档和 [收口记录](https://github.com/Ihtbfas/armwrestling-vr/issues/7)。
- [x] 在功能基线 `e1a3ad9` 上复验默认胶囊和本地 P1-E 主场景 Editor Play。完整人物的依赖、恢复和回退见 [README](README.md)。

## 当前阶段：P1-E 双方握持接触与玩家手臂显示（已完成 Editor 原型）

- [x] 在本机人物模式中使双方手掌朝向、拇指与四指包握更自然。
- [x] 对照 P1-D 同角度的第一人称、正侧视和连续往返，改善明显穿模，保持共享 grip 跟随与肘部稳定。
- [x] 尽量改善玩家裁切上臂的可见边缘；原 Ch03 及裁切素材仍受公开授权限制。
- [x] 保留 P1-D 预制动画、胶囊回退和离线比赛流程；不扩展到 PC/STM32/PICO 联调。
- [x] 提交真实 Unity Editor 验证结果、可复现步骤与 SHA。

[P1-E 任务书](docs/tasks/P1E-two-hand-grip-and-player-finish.md)及[运行与对照](docs/P1E-hand-contact-result.md)。已改善双手对掌、拇指靠拢和上臂边缘；部分角度仍有手部局部重叠。

## 当前阶段：P1-D 玩家人体手臂、对手肘部稳定与分层动画（已完成 Editor 原型）

- [x] 在第一人称视角中显示正常的人体玩家手掌/前臂，替代默认球形手的最终观感（保留回退基线）。
- [x] 让 Ch03 右肘在垫面附近相对稳定，比较新旧水平移动范围和握持精度。
- [x] 尝试至少一个真实的可回放骨骼动画片段，并与角度/IK 组合；如遇客观阻断说明原因。
- [x] 观察双方手掌握持、穿模与关键角度连续性，保留 P0～P1-C 主场景回归。
- [x] 提交真实 Editor 画面证据、代码、SHA 和后续建议，不扩展到实物/头显联调。

完整任务：[P1-D 角色动作提升](docs/tasks/P1D-player-avatar-elbow-animation.md)。见 [P1-D 运行与对照](docs/P1D-character-motion-result.md)。±30° 肘骨水平范围减少约 82%，对手掌心代理误差保持 3.31 mm。下一轮优先处理两手掌面、手指接触和模型授权。

## 当前阶段：P1-C 实物测量交接与局部手部动作预研（已完成）

- [x] 明确当前 Unity 演示几何与 PC 仿真/CAD/实际测量的来源差异。
- [x] 形成可直接填写的实物轴线、握把圆弧、座椅/肘垫位置与机械零位测量指南。
- [x] 如果 Ch03 骨骼条件允许，尝试简单的手指握姿，保持现有掌心 IK 和 P1-B Demo。
- [x] 若无实物数据，不伪称完成真实空间标定，不无限优化演示垫。
- [x] 交付必要文档、真实 Editor 证据（如修改动画）与下一步最小输入清单。

已交付 [现场测量指南与待填表](docs/calibration/physical-measurement-guide.md)及[手部实测结果](docs/P1C-geometry-and-hand-result.md)。尚未实物标定。Ch03 可选握姿减小了指尖采样穿入，掌面重叠仍待解决。

## 当前阶段：P1-B 肘垫可达性与接触优化（已完成演示标定）

- [x] 以 Ch03 实际骨骼、缩放和 grip 轨迹判断肘垫接触的可行范围。
- [x] 沿用 P1-A 方案 B 的姿态插值与手脚 IK，优先改进肘部位置与坐姿。
- [x] 在相同角度与往返轨迹对照 P1-A，区分掌心代理/肘骨原点和实际接触。
- [x] 保持 P0 的离线比赛流程；提交真实 Editor 证据或说明客观不可达原因。

±30° 内肘部采样下缘距垫面约 0.6–3.7 mm，掌心代理误差约 3.31 mm。±42° 负端仍有约 0.7 mm 采样穿入。见 [实测与复现](docs/P1B-elbow-contact-result.md)及 [任务书](docs/tasks/P1B-elbow-reachability-and-contact.md)。

## 当前尝试：P1-A 人物动画路线对照（已完成 Editor 实验）

- [x] 检查并修复 Ch03 的 Animator IK 运行基线，确认坐姿、手/肘与握持目标。
- [x] 尝试 3 个代表姿态的角度混合 + 必要的手部 IK，并与已有方案使用同样的输入比较。
- [x] 记录三个角度及连续往返动画的实际 Editor 结果，选择后续路线，不扩展成全身物理仿真。
- [x] 保持 P0 主场景及比赛流程可用；本轮不处理 UDP/PC/STM32。

推荐 B 的身体姿态叠加 A 的接触 IK。正角端肘垫接触、手掌闭合和实物标定仍待解决。详见 [P1-A 任务](docs/tasks/P1-animation-ab-comparison.md)及 [验证结果](docs/P1-animation-ab-result.md)。

## 阶段记录：P0 已完成本轮接管与主场景离线运行（2026-11-18）

- [x] 识别 ZIP 工程入口 `banshouwan/test1`、编辑器 `2022.3.62f3c1`、两个场景及依赖。
- [x] 原 ZIP 未覆盖；保存 SHA-256、167 个源文件散列及原 GUID，保留本机只读使用的基线副本。
- [x] 导入项目源码、场景、材质、配置；三类待核权资源和 `.meta` 留本地，提供精确恢复脚本。
- [x] PICO 绝对路径改为官方固定 Git 提交；默认离线模拟；Standalone 自动 XR 初始化关闭。
- [x] 首次导入/编译通过，两个原场景静态脚本检查通过；主场景实际 Editor Play 流程与渲染验证通过。
- [x] 盘点人物、手臂、动画、角色/UI、角度管线、XR 和通信；明确代码存在与实测覆盖的区别。
- [x] 更新运行说明、许可清单、交接记录、验证工具，交付工作分支供审查；不合并 main。

已知限制与详细证据见 [P0 交接](docs/P0-handover-result.md)。预览场景仅适合静态摆位，角色按钮 Play 未通过；真实输入、正式 UDP、PCVR 与 PICO 仍未验证。

## 下一轮优先事项

- [ ] **实物标定。** 按[测量指南](docs/calibration/physical-measurement-guide.md)采集零位、限位和握点数据，核对机械负角度与我方获胜方向。
- [ ] **UDP 联调。** 核对 `opponent_01..03` 与 PC 的 `person_01..03` 映射及客户端 ushort 序号回绕，按现有 UDP V0.1 隔离联调。
- [ ] **可共享人物。** 明确人物、HDRI 和字体再分发许可，或选可公开替代。先在实际第一人称视角观察 P1-E，再判断握姿是否需要继续细调。
- [ ] **头显验证。** 分别验证 PCVR 和 PICO 的运行、视点、射线点选、UI 可读性和性能。

PC 阻力参数、STM32、串口和硬件保护仍归独立 `armwrestling` 仓库；本轮没有修改。
