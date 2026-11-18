# P1-E 双手握持与玩家手臂

在 Unity 中国版 `2022.3.62f3c1` 的 `ArmWrestlingMain` 中验证。P1-E 沿用 P1-D 的两个 Humanoid 片段、Effort Blend Tree、Animator IK 和演示肘垫。

## 运行

1. 按[第三方资源恢复说明](third-party-assets.md)恢复本机 Ch03。如尚无玩家裁切网格，运行 **扳手腕 → P1-D → Prepare Local Player Arm**。
2. 打开 `Assets/Scenes/ArmWrestlingMain.unity`，在 **Runtime Bootstrap** 勾选 **Use Original Art**、**Use Character Motion**、**Use Hand Contact**，进入 Play。可按空格切换自动演示。
3. 关掉 **Use Hand Contact** 对照 P1-D。只保留 **Use Original Art** 可查看原 Ch03 模式。关闭美术开关可使用默认胶囊。没有本机裁切网格时，继续显示胶囊玩家。

两手继续跟随同一机械 `Grip Anchor`。对手腕目标和肘部可达求解沿用 P1-D。P1-E 取两手纵向轴的叉积作为两掌共同的侧向参考，分别朝相反方向，并保留少量设备横轴权重。玩家掌心目标偏置为设备坐标 `(+32, 0, +8) mm`，P1-D 为 `(+28, 0, 0) mm`。玩家与对手分别使用各自的指节屈曲及拇指对掌角。短袖由代码生成，跟随玩家上臂骨，封住原裁切边；外部机位和第一人称机位沿用原设置。

## 同输入 Editor Play 对照

同一场景、Ch03 网格和握点轨迹。每组 181 帧，30 Hz、6 秒往返。第 0／45／90 帧对应 −30°／0°／+30°。第一人称特写取原玩家眼位，单独缩小视野并对准共享握点。正视、侧视和原第一人称均另外保存。

| ±30° 记录项 | P1-D | P1-E |
| --- | ---: | ---: |
| 双手掌面法线夹角（平均） | 139.1° | 170.4° |
| 拇指远节关节代理间距（平均） | 96.9 mm | 28.7 mm |
| 两掌心代理间距（平均） | 30.0 mm | 35.5 mm |
| 双向疑似穿入点（每帧平均） | 27.8 | 8.5 |
| 玩家／对手腕折角（最大） | 12.1°／4.6° | 12.1°／4.5° |
| 对手掌心代理到共享握点误差（最大） | 3.31 mm | 3.31 mm |
| 玩家掌心代理到自身偏置目标误差（最大） | <0.01 mm | <0.01 mm |
| 对手肘骨水平最大点间距 | 48.6 mm | 48.6 mm |
| 对手肘下缘到垫面的蒙皮采样间隙 | 1.5–7.4 mm | 1.5–7.4 mm |

从近景看，双方掌面更相对，拇指转向对握区域，零位处原本横穿白色手背的橙色手指更少见。短袖在三角度侧视遮住裸露裁切边。−30° 附近拇指根部和掌面仍有局部重叠，+30° 部分指尖看起来略松。±42° 单独扫过 181 帧，肘部约束保持，−42° 附近仍可测到约 2.8 mm 的垫面采样穿入。外部视角仍能看出玩家模型仅有手臂。

疑似穿入用 `BakeMesh` 取双方各 1,114 个去重蒙皮顶点。对每个点，在另一只手 20 mm 邻域找最近采样顶点，筛选法线相对的样本，再统计沿对方外法线向内达到 1.5 mm 的点。它可帮助找到需要看近景的区域。稀疏近邻和法线会漏检，也可能将曲面折角误判，不能据此断定网格接触或无穿模。正式录像对应的 CSV 中 `thumb_tip_separation_mm` 实际记录拇指远节关节原点距离，当前工具列名已改为 `thumb_distal_separation_mm`。

## 回归和本机证据

Windows Direct3D11 实际 Editor Play 中，默认胶囊、原 Ch03、P1-D、P1-E 均完成设备检查、跳过标定、选角、准备、倒计时、暂停、继续和结算，运行错误均为 0。P1-E 在 Fighting 中检测到对抗片段并检查了玩家短袖和肘部求解。PCVR、PICO 真机和实物联调本轮未测。

| 本机目录（相对项目根目录） | 证据 |
| --- | --- |
| `.p0-local/p1e-hand-contact/20261118-163547/` | `grip-before-after.mp4`（左 P1-D、右 P1-E，上为第一人称特写、下为手部侧近景），`eyes-before-after.png`（三角度），`p1e/` 和 `measurements.csv` |
| `.p0-local/p1e-hand-contact/20261118-163513/` | P1-D 的同角度 `p1d/` 截图、连续帧和 CSV |
| `.p0-local/p1e-hand-contact/20261118-163623/` | P1-E ±42° 关键帧与 CSV |
| `.p0-local/p1e-hand-contact/` | `baseline-final.log`、`contact-final.log`、`contact-full-range.log` 和四组 `play-*.log`、结果文本及画面 |

原模型、裁切网格和上述画面证据均只保留本机。可在关闭同项目的 Editor 后，分别执行以下命令重现。

```powershell
$unity = "D:\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe"
New-Item -ItemType Directory .p0-local -Force | Out-Null
& $unity -batchmode -projectPath "$PWD" -executeMethod ArmWrestlingVR.EditorTools.AnimationComparison.Run -p1e -logFile "$PWD\.p0-local\p1e-sweep.log"
& $unity -batchmode -projectPath "$PWD" -executeMethod ArmWrestlingVR.EditorTools.AnimationComparison.Run -p1e -p1ebaseline -logFile "$PWD\.p0-local\p1e-baseline.log"
& $unity -batchmode -projectPath "$PWD" -executeMethod ArmWrestlingVR.EditorTools.P0Validation.Run -p0handcontact -logFile "$PWD\.p0-local\p1e-play.log"
```

采集可用 `-p1stills` 仅保存三角度 PNG 和全程 CSV，追加 `-p1bfullrange` 可扫到 ±42°。比赛回归可改用 `-p0motion` 或 `-p0art`，省略附加参数运行默认胶囊。

下一轮可在授权明确的手臂模型上做拇指根部和指腹接触细调，并在头显复核实际第一人称距离。实物几何有数据后再处理极限角肘垫压入。
