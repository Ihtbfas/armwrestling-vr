# P2-E 美术握姿实拍对照

Unity 2022.3.62f3c1、Windows Direct3D11、真实 `ArmWrestlingMain` Editor Play。Before 是稳定 P2-D，After 是可选 P2-E Contest。PNG 左旧右新，GIF 上旧下新。两边使用相同人物、镜头、机械输入及动画相位。画面由原帧拼接、缩放、标注和压缩。

只发布 MIT Microsoft Rocketbox 和原创场景的画面。[Microsoft MIT](ROCKETBOX-LICENSE.txt) · [DejaVu](DEJAVU-LICENSE.txt) · [版本、输入和源帧哈希](manifest.json) · [本轮结果与编辑方法](../../P2E-authored-grip-result.md)

## 零位与两组端点

![零位第一人称同镜头对照](neutral-hands.png)

![零位手部斜正面](neutral-grip-front.png)

![零位手部侧面](neutral-grip-side.png)

![零位手部反侧](neutral-grip-back.png)

![零位正面坐姿](neutral-front.png)

![零位全身侧视](neutral-side.png)

![同一场景的 Ready 和 Contest 两份姿态](ready-contest.png)

## 三角度关键帧和连续运动

各图从上到下是 −30°、0°、+30°。

![三角度第一人称](keyframes-hands.png)

![三角度正面](keyframes-front.png)

![三角度侧面](keyframes-side.png)

![连续双手运动，约六秒](hands-motion.gif)

![连续身体侧视，约六秒](body-side-motion.gif)

新姿态的四指向掌背包拢，拇指向虎口收起。玩家第一人称仍能看到松握，反侧还有局部指间交叠。腕姿与原来接近，Rocketbox 手部代理相交增加，主场景继续默认使用 P2-D。Ch03 和混合角色的画面仅在本机。
