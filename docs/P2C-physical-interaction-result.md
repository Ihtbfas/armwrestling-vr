# P2-C 主场景接触握持集成

打开 `Assets/Scenes/ArmWrestlingMain.unity`，用 Unity 中国版 `2022.3.62f3c1` 进入 Play。场景已保存 **Use Paired Grip = true**、**Paired Collisions = ContactGrip**，使用 Rocketbox 和原有离线选角、准备、倒计时及比赛流程。

在 Runtime Bootstrap 中改 **Paired Collisions** 可对照 P2-A（Off）、P2-B（Diagnose／Correct）。关闭 **Use Paired Grip** 使用胶囊；恢复本地 Ch03 后按旧三项开关可使用 P1-E。Rocketbox 和 MIT 许可沿用 P2-A。本轮仅增加原创代码与主场景设置，原始人物资源和本机证据继续保留。

## 选择与实现

查阅 Unity XRI 的刚体跟随、Auto Hand 的预制手势和指节闭合、Adaptive Hands 的逐指遇障停合。选择动画驱动的接触闭合，沿用 Unity 2022.3 的 Collider 查询。机械握点要求腕部跟随，因此本轮采用这一条路线完成主场景集成。未安装商业插件或 Unity 6 包。 [Unity XRI](https://github.com/Unity-Technologies/XR-Interaction-Toolkit-Examples/blob/main/Documentation/PhysicsInteractables.md) · [Auto Hand](https://earnest-robot.gitbook.io/auto-hand-docs/auto-hand/hand) · [Adaptive Hands](https://github.com/jeremiah-stevens/adaptive-hands)

每次 Animator IK 后恢复原配对腕位姿和指骨。根据原手势计算有限张开姿态，再沿张开到配对握姿的路径逐关节闭合，每段 24 步。每步用 PhysX 查询当前指段与对方手部，超过接触容许深度时保留上一有效步。当前帧初始已有穿入的部位以张开姿态深度为上限，允许 0.1 mm 查询容差。因此它能阻止继续加深，已有的掌根穿入仍可能保留。记录停止关节数和被拒绝步的超额深度。该过程每帧从同一原姿态重算，配对手骨回调按 IK 次数去重。 [Physics.ComputePenetration](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Physics.ComputePenetration.html)

对手胸部候选在 ±3° 范围旋转并重解两臂，保持骨长和腕目标。只有代理重叠减少、双肘位移不超过 8 mm、下沉不超过 1 mm、腕偏差小于 0.04 mm 的候选可使用。无约束试跑曾造成约 81 mm 的肘垫采样穿入，已放弃该版本。最终约束仅允许很小的胸部调整，胸臂问题改善有限。

## 实际 Editor 验证

Windows Direct3D11，主场景，双方同一模型、机位、机械角与片段相位。A 使用 P2-A 姿态加 Diagnose，B 使用 ContactGrip，各 181 帧、30 Hz，−30°→0°→+30°→−30°。保存第一人称、手侧近景、对手正侧面和诊断图。

| 181 帧统计 | A：原配对姿态 + Diagnose | B：主场景 ContactGrip |
| --- | ---: | ---: |
| 手部代理重叠对数 | 30 | 19 |
| 手部代理最大穿入 | 15.95 mm | 10.78 mm |
| 因接触停止的指关节 | 0 | 每帧 10 |
| 被拒绝闭合步的超额深度 | — | 约 0.285 mm |
| 对手腕目标最大误差 | 0.0185 mm | 0.0186 mm |
| 玩家腕最大弯曲 | 23.16° | 23.16° |
| 对手腕最大弯曲 | 26.25° | 26.26° |
| 肘部水平轨迹跨度 | 96.89 mm | 96.35 mm |
| 最大逐帧肘位移 | 3.84 mm | 3.84 mm |
| 肘表面采样最小间隙 | −14.33 mm | −13.97 mm |
| 胸臂代理峰值穿入 | 71.37 mm | 71.37 mm |

角度、Grip Anchor XYZ 与动画相位在 A/B CSV 中完全一致，两臂骨长差低于 0.001 mm。肘表面指标为既有蒙皮采样相对演示垫面的结果，不是肘关节原点。胸臂平均代理深度仅从 37.15 降至 36.99 mm，不能宣称已解决。

实际近景里，部分扎入对方手背的指节抬起，双腕仍在原配对位置。整体握姿仍偏张开，尚未达到自然扣握的成品效果。已有拇指根部和部分指节仍交叠，若将指节全张开会破坏握姿，当前保留有限闭合。胸部与袖口的改善较小，负角端仍明显。代理体与蒙皮采样只能用于定位，不能证明真实皮肤无穿插。

主场景 ContactGrip、P2-B Correct、P2-A、P1-E、胶囊均实际进入 Editor Play，完成选角、倒计时、暂停／继续和结算，运行错误均为 0。ContactGrip 回归额外确认主场景保存了新模式、接触查询执行、关节因实际查询被停止及对抗片段播放。PICO、PCVR、实物尺寸、UDP 和设备性能本轮未测。

## 本机证据与复现

| 本机目录（项目根目录下） | 内容 |
| --- | --- |
| `.p0-local/p2b-collision/20261119-115513/` | 最终对照 A，三角度与连续帧、碰撞明细和测量 CSV |
| `.p0-local/p2c-contact/20261119-115546/` | B，三角度、连续帧、CSV、`contact-before-after.mp4`（手部）、`body-before-after.mp4`（正侧面）和 `contact-keyframes.png` |
| `.p0-local/p2c-contact/` | 最终采集日志与五组 `*-play-final.log.txt` 比赛报告 |

关闭同一工程的其他 Editor 实例后可复现：

```powershell
$unity = "D:\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe"
& $unity -batchmode -projectPath "$PWD" -executeMethod ArmWrestlingVR.EditorTools.AnimationComparison.Run -p2c -logFile "$PWD\.p0-local\p2c-contact\capture.log"
& $unity -batchmode -projectPath "$PWD" -executeMethod ArmWrestlingVR.EditorTools.P0Validation.Run -p0contact -logFile "$PWD\.p0-local\p2c-contact\play.log"
```

下一步优先在实际第一人称观察剩余掌根相交，再编辑配对手势的起始位置和局部张开姿态。胸臂与肘垫需要联合摆位，现有几何下单纯转胸的空间有限，应结合真实装置测量再处理。
