# P1-C 实物几何交接与 Ch03 握姿预研

实物装置尚未测量。现场人员可以直接用 [实物测量指南](calibration/physical-measurement-guide.md) 中的步骤和待填表，记录输出轴线、带角度的握持轨迹、肘垫和椅面位姿。指南把本地 CAD 零件 STL 外形、课题初步设计、PC MuJoCo 模型和 Unity P1-B 演示分别列为不同来源。

## 手部实验

Ch03 的右手五指共 **15 个 Humanoid 骨骼全部映射**，包含三节拇指。试验在 Animator 完成 Idle 求值后读取手指实际局部旋转与弯曲轴，再在原 IK pass 内写入绝对旋转。早期直接以导入绑定旋转为基准的样本反而伸直手指，因而改用求值后的状态。最终使用轻量静态合指和拇指弯曲，保留 B 躯干姿态、腕目标、肘垫引导和握持圆弧。使用的是 [Unity Animator.SetBoneLocalRotation](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Animator.SetBoneLocalRotation.html)。

握姿目前为本地可选实验。打开主场景，选 Runtime Bootstrap，勾选 **Use Original Art** 与 **Use Experimental Grip** 再进入 Play。只勾选前者时延续 P1-B。

## Editor Play 对照

Unity 中国版 **2022.3.62f3c1 / Direct3D11**。同一次 Play 先采 P1-B，再采启用握姿。各 181 帧、30 Hz，角度从 −30° 到 +30° 往返，另复测 ±42°。每个采样步实测 1 次 IK 回调。腕骨、肘骨和握点位置逐帧比较到 CSV 精度。

| 指标 | P1-B → 握姿（±30°） | P1-B → 握姿（±42°） |
| --- | ---: | ---: |
| 食指／中指近端指间屈曲 | 38.7° → 47.7° ／ 40.3° → 49.3° | 同左 |
| 拇指两骨段夹角 | 26.6° → 32.6° | 同左 |
| 指部采样点落入玩家手球的帧数 | 38 → 34／181 | 32 → 26／181 |
| 指部最深采样穿入 | 1.05 → 0.58 mm | 1.96 → 1.45 mm |
| 掌部最深采样穿入 | 13.04 → 13.13 mm | 18.09 → 18.17 mm |
| 掌心代理点到握点的最大误差 | 3.31 mm → 3.31 mm | 3.31 mm → 3.31 mm |
| 腕折角峰值 | 13.49° → 13.49° | 18.33° → 18.33° |
| 肘部蒙皮采样间隙 | 0.58–3.71 mm 前后相同 | −0.71 至 +3.72 mm 前后相同 |

测量用每帧 BakeMesh。指部取由指骨主导的 1040 个顶点，掌部取由右手骨主导的 248 个顶点，计算顶点到玩家手球表面的有符号距离。指标用于观察两种姿态在同一代理形状下的差异，球体和采样点无法判断两只真实手的接触面。画面中手指弯曲略更自然，指部采样重叠略减；球形玩家手仍占据一部分掌面，掌部最大重叠甚至略增，故将实验留为可选。

P0 默认胶囊、P1-B 本地 Ch03 和启用实验握姿的本地 Ch03 均通过比赛流程回归，涵盖选角、倒计时、暂停、继续及结算，运行时错误为 0。实物接触、真实手追踪与 PICO 未在本轮测试。

## 复现和本机证据

恢复本地 Ch03 后从仓库根执行。沿用 P1-B 往返工具，额外保存正面与侧面的局部手部画面。加 `-p1bfullrange` 复测 ±42°，加 `-p1stills` 跳过逐帧 JPG。比赛流程可使用 `P0Validation.Run -p0grip`。

```powershell
New-Item -ItemType Directory .p0-local/p1c-grip -Force | Out-Null
& "D:\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe" -batchmode -projectPath "$PWD" -executeMethod ArmWrestlingVR.EditorTools.AnimationComparison.Run -p1c -logFile "$PWD\.p0-local\p1c-grip\grip.log"
```

本机 `.p0-local/p1c-grip/` 内保存原始结果。

- `20261118-141831/` 含三点正侧面和全身/局部 PNG、两种握姿的 CSV，以及 `original-left_grip-right_front-top_side-bottom.mp4`。视频左原姿态、右实验握姿，上正面、下侧面，181 帧约 6 秒。
- `20261118-141853/` 含 ±42° 限位的图片与测量。
- `final-p0-*.log` 和其对应的结果文本记录三次比赛回归。

下一步先拿到实物轴线、五个安全静态握持姿态及垫面/椅面，核对真实坐标和角度方向。随后用实测握把截面与握手参考重做玩家手代理，优先解决掌面交叠，再逐指细调。
