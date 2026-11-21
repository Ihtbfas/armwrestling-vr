# armwrestling-vr

单自由度力反馈扳手腕的正式 Unity/VR 工程。P0 已接管同学交付的 `banshouwan/test1`，保留原场景和代码路径，工程入口就是本仓库根目录。

`main` 的 P2 离线演示默认使用 Rocketbox 和 P2-D 稳定握姿 `LegacyHumanoidGrip`。在主场景 **Runtime Bootstrap → Use Authored Grip** 可试用 P2-E 的 `ImagePairedGrip` 准备与对抗姿态。两种模式都使用共同机械握点、Animator 与 IK。已在 Windows Editor 验证跨 Humanoid Profile 和比赛流程。双手局部穿模仍可见；本机 Ch03 另有肘垫几何偏差。[P2 收口、回归范围和 PICO 测试准备](docs/P2-closeout.md)

## 最简运行（Windows）

1. 用 **Unity 中国版编辑器 `2022.3.62f3c1`** 从 Hub 添加本仓库目录。本机需有 Git 命令行；首次导入需联网下载 Unity 包和固定版本 PICO SDK，本轮首次导入约数分钟。
2. 打开 **`Assets/Scenes/ArmWrestlingMain.unity`**，等待编译后点 **Play**。编辑状态下主场景只有 Runtime Bootstrap，运行时会搭出原有桌面、手臂、对手和 UI，属于原工程设计。
3. 在面板依次选择 **下一步：空间标定 → 跳过标定 → 角色 → 准备对战**。准备和倒计时后进入比赛。
4. **空格**切换自动演示；**A / ←** 我方发力，**D / →** 对手发力；**Esc** 暂停。也可使用面板上的暂停、继续和再来一局。

默认使用离线模拟，运行不需要 PC 控制程序、STM32 或头显。桌面自动 OpenXR 初始化默认关闭；PCVR 需自行在 Standalone XR 设置中启用并配置运行时，本轮未验证。

**胶囊回退**：关闭 Runtime Bootstrap 的 **Use Humanoid Grip**、**Use Paired Grip** 和 **Use Original Art**。原 Ch03 人物 FBX、HDRI、字体许可未确认，文件及其原 `.meta` 保留在本机、不上传；有原 ZIP 可按 [恢复说明](docs/third-party-assets.md)还原美术资源，并在关闭 **Use Humanoid Grip** 与 **Use Paired Grip** 后勾选 **Use Original Art** 查看原人物/HDRI。原人物的姿态和材质仍需调整，默认关闭此开关。Windows 内置动态字体回退已在本机检查，其他机器/Android 的中文字形仍需复核。

`ArmWrestlingPreview.unity` 保留作静态摆位参考；它的部分按钮回调未随场景保存，不能代替主场景做完整 Play 演示。不要为了首次运行执行“一键初始化”或重新生成全部场景。

## P1-A 人物动画对照

本地恢复 Ch03 后，可在主场景 Play 中使用 **Blend Body Poses** 观察固定躯干 Animator IK 与三档躯干姿态混合的差异。运行入口、量化结果和本机视频位置见 [P1-A 动画对照结果](docs/P1-animation-ab-result.md)。

## P1-B 肘垫接触

本地 Ch03 模式默认使用 B 姿态插值和调整后的演示对手肘垫。复现和前后测量见 [P1-B 接触结果](docs/P1B-elbow-contact-result.md)。

## P1-C 实物测量与手部预研

[实物测量指南](docs/calibration/physical-measurement-guide.md)列出现场步骤和可填写记录。恢复本地 Ch03 后，在 Runtime Bootstrap 勾选 **Use Original Art** 和 **Use Experimental Grip** 可试用弯指姿态。见[实验数据与复现](docs/P1C-geometry-and-hand-result.md)。

## P1-D 玩家手臂与对手动画

