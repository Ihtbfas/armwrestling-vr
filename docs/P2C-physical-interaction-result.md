# P2-C 接触握姿与坐姿集成（PR #10 修订）

打开 `Assets/Scenes/ArmWrestlingMain.unity`，用 Unity 中国版 `2022.3.62f3c1` 进入 Play。主场景继续默认 **Use Paired Grip = true / Paired Collisions = ContactGrip**。本次在同一 P2-C 分支直接替换姿态资产、接触闭合和坐姿适配；原离线比赛流程保留。

四指更接近包握，胸臂相交明显减轻。**虎口、拇指根部和部分指节仍相交，自然扣握尚未完全完成。** 旧代理的手部峰值比首轮更差，不能宣称全手无穿模。这仍是 PhysX 查询约束的骨骼动画，不是 Rigidbody/Joint 动力学手。

## 本次实际修改

- `ContactPairedGrip.asset` 独立保存双方腕位姿和各 15 个指骨的 open/closed 端点。四指用 Humanoid 屈伸轴搭建明确端点，再经 Editor 画面调整；拇指按对方食指虎口的静态导向制作端点。运行时不再猜世界旋转轴。拇指主要保持预制姿势，仅有限闭合。
- 参考 [Hurricane VR Hand Posing](https://cloudwalker2020.github.io/HurricaneVR-Docs/manual/hand_posing.html) 的预制端点、骨骼快照和 Finger Start。比赛开始时已经握持，`fingerStart=0.65`，从插值路径的 65% 继续闭合。没有安装商业插件。端点制作使用 [Unity HumanPoseHandler](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/HumanPoseHandler.html)。
- 双方同时提议动作，在同一候选姿态查询各碰撞对，再共同接受或回退。回退最多传播十次，重新检查其他手指。逐对限位避免深掌根接触掩盖新指尖穿入。起点已有穿入可以保持或减轻，每接受步允许 0.02 mm 查询容差；不假称起点已无穿模。
- 保留 44 个原诊断代理；另外从两手蒙皮中腕骨权重 ≥0.55、距腕沿掌轴 12–85 mm 的点生成两个接触掌凸包，指段仍为胶囊。凸包会填充凹部，仍是简化形状。CSV/Gizmos 继续使用原盒子，保持比较口径，不等于新凸包或真实网格的精确表面。
- 相对 P2-A，腕位姿只改变每侧 1 mm 横向、2 mm 前后，并绕掌轴转 15°。两掌采样中心间距约 33.95→35.82 mm，没有改机械握点或大幅分开双手。
- 身体向工作肘侧移，负角端 160 mm、正角端 20 mm，在原姿态上减少前倾 13°→7°。原预制片段、Effort Blend Tree 和肘部支撑继续执行。骨长、腕目标和肘垫位置保持；新模式支撑肘骨高度按蒙皮表面补偿 5 mm。这是演示人物摆位，未经实物标定。
- 双腕相对位姿、指骨端点、Finger Start 改变时重算接触；共同握点下的刚性运动复用结果。相对变化超过 0.5 mm 或 0.05° 会使缓存失效。仅处理当前配对双手，不能阻挡任意外部物体。

实测放弃了近乎平行的掌轴、单纯侧移身体、反转掌面：分别导致合掌早停、失去肘部支撑、背手相对。屈指方向验证了原 dorsal 方向，最终没有反转该方向。

## 实际 Editor 对照

同机位、同光照、同角度/Grip Anchor/动画相位，各 181 帧、30 Hz，−30°→0°→+30°→−30°。两组 CSV 的机械握点 XYZ 和动画相位完全一致。旧代理保持同一尺寸；新掌凸包仅参与闭合。

| 全程指标 | 首轮 LegacyContactGrip | 本次 ContactGrip |
| --- | ---: | ---: |
| 手部旧代理对数 | 19 | 16 |
| 手部旧代理最大穿入 | 10.78 mm | **15.27 mm（回退）** |
| 对手/玩家蒙皮点相交估计均值 | 27.25 / 15.76 | 7.55 / 5.98 |
| 对手/玩家蒙皮点相交估计峰值深度 | 18.05 / 16.64 mm | 17.40 / 14.42 mm |
| 胸臂代理最大穿入 | 71.37 mm | 16.62 mm |
| 胸臂代理平均穿入 | 36.99 mm | 4.61 mm |
| 对手腕目标最大误差 | 0.0186 mm | 0.0180 mm |
| 对手/玩家腕最大弯曲 | 26.26° / 23.16° | 27.06° / 23.24° |
| 肘部水平轨迹跨度 | 96.35 mm | 98.75 mm |
| 最大逐帧肘位移 | 3.84 mm | 4.11 mm |
| 肘表面采样最小间隙 | −13.97 mm | −10.59 mm |
| 肘表面采样平均间隙 | −3.70 mm | −3.65 mm |

胸臂在零位没有 >2 mm 的旧代理接触，负角仍有袖口附近交叠。初版坐姿使平均肘表面额外压入约 4.5 mm，最终仅在新模式将支撑肘骨目标从 130 提至 135 mm；用同一蒙皮采样复测后平均间隙恢复，最差穿入减少约 3.38 mm。轨迹跨度增约 2.40 mm、最大逐帧位移增约 0.27 mm，没有观察到明显抖动；仍不能称真实压力接触。没有采用单纯侧移候选约 99 mm 失支撑的布局。四指更自然包覆，拇指朝对方食指靠拢，侧面比正面可信。虎口仍有可见穿插。

蒙皮指标来自既有 389 点/手的最近点与法线估计，折叠处可误判符号，不是三角形相交测试。新凸包求解起点仍报告约 13.11 mm 最大接触深度，接触限制没有消除这些已有穿入。不能只挑相交点数下降宣称解决。

反向查询次序重跑 181 帧，停止关节数均为 24（八指各三关节）；几何/碰撞指标差异小于 0.002 mm，腕弯曲差小于 0.001°，未观察到先求玩家或对手的可见偏置。新姿态本身具有上下搭扣差异，不要求形态镜像对称。

接触缓存全程构建一次；逆序测试因切换测试开关主动重建。Windows Editor 实测缓存后的指骨求解平均 0.062 ms，包含初始化的峰值 29.90 ms，其中一次闭合构建 19.48 ms。含原诊断的整次 Evaluate 平均 19.81→0.28 ms，181 帧缓存后采样期间 Evaluate 最大 1.02 ms。此为当前 Editor CPU 方法计时，不含整帧渲染，不代表 PICO 帧率。

Unity 中国版 2022.3.62f3c1、Windows Direct3D11 下，新 ContactGrip、LegacyContactGrip、P2-B Correct、P2-A、P1-E、胶囊六种模式均实际进入主场景 Editor Play，完成选角、倒计时、暂停/继续和结算，RuntimeErrors=0。新模式还断言使用本次保存的双腕目标、有效 open/closed 资产、接触停止和原对抗片段。

审查发现新资产缺失时会错误沿用新算法，已修复为资源与算法一起降级 Legacy。临时移出本轮新姿态资产及 meta 后实际比赛通过，日志确认 LegacyContactGrip、RuntimeErrors=0；finally 恢复并核对两个文件 SHA256 相同。原 Ch03、裁切模型与旧视频没有移动。

首轮完整记录见 [41ef8ae 的结果文档](https://github.com/Ihtbfas/armwrestling-vr/blob/41ef8ae/docs/P2C-physical-interaction-result.md)。受限人物资源及 P1-D/P1-E 视频保留本机，未加入提交。PICO、PCVR、真实机械、UDP 和头显性能未测试。

## 编辑与回退

Runtime Bootstrap 的 **Paired Collisions**：`ContactGrip` 使用新资产和坐姿；`LegacyContactGrip` 恢复首轮 P2-C 完整模式；`Off/Diagnose/Correct` 使用原 P2-A/P2-B 资产。关闭 **Use Paired Grip** 使用胶囊，本地 Ch03 按 README 恢复 P1-E。新接触资产缺失或不完整时自动降级 LegacyContactGrip，并给出警告。

菜单 **扳手腕 → P2-C → Open Contact Pose Preview** 创建双手预览。选中预览根节点，调整双腕和指骨后 **Save Selected Closed Endpoints**。用 **Load Open Endpoints In Selected Preview** 切换 open，调整后 **Save Selected Open Endpoints**。资产 Inspector 可调 Finger Start。Create Contact Pose Draft 保留已有资产，只有 `-p2crebuild` 明确要求才重建草稿。不要把预览人物保存进主场景。

关闭同工程的其他 Editor 后复现，不加 `-quit/-nographics`：

```powershell
$unity = "D:\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe"
& $unity -batchmode -projectPath "$PWD" -executeMethod ArmWrestlingVR.EditorTools.AnimationComparison.Run -p2c -logFile "$PWD\.p0-local\p2c-contact\capture.log"
& $unity -batchmode -projectPath "$PWD" -executeMethod ArmWrestlingVR.EditorTools.P0Validation.Run -p0contact -logFile "$PWD\.p0-local\p2c-contact\play.log"
```

采集追加 `-p2clegacy` 对照首轮，`-p2creverse` 反转查询次序；`-p2cpose` 仅预览 closed，不能用来证明接触限制生效。比赛回归：`-p0contact/-p0contactlegacy/-p0collision/-p0pairedgrip/-p0handcontact`，无参数为胶囊。

## 本机证据

项目根目录下：

- `.p0-local/p2c-contact/20261119-145142/`：首轮模式完整对照，`legacy/` 连续帧和 CSV。
- `.p0-local/p2c-contact/20261119-151827/`：当前 `contact/` 零位及 ±30° 第一人称、手近景、正侧面，181 帧和 CSV；`hands-before-after.mp4`、`body-before-after.mp4`、`first-person-before-after.mp4`；`hands-before-after.png` 和 `body-before-after.png`。
- `.p0-local/p2c-contact/20261119-151907/`：反向查询测试画面和 CSV。
- `.p0-local/p2c-contact/refine-*.log` 及对应 `.txt`：本轮采集、比赛回归日志；`.p0-local/p2c-refinement/`：修订前通用截图/报告备份及资源缺失测试临时目录（测试后资源已还原）。

三段视频为采集帧按 30 fps 编码、约 6 秒，非头显实时录屏。所有证据只保存在本机。

下一步应使用可编辑配对姿态精修虎口的蒙皮和端点关系，先减少闭合起点交叠，再评估严格接触限制。保留胸臂布局，结合实物测量统一标定，不建议继续扩大自动旋转搜索。
