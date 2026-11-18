# P1-D 玩家手臂、对手肘部与预制动画

Unity 中国版 `2022.3.62f3c1`，主场景 `ArmWrestlingMain`。P1-D 为可选本机演示。保留公开版本的胶囊回退和 P1-C 人物模式。

## 如何运行

1. 按 [第三方资源恢复说明](third-party-assets.md) 在本机恢复 Ch03。
2. 在 Editor 中运行 **扳手腕 → P1-D → Prepare Local Player Arm**。生成的 `Assets/ArmWrestling/Resources/Characters/P1DLocalPlayerArm.asset` 和 `.meta` 均已被 Git 忽略。
3. 打开 `Assets/Scenes/ArmWrestlingMain.unity`。在 **Runtime Bootstrap** 勾选 **Use Original Art** 和 **Use Character Motion**，进入 Play 并按原流程开赛。P1-D 会启用弯指握姿。
4. 关闭 **Use Character Motion** 可回到 P1-C。关闭 **Use Original Art** 可体验公开胶囊模式。本地 Ch03 存在而裁切网格缺失时，先用 P1-C 的对手和胶囊玩家运行。

玩家显示使用 Ch03 裁出的右臂蒙皮，保留 2,594 个三角面和原骨骼绑定。腕、肘用原有双骨求解驱动，弯指使用 P1-C 的握姿数据。玩家掌心目标相对共享握点沿设备横轴偏置 28 mm，为双方不同的手掌留出空间。手臂显示与原单自由度角度同步。

对手在原演示垫面内选择内侧支撑区。先用实际前臂长度、腕到握点的 45 mm 偏置和握点高度求出可达肘点，再用上臂长度补偿肩部位置。以肘骨高于垫面约 14 mm 作为候选位置，另用蒙皮代理测量真实表面趋势。身体补偿受 70 mm 上限约束，候选不可达时退回 P1-C 解。肘垫、机械握点轨迹和角色骨长沿用原值。

`SeatedPrepare.anim`（2.8 s）和 `SeatedContest.anim`（1.8 s）各含七条手工编写的 Humanoid 肌肉曲线，覆盖肩、脊柱、胸、颈及头部。比赛阶段和当前角度驱动 **Effort** Blend Tree。保留三档身体摆位，Animator IK 继续负责手脚目标。公开仓库中的两个片段及 Controller 是原创曲线资源，独立于本机 FBX 和裁切网格。

## Editor Play 对照

同一场景、同一本机 Ch03、同一握点轨迹。每组按 30 Hz 手动推进片段，181 帧覆盖一次 6 s 往返；第 0、45、90 帧为负端、零位、正端。基线为 P1-C 默认 Ch03、胶囊玩家；P1-D 包含弯指姿态。自动采集给 `Effort` 传入固定渐变以重现两个片段，普通比赛由阶段和角度控制。

| 记录项 | P1-C ±30° | P1-D ±30° | P1-D ±42° |
| --- | ---: | ---: | ---: |
| 肘骨水平最大点间距 | 267.8 mm | 48.6 mm | 82.6 mm |
| 肘骨横向跨度 | 263.6 mm | 9.3 mm | 9.3 mm |
| 肘部蒙皮采样点水平最大点间距 | 260.1 mm | 56.3 mm | 87.6 mm |
| 采样下缘垫面间隙（最低／平均／最高） | 0.6／2.4／3.7 mm | 1.5／4.8／7.4 mm | −2.8／4.1／8.0 mm |
| 对手掌心代理到共享握点最大误差 | 3.31 mm | 3.31 mm | 3.31 mm |
| 对手腕折角（最大） | 13.5° | 4.6° | 4.6° |
| 玩家蒙皮掌心到自身偏置目标最大误差 | — | <0.01 mm | <0.01 mm |

±30° 内，肘骨水平移动范围减少约 82%，蒙皮代理减少约 78%。P1-D 两组各 181 帧均使用新肘部解，采样点均位于垫面投影内。±30° 平均离垫升高约 2.4 mm；−42° 极值处有约 2.8 mm 代理点穿入，极限位置尚需复查。两段片段和中间混合状态均实际播放。头与胸部局部旋转分别相对初始姿态变化约 4.4°、4.2°，视频中可见轻微的准备与发力变化。

第一人称可见玩家手掌、弯曲手指和前臂。外部相机能看到上臂裁切边缘。双方手指仍有局部视觉重叠；P1-D 两个掌心代理间距约 30 mm，目前没有逐指网格接触判定。表中的代理误差仅用于核对握点跟随和垫面关系。

### 回归与本机证据

Windows Direct3D11 的实际 Editor Play 中，默认胶囊、原 Ch03、P1-D 均完成角色选择、准备、倒计时、暂停、继续、结算，三次运行均无错误，P1-D 在正常比赛的 Fighting 阶段也检测到 `SeatedContest`。P1-D 桌面和头显打包、PCVR、PICO 真机、实物标定与正式 UDP 联调未测。

| 本机目录（相对项目根目录） | 内容 |
| --- | --- |
| `.p0-local/p1d-motion/20261118-155355/` | `player-front-side-sweep.mp4`（第一人称／正面／侧面），`motion/` 中有三个角度的 PNG，另有 `match-result.png` 和 `measurements.csv` |
| `.p0-local/p1d-motion/20261118-155417/` | 同角度 P1-C 的 `baseline/` 截图与 CSV |
| `.p0-local/p1d-motion/20261118-155430/` | ±42° 的 `motion/` 截图与 CSV |
| `.p0-local/p1d-motion/` | `candidate-fixed.log`、`baseline-fixed.log`、`full-range-fixed.log`、`p0-capsule-final.log`、`p0-ch03-final.log`、`p0-motion-final.log` |

复现使用现有 Editor，命令执行前关闭同一工程的其他 Editor 实例。每次启动记录一组。

```powershell
$unity = "D:\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe"
New-Item -ItemType Directory .p0-local -Force | Out-Null
& $unity -batchmode -projectPath "$PWD" -executeMethod ArmWrestlingVR.EditorTools.AnimationComparison.Run -p1d -logFile "$PWD\.p0-local\p1d-sweep.log"
& $unity -batchmode -projectPath "$PWD" -executeMethod ArmWrestlingVR.EditorTools.P0Validation.Run -p0motion -logFile "$PWD\.p0-local\p1d-play.log"
```

第一条可追加 `-p1dbaseline` 采集旧版，或追加 `-p1bfullrange -p1stills` 仅采集 ±42° 的关键帧和数据。需要重新裁切网格时可用 `CharacterMotionAssets.PrepareDemo` 执行 Editor 批处理，方法名加前缀 `ArmWrestlingVR.EditorTools.`，同时传入 `-p1d-recrop -quit`。Ch03、裁切网格和含原人物的画面仅保留本机。

## 下一步

1. 使用授权明确的第一人称手臂，按两手独立的掌面坐标调整相对位置和拇指搭扣，再用局部网格或轮廓采样检查穿插。
2. 拿到实物数据后重算握点与垫面关系，再处理 −42° 端点与肘下缘间隙。
3. 完善预制片段的准备到对抗变化，并在头显中观察视点、手臂遮挡与节奏。
