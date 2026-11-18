# P1-A 虚拟对手动画路线对照

使用 Windows 上的 Unity 中国版 `2022.3.62f3c1`、原 `ArmWrestlingMain` 场景和本地 Ch03。建议下一轮沿用 **B 的三档身体姿态叠加 A 的手脚 IK**。B 保留了握持精度，肩、头和躯干能随对抗方向移动。两条路线都还需要校准肘垫几何和手掌姿态。

## 实现

- **A，固定坐姿 + Animator IK。** 修复前在 −30° 实测 IK 回调为 0，掌心代理点离握持点 773 mm。开启原控制器 Base Layer 的 IK Pass，并把修改后的 layer 数组写回 controller，保证重新导入时也能持久化。保留原 Idle 状态，微调 Ch03 相对原座椅的摆位、固定前倾、左手支撑与双脚目标。右手以同一摆臂 grip 为掌心参考，手腕目标沿指向退回约 45 mm，肘垫位置用于 IK hint。脚部、身体姿态和 hint 使用装置坐标。
- **B，三档身体姿态 + 接触 IK。** 对手占优、持平、玩家占优分别使用视觉角 −30°、0°、+30°。在 `OnAnimatorIK` 中对自制的身体平移和倾转姿态分段平滑插值。手掌、支撑手、脚和肘部 hint 使用 A 的同一套 IK。中立档与 A 完全一致。没有引入新插件或第三方动画曲线。
- 主场景默认仍走 P0 的离线模拟。启用本地 **Use Original Art** 后，运行时选中 Ch03，可用 **Blend Body Poses** 在 A 与 B 间切换。开关默认关，保持最小实验入口。

Unity 文档：[OnAnimatorIK](https://docs.unity.cn/ScriptReference/MonoBehaviour.OnAnimatorIK.html)、[AnimatorController.layers](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Animations.AnimatorController-layers.html)、[Animator.SetIKRotation](https://docs.unity.cn/Documentation/ScriptReference/Animator.SetIKRotation.html)。

## Editor Play 结果

两条路线在同一次 Editor Play 中使用同一模型、原桌面和肘垫、相同 grip、同一组灯光和正侧镜头。采集时由工具接管视觉角，保留 `VirtualArmRig.ApplyAngle`，遮掉比赛 UI 和鼠标射线便于看关节。角度从 −30° 平滑往返到 +30° 再回到 −30°，每条路线 181 帧，30 Hz。正视觉角为玩家占优方向，数值是实验输入，尚未与实物 rad、轴或握持位置标定。

| 视觉角 | A 右肘 ΔX / 高度 mm | B 右肘 ΔX / 高度 mm | A / B 右腕折角 |
| --- | ---: | ---: | ---: |
| −30° | −13 / 60 | −55 / 40 | 20.4° / 5.8° |
| 0° | +106 / 98 | +106 / 98 | 18.2° / 18.2° |
| +30° | +195 / 124 | +163 / 82 | 27.1° / 4.1° |

肘部 ΔX 以原对手肘垫中心为零，高度是肘关节骨原点到垫面。肘垫宽 240 mm、深 180 mm。A 和 B 的肘骨水平投影分别有 96/181、98/181 帧在垫面范围内。这个指标同时受骨骼和摆位影响，不能代表皮肤与肘垫的接触。腕折角由前臂方向和手腕到中指根的方向计算，掌心代理点取手腕到中指根连线的 55%，未做网格碰撞测量。

- **手掌。** A、B 每个采样步都实测 1 次 IK 回调。掌心代理点到 grip 最大 3.31 mm，手腕到各自目标误差小于 0.01 mm。两种方案仍可见张指和球形玩家手的互穿，后续需要做拇指、掌面与手指的接触姿态。
- **肩、肘与坐姿。** A 从两端观察，肩和头的位置固定，正角端手臂接近伸直，视觉上像身体没有参与。B 两端的肩移动约 129 mm，头移动约 159 mm。正角端肘抬高从 124 mm 降到 82 mm，肘的投影仍超出垫右边约 43 mm。侧视可看到座椅上的屈髋坐姿，脚底贴地、腿部与座椅/桌缘穿插仍待检查。
- **连续性。** 右手在各条路线相邻采样间最大位移约 5.5 mm，右肘 A 约 4.8 mm、B 约 6.6 mm。采样轨迹连续。B 的肩颈出力感更强，正角端前臂仍会挡脸。首次试验的 B 负端肘折角接近 0°，收敛端点前倾、侧倾和旋转后，最终负端约 71°。
- **成本。** A 只需要有效的 Humanoid、IK Pass 和人工摆好的目标。B 共用同一套求解，只多三档姿态和一次插值，但姿态依赖模型臂长与座椅几何，换人物时要重调。

P0 默认胶囊和启用本地原美术各进行一次离线 Editor Play 回归。两次的角色选择、准备、倒计时、暂停/继续、结算均通过，运行时错误计数为 0。本轮 P1 测量没有连接真机或 PC 控制端。

## 复现与证据

先按 [资产恢复说明](third-party-assets.md) 恢复本地 Ch03。在仓库根执行以下命令。运行结束后可按需用 FFmpeg 将 `A`、`B` 目录中的 `f-%03d.jpg`、`s-%03d.jpg` 以 30 fps 编码，或者使用已保存的四宫格视频。

```powershell
New-Item -ItemType Directory .p0-local/p1-animation -Force | Out-Null
& "D:\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe" -batchmode -projectPath "$PWD" -executeMethod ArmWrestlingVR.EditorTools.AnimationComparison.Run -logFile "$PWD\.p0-local\p1-animation\ab.log"
```

用 `-p1stills` 可跳过视频序列并保留三点正侧 PNG 与全程测量。最终本机证据位于 `.p0-local/p1-animation/20261118-124358/`，视频名 `A-left_B-right_front-top_side-bottom.mp4`，左侧 A、右侧 B、上排正面、下排侧面。每条路线 `front/side-000/045/090.png` 给出 −30°、0°、+30°。CSV 文件名为 `measurements.csv`。原基线放在同级 `baseline/`。图片、视频和数值原始输出都在 Git 忽略目录。

## 下一步

先用模型前臂长度约 242 mm 约束 grip、肘垫可达区域和椅子相对桌面的位置。确定肘在垫面的可行姿态，再细调握手朝向和左手支撑。随后只在能改善接触的前提下扩充姿态，以同一套三点与往返实验复查。设备上线前仍需另做真实机械角标定。
