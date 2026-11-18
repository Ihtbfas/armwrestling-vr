# P0 资产与公开范围（2026-11-18）

本仓库的 MIT 文件不授予第三方模型、字体、贴图或 SDK 的再分发权限。
用户授权接管的项目专用 C#、场景、纯色材质、参数和 IK 配置按原路径导入；第三方资源单独处理。

| 内容 | 调查依据 | 本轮处理 |
|---|---|---|
| `Resources/Characters/Ch03.fbx`，20,974,352 B | 原交付文档推荐 Mixamo；FBX 来源/公开再分发授权未附带 | 不上传文件和 `.meta`；本机从原 ZIP 恢复 |
| `Resources/HDRI/circus_arena.hdr`，25,834,262 B | 代码称 Poly Haven；ZIP 的 `sucai'/crossfit_gym_4k.hdr` 同大小，但未取得能对应这些字节的来源证明 | 保守留本地；不以文件名推定 CC0 |
| `Resources/ArmWrestlingFont.ttf`，17,773,244 B | 未附字体许可 | 不上传文件和 `.meta`；Windows 使用系统字体回退，不复制系统字体 |
| PICO OpenXR SDK | ZIP 没有 SDK，只有原电脑绝对路径；官方固定提交的 `package.json` 为 1.4.1，与原说明一致 | UPM 引用官方 Git 固定提交，不把 SDK 代码/库打入本仓库 |
| Unity XR/XRI、Hands、Input System、uGUI 等 | 原 `manifest.json` / `packages-lock.json` | 保留版本，UPM 下载；不提交 PackageCache |
| `IdlePose.anim` / `OpponentIK.controller` | 工程编辑器脚本生成；片段无动画曲线，控制器用于触发 Humanoid IK | 原样保留，不视为成品动作库 |

上表前三项路径均相对 `Assets/ArmWrestling/`，文件及原 `.meta` 都被 `.gitignore` 排除。
公开版本沿用原工程已有的胶囊对手/纯色环境回退，不另建替代场景。
PICO SDK 上游 `LICENSE.md` 保留权利声明，不能因为可以下载就把它当作 MIT 重发。

## 有原始 ZIP 时恢复本地完整美术

关闭使用本工程的 Editor 后，在仓库根运行：

```powershell
python tools/restore_local_assets.py "E:\桌面\研究生\实验室\人机交互课程\实物大作业\code\banshouwan.zip"
git status --short
```

脚本只读取六个明确条目，校验每个文件 SHA-256，恢复原 GUID；发现本地文件被改过时拒绝覆盖。
它不导入 ZIP 内的缓存、PC 控制代码、额外人物或历史构建，也不改变 Git 忽略规则。
恢复后在 Main 场景的 Runtime Bootstrap 勾选 Use Original Art 才使用原人物/HDRI；默认保持胶囊展示。原人物姿态、材质仍需改进。
恢复仅用于本机运行，不代表取得公开分发权限。确认具体资产授权后，再单独评估公开或替换；本轮无需 Git LFS。

## 基线

- 原件：同级 `banshouwan.zip`，2,351,610,922 B，未覆盖。
- SHA-256：`828e42a024e6c99f9617a421cbb49e4de82ae396a46a42a423d0a09f001a26b4`。
- 工程入口：`banshouwan/test1/`。
- 原始 Assets / Packages / ProjectSettings 共 167 个文件的散列和 GUID：`p0-source-inventory.json`。
- 本轮本地基线及日志：仓库 `.p0-local/`（Git 忽略）。原交付说明在 `.p0-local/baseline/Docs/`，其中旧协议、既往运行声称不作为本轮验证结论。

上游参考：[PICO 固定提交](https://github.com/Pico-Developer/PICO-Unity-OpenXR-SDK/tree/3aa3e62bff41df618529eeb60ff02c29a515dafe)、[SDK 许可声明](https://github.com/Pico-Developer/PICO-Unity-OpenXR-SDK/blob/3aa3e62bff41df618529eeb60ff02c29a515dafe/LICENSE.md)。
