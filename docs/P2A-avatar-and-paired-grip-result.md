# P2-A Rocketbox 人物与成对扳手腕握姿

## 运行

1. 用 Unity 中国版 `2022.3.62f3c1` 打开项目和 `Assets/Scenes/ArmWrestlingMain.unity`。
2. 在 **Runtime Bootstrap** 勾选 **Use Paired Grip** 后 Play。使用原来的选角、准备和倒计时流程。按空格可启停自动对战。
3. 关闭 **Use Paired Grip** 可回到现有模式。三项旧开关 **Use Original Art**、**Use Character Motion**、**Use Hand Contact** 对应本地 Ch03 的 P1-E。关闭人物开关使用胶囊。

### 人物及授权

使用 Microsoft Rocketbox 的 `Male_Adult_01`，上游固定提交 `0943055db6ec570bcef9f2c8b41c9e5467c808f9`。同一个 Humanoid 用于对手全身和玩家右臂，玩家使用较暖的材质色调区分。只导入该模型与三张实际用到的贴图，身体和头部色图从 2048 缩为 1024，透明部件保留 512 的 RGBA 图。资产目录含 FBX、贴图、三个材质、配对姿态及原 MIT 许可（Copyright © 2020 Microsoft），总计约 1.3 MB。 [Rocketbox 来源](https://github.com/microsoft/Microsoft-Rocketbox/tree/0943055db6ec570bcef9f2c8b41c9e5467c808f9/Assets/Avatars/Adults/Male_Adult_01) · [随附许可证](../Assets/ArmWrestling/Resources/P2A/ROCKETBOX-LICENSE.txt)

Rocketbox 的官方全局导入脚本默认选 Generic。本工程通过 **扳手腕 → P2-A → Prepare Rocketbox Model** 只配置这一份 FBX 的 Humanoid 映射和材质。实测 `Animator.isHuman = true`、Avatar 有效，右手 15／15 指骨有非零蒙皮权重，单蒙皮网格 4,811 顶点。Editor Play 近景可见肤色、脸、眼睛、头发、衣服和手指屈曲。玩家启动时从同一 FBX 的蒙皮提取右臂并保留 UV 和相应材质。两侧等比 0.94，以匹配现有演示桌椅和手臂可达范围。 [上游导入脚本](https://github.com/microsoft/Microsoft-Rocketbox/blob/0943055db6ec570bcef9f2c8b41c9e5467c808f9/Assets/Editor/FixRocketboxMaxImport.cs)

## 配对姿态

参考扳手腕比赛的对掌开局，以及 Meta Hand Grab Pose 的物体相对姿态和 Blender 的单帧、多骨架 Pose Asset。使用 Unity 的姿态资产保留两腕、两组手指信息，沿用项目已有 Animator 和 IK。 [TAWF 握姿规则](https://www.tawf.ca/league/learn/arm-wrestling-rules-and-championships) · [Meta Hand Grab](https://developers.meta.com/vr/documentation/unity/unity-isdk-hand-grab-interaction/) · [Blender Pose Library](https://docs.blender.org/manual/en/4.2/animation/armatures/posing/editing/pose_library.html)

- 先在零位布置两个右手腕及掌面。在同一画面下修订腕轴和间距。指节通过编辑器导向点摆出对方手背的包握路线，两个拇指分别朝对方食指根部的虎口搭扣。
- 保存到 `NeutralPairedGrip.asset`。每侧保存相对共同 `Grip Anchor` 的腕位置和旋转，以及 15 个指骨的局部四元数。可在空场景用 **Open Neutral Grip Preview** 打开双手预览，编辑人物根节点和指骨，再选中预览根节点用 **Save Selected Neutral Grip** 烘焙。
- Play 中只将保存的腕位姿随现有 `Grip Anchor` 变换，并恢复指骨局部姿态。保留 `SeatedPrepare`、`SeatedContest` 和 Effort 混合。对手沿用 Animator IK，由腕目标和实际前臂长度求肘垫上的可达位置。玩家沿用双骨求解，在能触达肘垫时将支撑纳入手臂随动。

## Editor Play 验证

Windows Direct3D11 的主场景先复核中立特写，再用相同的机械角度往返 6 秒、181 帧，保存 −30°／0°／+30° 和连续画面。与 P1-E 使用相同机位和角度输入。对手和玩家腕部从零位的配对姿态整体随动，±30° 的对手肘部支撑求解在全部 181 帧生效。

| ±30° 核查项 | P2-A |
| --- | ---: |
| 对手腕到预设目标最大误差 | 0.019 mm |
| 玩家掌心代理到预设目标最大误差 | <0.001 mm |
| 双手掌心代理距离 | 33.94–33.97 mm |
| 0° 玩家／对手腕折角 | 3.1°／3.9° |
| 全程玩家／对手最大腕折角 | 23.2°／26.3° |
| 对手肘骨水平最大点间距 | 96.9 mm（P1-E 为 48.6 mm） |
| 对手肘下缘相对垫面的采样高度 | −14.3 至 +4.9 mm |

零位和扫角使用同一姿态资产。两手代理距离验证相对布局随握点保持稳定。肘部数据来自局部蒙皮采样，负值表示采样点位于垫面以下。

实际近景里，人物比例、衣服和皮肤清晰，拇指搭向虎口，四指从侧面包向另一手。零位侧面仍有局部穿插，个别指尖略松，端点腕部的折角比零位大。肘垫表面也仍有局部穿入，玩家手臂作为局部模型在宽侧视有袖口边缘。这里交付的是单套握姿原型。头显、真机、实体标定、PC UDP 与终端性能本轮未测。

P2-A、P1-E、默认胶囊三种模式均进入实际 Editor Play，跑完选角、倒计时、暂停、继续和结算，运行错误均为 0。P2-A 比赛中确认播放对抗片段。

### 本机证据

| 目录（相对项目根目录） | 内容 |
| --- | --- |
| `.p0-local/p2a-avatar/20261118-205104/p2a/` | 单独验证的 0° 第一人称特写、手部正侧近景和对手正侧面 |
| `.p0-local/p2a-avatar/20261118-205241/` | `paired-grip-sweep.mp4`、`p1e-p2a-keyframes.png`、三角度 PNG 和 `measurements.csv` |
| `.p0-local/p2a-avatar/` | `prepare-paired.log`、`paired-final.log`、`paired-play-final.log`、`p1e-play-final.log`、`capsule-play-final.log` |
| `.p0-local/p1e-hand-contact/20261118-163547/p1e/` | P1-E 同机位历史对照 |

对照画面含 Ch03，留在本机。复现时先关闭使用同一项目的 Editor。

```powershell
$unity = "D:\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe"
New-Item -ItemType Directory -Force .p0-local/p2a-avatar | Out-Null
& $unity -batchmode -projectPath "$PWD" -executeMethod ArmWrestlingVR.EditorTools.AnimationComparison.Run -p2a -logFile "$PWD\.p0-local\p2a-avatar\paired-sweep.log"
& $unity -batchmode -projectPath "$PWD" -executeMethod ArmWrestlingVR.EditorTools.P0Validation.Run -p0pairedgrip -logFile "$PWD\.p0-local\p2a-avatar\paired-play.log"
```

第一条可追加 `-p2aneutral -p1stills` 只看零位。第二条的 `-p0handcontact` 可跑 P1-E。省略模式参数可跑胶囊。

下一步优先让使用者在头显中看这套固定握姿，确认哪些手指接触在实际眼位最明显，再用 pose preview 修改关键骨骼。实物腕轴和肘垫数据可用于随后核对端点腕部姿态及支撑。
