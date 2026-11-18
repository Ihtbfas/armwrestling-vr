# armwrestling-vr

单自由度力反馈扳手腕系统的 **Unity/VR 正式工程仓库**。本仓库主要负责场景、虚拟人物、骨骼动画、单轴动作映射、VR 输入与 UI、比赛画面。

**关联 PC 控制仓库：** https://github.com/Ihtbfas/armwrestling

## 当前状态（2026-11-18）

- 新仓库已建立；**VR 同学原始工程尚未导入或验证**。
- 暂不预设具体 Unity/团结引擎版本、XR SDK、PICO 设备或渲染管线。以交付工程实际情况为准。
- 第一阶段：**P0 接收原工程、保留有效资产、运行已有场景、记录现状**。
- 参考架构见 [docs/architecture-reference.md](docs/architecture-reference.md)（可调整，不是必须照搬的模板）。
- 实施任务见 [docs/tasks/P0-import-and-run.md](docs/tasks/P0-import-and-run.md)；阶段状态见 [TASKS.md](TASKS.md)；Codex 指引见 [AGENTS.md](AGENTS.md)。

## 工作原则

1. **先让同学的现有工程跑起来，再做架构整理。** 不因预设框架而重建或丢弃可用资源。
2. Unity 工程与 PC 控制代码独立维护；当前 UDP V0.1 以 PC 仓库 [docs/vr-udp-v01.md](https://github.com/Ihtbfas/armwrestling/blob/main/docs/vr-udp-v01.md) 为准。
3. 代码简单、直观、可维护。优先复用原场景、Prefab、动画、插件。
4. 允许 Codex 选择必要的修复方法，自行完成多轮试错；真实 Editor/头显测试与仅静态检查必须严格区分。
5. 本仓库公开且采用 MIT 许可。**同学交付模型、动画、音频、商店素材和第三方 SDK 的重新发布权限须另行确认；不得默认这些内容都属于 MIT。**

初次导入时，应纳入工程实际所需的 `Assets/`、`Packages/`、`ProjectSettings/` 及相应 `.meta` 文件；忽略 `Library/` 等可重新生成的缓存。工程根目录最终由 P0 按实际项目确定。
