# armwrestling-vr

单自由度力反馈扳手腕的正式 Unity/VR 工程。P0 已接管同学交付的 `banshouwan/test1`，保留原场景和代码路径，工程入口就是本仓库根目录。

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

## 实际验证与交接

- **已验证：** 原版本首次导入和 C# 编译、两场景脚本引用、主场景真实 Editor Play（批处理驱动）、Direct3D11 渲染、通过现有按钮回调完成角色选择/准备/倒计时/暂停/继续/结算、持续模拟角度；Windows 独立构建及10步加载冒烟检查。
- **未验证：** 真实鼠标射线点选、PCVR、PICO 真机、手柄/手追踪、STM32 与正式 PC UDP 联调。原代码已有相关接口不等于已经联调。
- 测试结果、限制及下一步：[P0 交接](docs/P0-handover-result.md)。资产边界与 ZIP 基线：[许可清单](docs/third-party-assets.md)。阶段状态：[TASKS](TASKS.md)。

复现自动 Editor Play 检查（关闭其他使用本工程的 Editor，**不加 `-quit` / `-autotest`**）：

```powershell
New-Item -ItemType Directory .p0-local -Force | Out-Null
& "D:\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe" -batchmode -projectPath "$PWD" -executeMethod ArmWrestlingVR.EditorTools.P0Validation.Run -logFile "$PWD\.p0-local\editor-play.log"
python tools/check_import.py
```

验证入口会进入实际 Play、调用现有 UI 按钮、检查角度和结算，并以非零退出码报告失败；本机日志/截图保存在 `.p0-local/`。静态 GUID 检查需要先让 UPM 完成解析。

PC 控制仓库独立维护：[Ihtbfas/armwrestling](https://github.com/Ihtbfas/armwrestling)。UDP 以其 [V0.1 文档](https://github.com/Ihtbfas/armwrestling/blob/main/docs/vr-udp-v01.md)为准。本轮不修改控制仓库或协议。原网络客户端的角色 ID 与当前 PC 人物库尚需对齐，正式联网前先处理交接文档中的问题。

参考 [架构建议](docs/architecture-reference.md)、[P0 任务书](docs/tasks/P0-import-and-run.md)及 [AGENTS](AGENTS.md)。根部 MIT 不覆盖第三方素材或 SDK。
