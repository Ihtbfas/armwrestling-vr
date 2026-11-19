# P2-B 碰撞感知动画实验

## 运行方式

在 `ArmWrestlingMain` 的 **Runtime Bootstrap** 勾选 **Use Paired Grip**，然后选择 **Paired Collisions**。三个模式分别为：

| 模式 | 在 Play 中的表现 |
| --- | --- |
| **Off**（默认） | P2-A 原姿态与 IK |
| **Diagnose** | 原姿态、代理重叠查询和诊断 |
| **Correct** | 诊断与有限的指节、对手右臂修正 |

打开 **Show Collision Gizmos** 和 Scene 视图的 Gizmos，可查看青色代理、红色重叠代理与黄色分离方向。Play 中可在装置层级下的 **Paired collision experiment** 切换 *Mode* 与 *Show Gizmos*。

## 代理配置与执行顺序

- 共 44 个查询体。两边右手各有 1 个掌盒与 15 个指节胶囊。掌盒厚 23 mm，长宽按腕和指根标志点取值。指根胶囊半径 7.5–10 mm，向末节缩小，长度沿相邻关节或末节轴。
- 玩家右臂、对手左右臂各有上臂／前臂胶囊，半径 43／31 mm。上臂近肩端截去骨段长度的 30%，减少肩接头的预期重叠。对手胸和腹用随骨架的盒体（304 × 220 × 160 mm、242 × 203 × 146 mm）。两侧肘点用半径 34 mm 的球，与现有肘垫尺寸的盒体查询。
- 只检查跨人物手—手、臂—对手躯干、及肘—对应肘垫。统计将 2 mm 内的代理接触视为余量。明细保留每对的代理标签、深度和第一个代理的分离方向、中心点，坐标以装置为准。红点和箭头示意代理中心与方向，箭头为便于观察已放大。
- 初试中禁用 Collider 导致零位查询全为零。现将启用的代理放在隔离、未推进模拟的 3D 物理场景。启动时核查已知双球重叠 18 mm，及另一组不相交。每次用最新骨骼的显式位姿查询。 [Unity ComputePenetration](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Physics.ComputePenetration.html) · [Unity 多场景物理](https://docs.unity3d.com/2022.3/Documentation/Manual/physics-multi-scene.html) · [Meta 指骨胶囊](https://developers.meta.com/vr/documentation/unity/unity-handtracking-hands-setup/)

机械输入和 P2-A 的配对姿态继续提供腕部目标。Animator IK 后先恢复双方预制指骨，随后按碰撞结果尝试指根转动（每指至多 14°）。对手右臂可围绕肩—腕轴有限外转，保持两端位置和骨长。候选必须减少代理重叠，且肘点水平修正不超过 30 mm，相对原帧下沉最多 4 mm、上移最多 12 mm。分离方向易在胸盒不同面之间跳变，外转方向按人体外侧选定，再根据查询收益平滑衰减。实际计算挂在 `LateUpdate` 的配对手骨步骤，每次 IK 求值只处理一次。采集代码显式调用同一步骤，因此报告对应渲染后的骨骼。 [Unity 事件顺序](https://docs.unity3d.com/2022.3/Documentation/Manual/ExecutionOrder.html)

## 实测与限制

使用 Unity 中国版 `2022.3.62f3c1`、Windows Direct3D11。同一 Rocketbox、同一场景及机位，181 帧、30 Hz、相同 −30°／0°／+30° 往返。A 为 Diagnose，B 为 Correct。零位代理定位在双方拇指、食指等近节和对手右上臂—胸部。肘垫代理只参与诊断。

| 核查项 | A Diagnose | B Correct |
| --- | ---: | ---: |
| 零位上臂—胸部代理重叠 | 45.1 mm | 33.6 mm |
| 上臂—胸部全程峰值 | 71.4 mm | 71.4 mm |
| 手—手代理重叠对数（每帧，超过 2 mm） | 30 | 28 |
| 手—手代理峰值（全程） | 16.0 mm | 15.7 mm |
| 双向近邻蒙皮疑似穿入采样点（每帧均值） | 34.8 | 31.0 |
| 对手腕到目标最大误差 | <0.020 mm | <0.020 mm |
| 双掌心代理平均距离 | 33.95 mm | 33.95 mm |
| 玩家／对手最大腕折角 | 23.2°／26.3° | 23.2°／26.3° |
| 对手肘骨水平最大点间距 | 96.9 mm | 118.6 mm |
| 对手肘骨最大单帧位移 | 3.8 mm | 10.0 mm |
| 对手肘下缘蒙皮相对垫面最小高度 | −14.3 mm | −14.3 mm |

两个模式的 Grip Anchor 位置、角度序列和动画相位一致。原对手肘垫支撑求解在两组各 181 帧全部生效。B 的零位肘下缘采样从 −2.3 mm 变为 −6.0 mm。臂部改善集中在零位附近，代价是肘滑移与局部下沉。数值 0 表示该组无超过 2 mm 的代理重叠。

零位正面能看到右袖口更向外，袖口与胸部的视觉交叠减少。手侧近景中，部分指节让开，但仍可见局部指节交叠。较深的拇指／小指近节重叠仍存在。负角度端的上臂穿入没有明显改变，肘部在部分角度会比 P2-A 多滑动。代理深度和离散蒙皮采样用于定位，真实网格的边界仍须看渲染。这里保留可切换的实验模式。

P2-B、P2-A、P1-E 和胶囊均在实际 Editor Play 完成选角、倒计时、暂停、继续与结算，运行错误为 0。P2-B 比赛模式确认了查询和手指修正在运行。PICO、PCVR、实物标定、UDP 与设备性能本轮未测。

## 本机证据与复现

| 本机目录（相对项目根目录） | 内容 |
| --- | --- |
| `.p0-local/p2b-collision/20261119-105441/` | A，原姿态三角度及连续帧、两份 CSV |
| `.p0-local/p2b-collision/20261119-105511/` | B，修正三角度及连续帧、两份 CSV、`hands-before-after.mp4`、`body-before-after.mp4`、`neutral-visual-comparison.png`、`diagnostic-comparison.png` |
| `.p0-local/p2b-collision/` | `baseline-final.log`、`corrected-final.log`、四种比赛模式的 `*-play-final.log` 和结果文本 |

在关闭本项目的其他 Editor 实例后，使用：

```powershell
$unity = "D:\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe"
New-Item -ItemType Directory -Force .p0-local/p2b-collision | Out-Null
& $unity -batchmode -projectPath "$PWD" -executeMethod ArmWrestlingVR.EditorTools.AnimationComparison.Run -p2b -logFile "$PWD\.p0-local\p2b-collision\baseline.log"
& $unity -batchmode -projectPath "$PWD" -executeMethod ArmWrestlingVR.EditorTools.AnimationComparison.Run -p2b -p2bcorrect -logFile "$PWD\.p0-local\p2b-collision\corrected.log"
& $unity -batchmode -projectPath "$PWD" -executeMethod ArmWrestlingVR.EditorTools.P0Validation.Run -p0collision -logFile "$PWD\.p0-local\p2b-collision\play.log"
```

采集追加 `-p2aneutral -p1stills` 可快速检查零位。下一步优先在头显实际眼位核对仍明显的指根，再决定是否重做局部配对姿态。若要修 −30° 的上臂—胸部和肘垫约束，应先用实物握点／肘垫位置验证可达空间，再考虑肩和躯干联动。
