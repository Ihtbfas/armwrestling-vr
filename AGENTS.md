# Codex 工作约定（armwrestling-vr）

## 目标与边界

- 本仓库是**正式 Unity/团结 VR 项目**，优先维护交付的原工程，不创建替代性的全新空工程。
- 负责场景、人物、骨骼/Animator/IK、角度到姿态映射、VR 输入、UI 和比赛视觉流程。
- PC 控制、人物阻力数值、STM32、磁粉制动器、串口协议及整体联调由独立仓库 `Ihtbfas/armwrestling` 负责。
- UDP 不得自行改版；先阅读 PC 仓库 `docs/vr-udp-v01.md`。参考 Unity 测试客户端不等于正式 VR 工程。
- 参考 [架构建议](docs/architecture-reference.md) 和 [P0 任务](docs/tasks/P0-import-and-run.md)；它们是方向与边界，不是必须实现的目录/类名单。

## 工作方法

1. 先检查 `git status`、分支、原工程目录、引擎/包版本及资源来源。**保存交付原件或可恢复的导入基线**。
2. 遇到明确问题可自行搜索、试错、修复和回归；**尽可能一次完成当前阶段**，不要因为每个小决策反复询问。
3. 为最小可运行目标修改必要部分；不做整项目重构、不引入未经需要的大型框架或依赖，不为了结构漂亮批量移动素材。
4. Unity 资源移动时维护对应 `.meta`（GUID）和场景/Prefab 引用；不要把 `Library/Temp/Build` 等生成物提交。
5. GitHub 为 Public。导入之前核对模型、音频、动画、插件及下载素材的授权和重新分发限制。来源不明确时先保留在本机、记录路径和缺失清单，不擅自上传。
6. 一切真实操作要留证据。静态检查、Unity Editor Play、PCVR、PICO 真机是不同验证级别；无法启动 Editor 时不能宣称“已运行”。
7. 代码优先易读、使用简洁的 MonoBehaviour 组件和 Inspector 引用；模块解耦以需要为限，谨慎增加抽象、管理器或架构层。
8. 尽量在独立工作分支实施，交付提交 SHA/PR、实际测试结果、运行步骤、仍存问题和下一步建议；非关键问题不阻塞 P0 完成。

## 本阶段

当前分支 `feat/p1c-geometry-handoff`：执行 [P1-C 实物几何标定准备与手部接触预研](docs/tasks/P1C-physical-geometry-and-hand-pose.md)。沿用 P1-B 的演示基线；不得把演示轴/肘垫参数描述成实物测量值。优先可执行的简明测量交接，骨骼条件允许时再做最小手部动作实验。
