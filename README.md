# armwrestling-vr

单自由度力反馈扳手腕的正式 Unity/VR 工程。P0 已接管同学交付的 `banshouwan/test1`，保留原场景和代码路径，工程入口就是本仓库根目录。

`main` 是 P0～P1-E 的 Windows Editor 离线演示基线，功能提交为 [`e1a3ad9`](https://github.com/Ihtbfas/armwrestling-vr/commit/e1a3ad92cf3f51bc5c261ddbf6a04712b6e9703a)。公开 clone 可直接使用胶囊模式。本机 P1-E 人物模式包含玩家蒙皮手臂、对手预制动画与 IK、肘部支撑和双手握姿。

## 最简运行（Windows）

1. 用 **Unity 中国版编辑器 `2022.3.62f3c1`** 从 Hub 添加本仓库目录。本机需有 Git 命令行；首次导入需联网下载 Unity 包和固定版本 PICO SDK，本轮首次导入约数分钟。
2. 打开 **`Assets/Scenes/ArmWrestlingMain.unity`**，等待编译后点 **Play**。编辑状态下主场景只有 Runtime Bootstrap，运行时会搭出原有桌面、手臂、对手和 UI，属于原工程设计。
3. 在面板依次选择 **下一步：空间标定 → 跳过标定 → 角色 → 准备对战**。准备和倒计时后进入比赛。
4. **空格**切换自动演示；**A / ←** 我方发力，**D / →** 对手发力；**Esc** 暂停。也可使用面板上的暂停、继续和再来一局。

默认使用离线模拟，运行不需要 PC 控制程序、STM32 或头显。桌面自动 OpenXR 初始化默认关闭；PCVR 需自行在 Standalone XR 设置中启用并配置运行时，本轮未验证。

**公开版本**使用原代码已有的胶囊对手和纯色环境回退。人物 FBX、HDRI、字体许可未确认，文件及其原 `.meta` 保留在本机、不上传；有原 ZIP 可按 [恢复说明](docs/third-party-assets.md)还原美术资源，并在主场景 Runtime Bootstrap 勾选 **Use Original Art** 查看原人物/HDRI。原人物的姿态和材质仍需调整，默认关闭此开关。Windows 内置动态字体回退已在本机检查，其他机器/Android 的中文字形仍需复核。

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
2. 打开主场景，在 **Runtime Bootstrap** 勾选 **Use Original Art**、**Use Character Motion**、**Use Hand Contact**，然后 Play。关掉 **Use Hand Contact** 可回看 P1-D，关掉 **Use Original Art** 即回到默认胶囊。

见 [P1-E 运行、对照与本机证据](docs/P1E-hand-contact-result.md)。

## P2-A Rocketbox 人物与成对握姿

在主场景 **Runtime Bootstrap** 勾选 **Use Paired Grip** 后 Play。该模式使用随仓库分发的 MIT Rocketbox 人物、原有比赛动画和统一的双手姿态。默认胶囊和本机 P1-E 可继续运行。见 [人物许可、姿态编辑与 Editor 验证](docs/P2A-avatar-and-paired-grip-result.md)。

## 实际验证与交接

- **P1 收口已验证**。Unity 中国版 `2022.3.62f3c1`、Windows Direct3D11 下，默认胶囊和本地 P1-E 均进入主场景 Editor Play，通过现有按钮回调完成选角、倒计时、暂停、继续和结算，运行错误为 0。P1-E 对抗片段、玩家蒙皮和短袖、对手肘部求解均通过检查。
- **仍待验证**。PCVR、PICO 真机、真实鼠标与头显输入、机械握点和零位标定、STM32 及 PC UDP 联调（含角色 ID 和序号兼容）。Windows 独立构建的既有冒烟证据来自 P0。
- 测试结果、限制及下一步：[P0 交接](docs/P0-handover-result.md)。资产边界与 ZIP 基线：[许可清单](docs/third-party-assets.md)。阶段状态：[TASKS](TASKS.md)。

复现自动 Editor Play 检查（关闭其他使用本工程的 Editor，**不加 `-quit` / `-autotest`**）：

```powershell
New-Item -ItemType Directory .p0-local -Force | Out-Null
& "D:\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe" -batchmode -projectPath "$PWD" -executeMethod ArmWrestlingVR.EditorTools.P0Validation.Run -logFile "$PWD\.p0-local\editor-play.log"
python tools/check_import.py
```

验证命令默认跑胶囊模式。准备好本地人物资源后，追加 `-p0handcontact` 可重跑 P1-E。工具进入实际 Play、调用现有 UI 按钮并检查角度与结算，失败时返回非零退出码。日志和截图保存在 `.p0-local/`。静态 GUID 检查需要先让 UPM 完成解析。

PC 控制仓库独立维护：[Ihtbfas/armwrestling](https://github.com/Ihtbfas/armwrestling)。UDP 以其 [V0.1 文档](https://github.com/Ihtbfas/armwrestling/blob/main/docs/vr-udp-v01.md)为准。本轮不修改控制仓库或协议。原网络客户端的角色 ID 与当前 PC 人物库尚需对齐，正式联网前先处理交接文档中的问题。

参考 [架构建议](docs/architecture-reference.md)、[P0 任务书](docs/tasks/P0-import-and-run.md)及 [AGENTS](AGENTS.md)。根部 MIT 不覆盖第三方素材或 SDK。