本机恢复 Ch03 后运行 **扳手腕 → P1-D → Prepare Local Player Arm**，再在主场景 Runtime Bootstrap 勾选 **Use Original Art** 和 **Use Character Motion**。可看到第一人称蒙皮手臂、对手限域肘部和准备／对抗动画混合。详见 [P1-D 运行、对照与本机证据](docs/P1D-character-motion-result.md)。

## P1-E 双手握持与玩家短袖

完整人物模式需要本机的 `Ch03.fbx` 和原 `.meta`，以及从 Ch03 生成的 `P1DLocalPlayerArm.asset`。HDRI 和字体为可选资源。仓库的公开 clone 提供动画和代码，人物原件与衍生网格仅在本机提供。

1. 用原交付 ZIP [恢复本地素材](docs/third-party-assets.md)。如缺少玩家裁切资源，运行 **扳手腕 → P1-D → Prepare Local Player Arm**。
2. 打开主场景，在 **Runtime Bootstrap** 关闭 **Use Humanoid Grip** 和 **Use Paired Grip**，勾选 **Use Original Art**、**Use Character Motion**、**Use Hand Contact**，然后 Play。关掉 **Use Hand Contact** 可回看 P1-D，关掉 **Use Original Art** 即回到胶囊。

见 [P1-E 运行、对照与本机证据](docs/P1E-hand-contact-result.md)。

## P2-A Rocketbox 人物与成对握姿

在主场景 **Runtime Bootstrap** 关闭 **Use Humanoid Grip**，勾选 **Use Paired Grip**，将 **Paired Collisions** 设为 **Off** 后 Play。该模式使用随仓库分发的 MIT Rocketbox 人物、原有比赛动画和统一的双手姿态。胶囊回退和本机 P1-E 可继续运行。见 [人物许可、姿态编辑与 Editor 验证](docs/P2A-avatar-and-paired-grip-result.md)。

## P2-B 碰撞诊断与修正试验

在 Rocketbox 模式下用 **Paired Collisions** 切换 **Off**、**Diagnose**、**Correct**。**Show Collision Gizmos** 在 Scene 视图显示代理体与分离方向。见 [P2-B 实验、对照与复现](docs/P2B-collision-experiment-result.md)。

## P2-E 美术握姿

主场景的 Runtime Bootstrap 里启用 **Use Authored Grip** 可查看按四视角参考制作的 Ready / Contest。默认使用 P2-D 稳定握姿。通过 **扳手腕 → P2-E → Open Authored Grip Preview** 可编辑骨节；已有本机 Ch03 Profile 可用同菜单的 **Apply Ch03 Middle Finger Calibration** 应用中指校准。[本轮结果与复现](docs/P2E-contact-finish-result.md) · [同镜头前后对比](docs/validation/p2e-contact-finish/README.md)

## P2-D 跨人物握姿

主场景默认关闭 **Use Grip Surface Targets**，使用较保守的跨人物 LegacyHumanoid 握姿。一次有界关节约束实验未达到自然扣握验收，见[本轮结论与默认回退](docs/P2D-joint-constraints-result.md)和[真实对照](docs/validation/p2d-joint-constraints/README.md)。实验及上轮表面目标资源仍保留，换人物仍只选 Profile。

此前修订新增掌部/虎口/拇指及四指的可视接触目标；[最新画廊](docs/validation/p2d-contact-targets/README.md)和[本轮修订结果](docs/P2D-contact-targets-revision.md)展示改善与代价。关闭该开关可回看首轮 P2-D。

主场景已接入共享 `HumanoidGripDefinition` 与独立玩家/对手 `GripAvatarProfile`。可在 Runtime Bootstrap 选择人物配置，无需改 C#；Rocketbox 与本机 Ch03 两套不同骨架均已实际 Editor 扫角及比赛回归。人物切换、骨映射要求、几何限制见 [P2-D 结果](docs/P2D-cross-avatar-grip-result.md)，真实 PNG/GIF 见 [公开画廊](docs/validation/p2d-visual/README.md)。**松握、指节交错及部分肘垫/胸臂问题仍在，跨骨架运行通过不等于自然握姿完成。**

以下历史 P2-A～P2-C / P1-E 模式先关闭 **Use Humanoid Grip** 再使用原开关；P2-C 与胶囊回退均保留。原有 P2-C 画廊未覆盖。

## P2-C 主场景接触握持

可在 GitHub 直接查看 [P2-C 视觉画廊：零位、±30° 与连续 GIF](docs/validation/p2c-visual/README.md)。PNG 左旧右新，GIF 上旧下新；仍可看到虎口穿模和坐姿的剩余问题。

关闭新 **Use Humanoid Grip** 后，**Use Paired Grip + ContactGrip** 可回看原 P2-C。使用可编辑 open/closed 配对端点、同步接触闭合和工作肘侧坐姿；原动画和机械握点继续驱动比赛。虎口仍有交叠，尚未完成自然握姿。Runtime Bootstrap 的 **Paired Collisions** 可选 LegacyContactGrip 回看首轮 P2-C，或 Off／Diagnose／Correct 回看 P2-A／P2-B，关闭 **Use Paired Grip** 使用胶囊。见 [集成结果、限制与证据](docs/P2C-physical-interaction-result.md)。

## 实际验证与交接

- **P2 主场景回归**。默认 Rocketbox、可选美术稿、本机 Ch03、混合角色及历史模式的实测清单见 [P2 阶段交接](docs/P2-closeout.md)。
- **P1 收口已验证**。Unity 中国版 `2022.3.62f3c1`、Windows Direct3D11 下，默认胶囊和本地 P1-E 均进入主场景 Editor Play，通过现有按钮回调完成选角、倒计时、暂停、继续和结算，运行错误为 0。P1-E 对抗片段、玩家蒙皮和短袖、对手肘部求解均通过检查。
- **仍待验证**。PCVR、PICO 真机、真实鼠标与头显输入、机械握点和零位标定、STM32 及 PC UDP 联调（含角色 ID 和序号兼容）。Windows 独立构建的既有冒烟证据来自 P0。
- 测试结果、限制及下一步：[P0 交接](docs/P0-handover-result.md)。资产边界与 ZIP 基线：[许可清单](docs/third-party-assets.md)。阶段状态：[TASKS](TASKS.md)。

复现自动 Editor Play 检查（关闭其他使用本工程的 Editor，**不加 `-quit` / `-autotest`**）：

```powershell
New-Item -ItemType Directory .p0-local -Force | Out-Null
& "D:\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe" -batchmode -projectPath "$PWD" -executeMethod ArmWrestlingVR.EditorTools.P0Validation.Run -logFile "$PWD\.p0-local\editor-play.log"
python tools/check_import.py
```

直接打开主场景会使用 P2-D 稳定人物。上述验证命令无额外参数时专测胶囊；追加 `-p0crossdefault` 测主场景默认握姿，`-p0authored` 测可选美术握姿，`-p0contact` 跑主场景 ContactGrip。准备好本地人物资源后，追加 `-p0handcontact` 可重跑 P1-E。工具进入实际 Play、调用现有 UI 按钮并检查角度与结算，失败时返回非零退出码。日志和截图保存在 `.p0-local/`。静态 GUID 检查需要先让 UPM 完成解析。

PC 控制仓库独立维护：[Ihtbfas/armwrestling](https://github.com/Ihtbfas/armwrestling)。UDP 以其 [V0.1 文档](https://github.com/Ihtbfas/armwrestling/blob/main/docs/vr-udp-v01.md)为准。本轮不修改控制仓库或协议。原网络客户端的角色 ID 与当前 PC 人物库尚需对齐，正式联网前先处理交接文档中的问题。

参考 [架构建议](docs/architecture-reference.md)、[P0 任务书](docs/tasks/P0-import-and-run.md)及 [AGENTS](AGENTS.md)。根部 MIT 不覆盖第三方素材或 SDK。
