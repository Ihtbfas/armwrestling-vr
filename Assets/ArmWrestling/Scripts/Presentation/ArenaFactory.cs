using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArmWrestlingVR
{
    /// <summary>
    /// 场景与美术资源的程序化生成器。
    ///
    /// 整个场景（桌面、机械摆臂、双方手臂、对手人物、座椅、地面、操作面板）都由代码生成，
    /// 这样做的原因：
    /// 1. 没有任何外部模型、材质、预制体依赖，工程可以立即打开、编译、运行；
    /// 2. 几何尺寸集中在一处，机械组给出实测值后改常量即可；
    /// 3. 后续换成正式模型时，只需替换 <c>BuildArena</c> 中对应的物体。
    ///
    /// 所有坐标都在“装置坐标系”中：原点位于桌面中心上表面，+Y 向上，+Z 指向对手，
    /// +X 指向使用者右侧。
    /// </summary>
    public static class ArenaFactory
    {
        public const string SystemRootName = "ArmWrestling System";
        public const string DeviceRootName = "Device Frame";
        public const string PlayerRigName = "Player Rig";

        // 机械与人体尺寸（米）。与机械组确认后修改这里。
        public const float TableHeight = 0.75f;      // 桌面距地面
        public const float TableWidth = 1.30f;
        public const float TableDepth = 0.90f;
        public const float SwingLength = 0.30f;      // 摆臂有效长度（开题报告：300 mm）
        public const float SwingPivotHeight = 0.05f; // 转轴中心离桌面的高度
        public const float GripHeight = SwingPivotHeight + SwingLength;
        public const float PlayerSeatZ = -0.62f;
        public const float OpponentSeatZ = 0.62f;
        public const float PlayerShoulderX = 0.21f;
        public const float PlayerShoulderY = 0.17f;
        public const float PlayerShoulderZ = -0.40f;
        public const float ElbowPadX = 0.21f;
        public const float ElbowPadZ = -0.15f;
        public const float UpperArmLength = 0.30f;
        public const float ForearmLength = 0.28f;

        /// <summary>诊断开关：为 true 时不生成世界空间 UI（仅用于定位打包问题的临时手段）。</summary>
        public static bool SkipHudForDiagnostics;

        /// <summary>诊断开关：为 true 时不生成手臂 IK 骨骼。</summary>
        public static bool SkipArmsForDiagnostics;

        /// <summary>用导入的 HDRI 做天空盒和环境光；关掉则回到纯色竞技场。</summary>
        public static bool UseHdriSky = false;

        /// <summary>天空盒的朝向（度）。可用命令行 -skyrot 200 现场试角度。</summary>
        public static float SkyRotation = 0f;

        /// <summary>用导入的人物模型替代胶囊体对手。</summary>
        public static bool UseCharacterModel = false;

        // 配色
        private static readonly Color TableColor = new Color(0.27f, 0.145f, 0.085f);
        private static readonly Color TableEdgeColor = new Color(0.10f, 0.11f, 0.13f);
        private static readonly Color DarkMetalColor = new Color(0.16f, 0.17f, 0.19f);
        private static readonly Color PadColor = new Color(0.05f, 0.28f, 0.62f);
        private static readonly Color SkinColor = new Color(0.78f, 0.58f, 0.45f);
        private static readonly Color SleeveColor = new Color(0.10f, 0.20f, 0.42f);
        private static readonly Color OpponentShirtColor = new Color(0.62f, 0.16f, 0.16f);
        private static readonly Color OpponentSkinColor = new Color(0.72f, 0.52f, 0.40f);
        // 竞技场配色：整体压暗，让自发光灯带和暖色射灯跳出来
        private static readonly Color FloorColor = new Color(0.07f, 0.075f, 0.095f);
        private static readonly Color ArenaDiscColor = new Color(0.11f, 0.12f, 0.16f);
        private static readonly Color WallColor = new Color(0.10f, 0.11f, 0.15f);
        private static readonly Color ColumnColor = new Color(0.13f, 0.14f, 0.19f);
        private static readonly Color CeilingColor = new Color(0.025f, 0.03f, 0.045f);
        private static readonly Color LightPanelColor = new Color(1f, 0.95f, 0.85f);
        private static readonly Color BannerWarmColor = new Color(0.55f, 0.14f, 0.20f);
        private static readonly Color BannerCoolColor = new Color(0.10f, 0.30f, 0.62f);
        private static readonly Color StripColor = new Color(0.32f, 0.86f, 0.95f);
        private static readonly Color FogColor = new Color(0.015f, 0.03f, 0.06f);
        private static readonly Color ChairColor = new Color(0.10f, 0.11f, 0.14f);

        private static readonly Dictionary<Color, Material> MaterialCache = new Dictionary<Color, Material>();
        private static readonly Dictionary<Color, Material> EmissiveCache = new Dictionary<Color, Material>();

        // ------------------------------------------------------------------
        // 入口
        // ------------------------------------------------------------------

        /// <summary>构建完整场景并返回主控制器。</summary>
        public static ArmWrestlingApp Build(ArmWrestlingConfig config)
        {
            GameObject systemRoot = new GameObject(SystemRootName);
            systemRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            XrPlayerRig rig = BuildPlayerRig(systemRoot.transform, out Camera eyeCamera);

            GameObject deviceRoot = new GameObject(DeviceRootName);
            deviceRoot.transform.SetParent(systemRoot.transform, false);
            deviceRoot.transform.SetPositionAndRotation(new Vector3(0f, TableHeight, 0.62f), Quaternion.identity);

            VirtualArmRig armRig = BuildArena(deviceRoot.transform);

            HudPresenter hud = null;
            if (!SkipHudForDiagnostics)
            {
                hud = new GameObject("HUD").AddComponent<HudPresenter>();
                hud.transform.SetParent(deviceRoot.transform, false);
                // 世界空间 Canvas 的可见面朝向 -Z，玩家在 -Z 一侧，因此不要再绕 Y 旋转 180°，
                // 否则会看到背面（文字左右镜像）。绕 X 正向倾斜 = 面板向后仰，正对下方坐着的使用者。
                // 高度 0.98 m、距桌面中心 1.0 m：比初版更近更低，配合更大的字号提升可读性。
                hud.Build(deviceRoot.transform, new Vector3(0f, 0.98f, 1.00f), Quaternion.Euler(22f, 0f, 0f));
            }

            DeviceFrameCalibration calibration = deviceRoot.AddComponent<DeviceFrameCalibration>();
            calibration.deviceRoot = deviceRoot.transform;
            calibration.rig = rig;

            VrPointer pointer = systemRoot.AddComponent<VrPointer>();
            pointer.rig = rig;
            pointer.eyeCamera = eyeCamera;
            pointer.xrRaySource = rig.rightHand;

            ArmWrestlingApp app = systemRoot.AddComponent<ArmWrestlingApp>();
            app.config = config != null ? config : ArmWrestlingConfig.Load();
            app.rig = rig;
            app.calibration = calibration;
            app.hud = hud;
            app.armRig = armRig;
            app.deviceRoot = deviceRoot.transform;

            armRig.ApplyAngle(0f);

            // 自动化冒烟测试驱动：只有命令行带 -autotest 时才工作，正常运行时自动禁用。
            SmokeTestDriver smokeTest = systemRoot.AddComponent<SmokeTestDriver>();
            smokeTest.app = app;

            return app;
        }

        // ------------------------------------------------------------------
        // 玩家装置
        // ------------------------------------------------------------------

        private static XrPlayerRig BuildPlayerRig(Transform parent, out Camera eyeCamera)
        {
            GameObject rigObject = new GameObject(PlayerRigName);
            rigObject.transform.SetParent(parent, false);

            GameObject headObject = new GameObject("Head");
            headObject.transform.SetParent(rigObject.transform, false);
            headObject.transform.localPosition = new Vector3(0f, 1.15f, 0f);

            eyeCamera = headObject.AddComponent<Camera>();
            // 用了 HDRI 就让相机清成天空盒，否则天空永远看不到
            eyeCamera.clearFlags = UseHdriSky ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            eyeCamera.backgroundColor = new Color(0.015f, 0.03f, 0.06f);
            eyeCamera.nearClipPlane = 0.03f;
            eyeCamera.farClipPlane = 40f;
            if (!Application.isMobilePlatform)
            {
                // VR 模式下不能设置 FOV（Unity 会警告并忽略），只在桌面预览时给个宽一点的视野。
                eyeCamera.fieldOfView = 70f;
            }

            eyeCamera.tag = "MainCamera";

            headObject.AddComponent<AudioListener>();

            GameObject leftObject = new GameObject("Left Hand");
            leftObject.transform.SetParent(rigObject.transform, false);
            leftObject.transform.localPosition = new Vector3(-0.2f, 1.0f, 0.25f);

            GameObject rightObject = new GameObject("Right Hand");
            rightObject.transform.SetParent(rigObject.transform, false);
            rightObject.transform.localPosition = new Vector3(0.2f, 1.0f, 0.25f);

            XrPlayerRig rig = rigObject.AddComponent<XrPlayerRig>();
            rig.head = headObject.transform;
            rig.leftHand = leftObject.transform;
            rig.rightHand = rightObject.transform;
            return rig;
        }

        // ------------------------------------------------------------------
        // 场景内容
        // ------------------------------------------------------------------

        private static VirtualArmRig BuildArena(Transform deviceRoot)
        {
            CreateLighting(deviceRoot);

            // 必须放在 CreateLighting 之后：那边会把 ambientMode 设成 Flat，
            // 先设天空盒环境光会被它覆盖，结果整个舞台是黑的。
            if (UseHdriSky)
            {
                ConfigureSkybox();
            }
            BuildEnvironment(deviceRoot);
            BuildTable(deviceRoot);

            VirtualArmRig armRig = BuildArms(deviceRoot);
            BuildOpponentBody(deviceRoot, armRig);
            BuildSeats(deviceRoot);

            return armRig;
        }

        /// <summary>
        /// 用 Poly Haven 的 HDRI 当天空盒，并让环境光从它来。
        /// 一张 HDRI 就能给整个场景提供真实的方向光、色温和反射。
        /// </summary>
        private static void ConfigureSkybox()
        {
            Texture2D hdri = Resources.Load<Texture2D>("HDRI/circus_arena");
            if (hdri == null)
            {
                Debug.LogWarning("[ArmWrestling] 找不到 HDRI，回退到纯色环境");
                return;
            }

            // 用材质资产而不是 Shader.Find：Shader.Find 在打包后找不到内置天空盒着色器。
            Material skybox = Resources.Load<Material>("HDRI/SkyboxPanoramic");
            if (skybox == null)
            {
                Shader shader = Shader.Find("Skybox/Panoramic");
                if (shader == null)
                {
                    Debug.LogWarning("[ArmWrestling] 缺少 Skybox/Panoramic 着色器");
                    return;
                }

                skybox = new Material(shader);
                skybox.SetTexture("_MainTex", hdri);
                skybox.SetFloat("_Exposure", 1.15f);
                skybox.SetFloat("_Rotation", SkyRotation);
            }

            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.1f;
            DynamicGI.UpdateEnvironment();
        }

        private static void CreateLighting(Transform parent)
        {
            // 冷色方向光：只提供一点方向感和桌面接触阴影，主戏交给射灯与色光
            GameObject keyObject = new GameObject("Key Light");
            keyObject.transform.SetParent(parent, false);
            keyObject.transform.localRotation = Quaternion.Euler(58f, -34f, 0f);
            Light keyLight = keyObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(0.66f, 0.78f, 1f);
            keyLight.intensity = 0.45f;
            keyLight.shadows = LightShadows.Soft;
            keyLight.shadowStrength = 0.6f;

            // 桌面暖色射灯（视觉焦点）
            GameObject spotObject = new GameObject("Table Spot");
            spotObject.transform.SetParent(parent, false);
            spotObject.transform.localPosition = new Vector3(0f, 3.6f, 0.3f);
            spotObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Light spot = spotObject.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 9f;
            spot.spotAngle = 50f;
            spot.intensity = 3.6f;
            spot.color = new Color(1f, 0.87f, 0.70f);
            spot.shadows = LightShadows.None;

            // 冷暖对冲的两盏色光，打在墙面与立柱上
            AddPointLight(parent, new Vector3(-4.2f, 2.6f, 3.4f), new Color(0.22f, 0.62f, 1f), 1.15f, 10f);
            AddPointLight(parent, new Vector3(4.2f, 2.6f, 3.4f), new Color(1f, 0.28f, 0.46f), 0.95f, 10f);

            if (!UseHdriSky)
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.155f, 0.165f, 0.215f);

                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogDensity = 0.022f;
                RenderSettings.fogColor = FogColor;
            }

            // 雾：让远处的墙和天花退到暗处，空间立刻"深"下来
        }

        private static void AddPointLight(Transform parent, Vector3 position, Color color, float intensity, float range)
        {
            GameObject lightObject = new GameObject("Rim Light");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        /// <summary>
        /// 夜间竞技场：10 m × 12 m、层高 7 m 的大厅——场心圆台与环形灯带、两排带竖向灯带的立柱、
        /// 后墙发光徽标、顶部灯槽，整体靠自发光面片 + 三盏实时光营造冷暖对比。
        /// 全部是基本几何体，手机端开销可以忽略。
        /// </summary>
        private static void BuildEnvironment(Transform parent)
        {
            if (UseHdriSky)
            {
                BuildOpenFloor(parent);
                BuildTableSpot(parent);
                return;
            }

            const float hallHalfX = 5.0f;
            const float hallHalfZ = 6.0f;
            const float hallHeight = 7.0f;
            const float wallThickness = 0.2f;

            float floorY = -TableHeight;
            float ceilingY = floorY + hallHeight;

            Material stripMaterial = GetEmissiveMaterial(StripColor, 1.9f);
            Material panelMaterial = GetEmissiveMaterial(LightPanelColor, 1.35f);

            // 地面与场心圆台
            CreateBox(parent, "Floor", new Vector3(0f, floorY - 0.02f, 0f),
                new Vector3(hallHalfX * 2f, 0.04f, hallHalfZ * 2f), FloorColor);
            CreateCylinder(parent, "Arena Disc", new Vector3(0f, floorY + 0.006f, 0.2f),
                new Vector3(5.4f, 0.006f, 5.4f), ArenaDiscColor);

            // 场心环形灯带（16 段拼成圆环）
            for (int i = 0; i < 16; i++)
            {
                float angle = i * 22.5f;
                float rad = angle * Mathf.Deg2Rad;
                Vector3 position = new Vector3(Mathf.Sin(rad) * 2.62f, floorY + 0.016f, 0.2f + Mathf.Cos(rad) * 2.62f);
                GameObject segment = CreateBox(parent, "Ring Segment " + i, position,
                    new Vector3(0.92f, 0.014f, 0.1f), StripColor);
                segment.transform.localRotation = Quaternion.Euler(0f, angle, 0f);

                Renderer renderer = segment.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = stripMaterial;
                }
            }

            // 两排立柱 + 竖向灯带
            float[] columnZ = { -3.9f, -1.3f, 1.3f, 3.9f };
            for (int side = -1; side <= 1; side += 2)
            {
                foreach (float z in columnZ)
                {
                    CreateCylinder(parent, "Column", new Vector3(side * 3.9f, floorY + hallHeight * 0.5f, z),
                        new Vector3(0.9f, hallHeight * 0.5f, 0.9f), ColumnColor);

                    GameObject strip = CreateBox(parent, "Column Strip", new Vector3(side * 3.44f, floorY + hallHeight * 0.44f, z),
                        new Vector3(0.045f, hallHeight * 0.6f, 0.07f), StripColor);
                    Renderer stripRenderer = strip.GetComponent<Renderer>();
                    if (stripRenderer != null)
                    {
                        stripRenderer.sharedMaterial = stripMaterial;
                    }
                }
            }

            // 四面墙与天花
            float wallCenterY = floorY + hallHeight * 0.5f;
            CreateBox(parent, "Wall Back", new Vector3(0f, wallCenterY, hallHalfZ),
                new Vector3(hallHalfX * 2f, hallHeight, wallThickness), WallColor);
            CreateBox(parent, "Wall Front", new Vector3(0f, wallCenterY, -hallHalfZ),
                new Vector3(hallHalfX * 2f, hallHeight, wallThickness), WallColor);
            CreateBox(parent, "Wall Left", new Vector3(-hallHalfX, wallCenterY, 0f),
                new Vector3(wallThickness, hallHeight, hallHalfZ * 2f), WallColor);
            CreateBox(parent, "Wall Right", new Vector3(hallHalfX, wallCenterY, 0f),
                new Vector3(wallThickness, hallHeight, hallHalfZ * 2f), WallColor);
            CreateBox(parent, "Ceiling", new Vector3(0f, ceilingY + 0.1f, 0f),
                new Vector3(hallHalfX * 2f, 0.2f, hallHalfZ * 2f), CeilingColor);

            // 顶部灯槽
            for (int i = 0; i < 3; i++)
            {
                GameObject panel = CreateBox(parent, "Ceiling Light " + i,
                    new Vector3(0f, ceilingY - 0.1f, -2.6f + i * 2.6f),
                    new Vector3(hallHalfX * 1.3f, 0.05f, 0.7f), LightPanelColor);

                Renderer renderer = panel.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = panelMaterial;
                }
            }

            // 后墙徽标：发光圆环 + 两侧竖向条幅
            float emblemZ = hallHalfZ - 0.18f;
            GameObject emblem = CreateCylinder(parent, "Emblem Ring",
                new Vector3(0f, floorY + hallHeight * 0.66f, emblemZ), new Vector3(2.9f, 0.02f, 2.9f), StripColor);
            emblem.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Renderer emblemRenderer = emblem.GetComponent<Renderer>();
            if (emblemRenderer != null)
            {
                emblemRenderer.sharedMaterial = stripMaterial;
            }

            CreateCylinder(parent, "Emblem Core", new Vector3(0f, floorY + hallHeight * 0.66f, emblemZ - 0.03f),
                new Vector3(2.55f, 0.02f, 2.55f), WallColor)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            CreateBox(parent, "Banner Cool", new Vector3(-2.55f, floorY + 2.0f, emblemZ),
                new Vector3(0.34f, 2.6f, 0.03f), BannerCoolColor);
            CreateBox(parent, "Banner Warm", new Vector3(2.55f, floorY + 2.0f, emblemZ),
                new Vector3(0.34f, 2.6f, 0.03f), BannerWarmColor);
        }

        /// <summary>
        /// 不再铺整块地面——地面交给 HDRI 自带的场地。
        /// 这里只搭一块低矮的圆形舞台，让桌子和两个人"站在台上"，
        /// 并把舞台中心设在玩家与对手之间，看起来就像比赛放在马戏场中央。
        /// </summary>
        private static void BuildOpenFloor(Transform parent)
        {
            float floorY = -TableHeight;

            // 舞台：上表面与真实地面齐平，半径 4.2 m
            CreateCylinder(parent, "Stage", new Vector3(0f, floorY - 0.06f, 0.1f),
                new Vector3(3.4f, 0.03f, 3.4f), new Color(0.13f, 0.17f, 0.24f));

            // 舞台包边（略深一圈，边界更清楚）
            CreateCylinder(parent, "Stage Rim", new Vector3(0f, floorY - 0.055f, 0.1f),
                new Vector3(3.6f, 0.008f, 3.6f), new Color(0.09f, 0.11f, 0.16f));

            // 舞台中央的圈线，把视线引到桌子上
            CreateCylinder(parent, "Stage Ring", new Vector3(0f, floorY + 0.002f, 0.1f),
                new Vector3(3.0f, 0.003f, 3.0f), new Color(0.22f, 0.28f, 0.38f));
        }

        /// <summary>只给桌面打一盏暖色射灯，保证人物和桌子的亮度和背景协调。</summary>
        private static void BuildTableSpot(Transform parent)
        {
            GameObject spotObject = new GameObject("Table Spot");
            spotObject.transform.SetParent(parent, false);
            spotObject.transform.localPosition = new Vector3(0f, 3.2f, 0.4f);
            spotObject.transform.localRotation = Quaternion.Euler(84f, 0f, 0f);

            Light spot = spotObject.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 12f;
            spot.spotAngle = 78f;
            spot.intensity = 3.2f;
            spot.color = new Color(1f, 0.92f, 0.82f);
            spot.shadows = LightShadows.Soft;
            spot.shadowStrength = 0.7f;
        }

        private static void BuildTable(Transform parent)
        {
            CreateBox(parent, "Table Top", new Vector3(0f, -0.035f, 0f), new Vector3(TableWidth, 0.07f, TableDepth), TableColor);
            CreateBox(parent, "Table Trim", new Vector3(0f, -0.005f, 0f), new Vector3(TableWidth + 0.02f, 0.012f, TableDepth + 0.02f), TableEdgeColor);

            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * (TableWidth * 0.5f - 0.10f);
                float z = (i < 2 ? -1f : 1f) * (TableDepth * 0.5f - 0.10f);
                CreateBox(parent, "Table Leg " + i, new Vector3(x, -TableHeight * 0.5f - 0.03f, z),
                    new Vector3(0.055f, TableHeight - 0.08f, 0.055f), DarkMetalColor);
            }

            // 两侧肘垫
            CreateBox(parent, "Player Elbow Pad", new Vector3(ElbowPadX, 0.02f, ElbowPadZ), new Vector3(0.24f, 0.045f, 0.18f), PadColor);
            CreateBox(parent, "Opponent Elbow Pad", new Vector3(-ElbowPadX, 0.02f, -ElbowPadZ), new Vector3(0.24f, 0.045f, 0.18f), PadColor);
        }

        private static VirtualArmRig BuildArms(Transform deviceRoot)
        {
            // 摆臂：只保留用于计算的运动学节点（pivot + 握持点），不渲染任何机构。
            // 真实扳手腕时两人手之间没有连杆，画面里出现金属杆会很出戏。
            GameObject pivotObject = new GameObject("Swing Arm Pivot");
            pivotObject.transform.SetParent(deviceRoot, false);
            pivotObject.transform.localPosition = new Vector3(0f, SwingPivotHeight, 0f);
            pivotObject.transform.localRotation = Quaternion.identity;

            GameObject gripObject = new GameObject("Grip Anchor");
            gripObject.transform.SetParent(pivotObject.transform, false);
            gripObject.transform.localPosition = new Vector3(0f, SwingLength, 0f);

            VirtualArmRig armRig = deviceRoot.gameObject.AddComponent<VirtualArmRig>();
            armRig.swingPivot = pivotObject.transform;
            armRig.gripAnchor = gripObject.transform;
            armRig.swingLength = SwingLength;

            if (SkipArmsForDiagnostics)
            {
                return armRig;
            }

            armRig.playerArm = BuildArmChain(deviceRoot, "Player",
                new Vector3(PlayerShoulderX, PlayerShoulderY, PlayerShoulderZ),
                new Vector3(ElbowPadX, 0.05f, ElbowPadZ),
                SleeveColor, SkinColor);

            if (!UseCharacterModel)
            {
                armRig.opponentArm = BuildArmChain(deviceRoot, "Opponent",
                    new Vector3(-PlayerShoulderX, PlayerShoulderY, -PlayerShoulderZ + 0.02f),
                    new Vector3(-ElbowPadX, 0.05f, -ElbowPadZ - 0.02f),
                    OpponentShirtColor, OpponentSkinColor);
            }

            return armRig;
        }

        private static ArmChain BuildArmChain(Transform parent, string name, Vector3 shoulder, Vector3 elbowHint, Color sleeveColor, Color skinColor)
        {
            GameObject root = new GameObject(name + " Arm");
            root.transform.SetParent(parent, false);

            GameObject shoulderObject = new GameObject(name + " Shoulder");
            shoulderObject.transform.SetParent(root.transform, false);
            shoulderObject.transform.localPosition = shoulder;

            GameObject elbowObject = new GameObject(name + " Elbow Hint");
            elbowObject.transform.SetParent(root.transform, false);
            elbowObject.transform.localPosition = elbowHint;

            GameObject upper = CreateCapsule(root.transform, name + " Upper Arm", shoulder, Vector3.one * 0.11f, sleeveColor);
            GameObject fore = CreateCapsule(root.transform, name + " Forearm", shoulder, Vector3.one * 0.09f, skinColor);
            GameObject hand = CreateSphere(root.transform, name + " Hand", shoulder, Vector3.one * 0.11f, skinColor);

            ArmChain chain = new ArmChain
            {
                upperArm = upper.transform,
                forearm = fore.transform,
                hand = hand.transform,
                shoulderAnchor = shoulderObject.transform,
                elbowHint = elbowObject.transform,
                upperLength = UpperArmLength,
                forearmLength = ForearmLength,
                upperRadius = 0.055f,
                forearmRadius = 0.045f,
                handRadius = 0.052f
            };

            chain.Solve(shoulder, shoulder + Vector3.down * 0.35f, shoulder + Vector3.forward, Vector3.up, Quaternion.identity);
            return chain;
        }

        private static void BuildOpponentBody(Transform parent, VirtualArmRig armRig)
        {
            if (UseCharacterModel && SpawnCharacter(parent, armRig))
            {
                return;
            }

            BuildCapsuleOpponent(parent);
        }

        /// <summary>
        /// 生成导入的人物模型，放在桌子对面，用 IK 摆成坐姿。
        /// 返回 false 表示模型不可用（调用方回退到胶囊体）。
        /// </summary>
        private static bool SpawnCharacter(Transform parent, VirtualArmRig armRig)
        {
            GameObject prefab = Resources.Load<GameObject>("Characters/Ch03");
            if (prefab == null)
            {
                Debug.LogWarning("[ArmWrestling] 找不到人物模型 Resources/Characters/Ch03");
                return false;
            }

            GameObject model = Object.Instantiate(prefab, parent);
            model.name = "Opponent Character (Ch03)";

            const float scale = 0.95f;
            float feetY = -TableHeight;

            // The Humanoid Idle controller rebases the body to the model origin.
            // Put that origin at seat height; subtracting standing hip height again
            // placed the animated character entirely below the tabletop.
            model.transform.localPosition = new Vector3(0f, -0.30f, OpponentSeatZ + 0.30f);
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            model.transform.localScale = Vector3.one * scale;

            Animator animator = model.GetComponent<Animator>();
            if (animator == null)
            {
                animator = model.AddComponent<Animator>();
            }

            // 没有 Controller 就不会回调 OnAnimatorIK，模型会一直是 T-pose
            RuntimeAnimatorController ikController = Resources.Load<RuntimeAnimatorController>("Characters/OpponentIK");
            if (ikController != null)
            {
                animator.runtimeAnimatorController = ikController;
            }
            else
            {
                Debug.LogWarning("[ArmWrestling] 找不到 IK 控制器 Resources/Characters/OpponentIK");
            }

            Transform leftHand = new GameObject("Left Hand Rest").transform;
            leftHand.SetParent(parent, false);
            leftHand.localPosition = new Vector3(0.34f, 0.06f, OpponentSeatZ - 0.05f);

            Transform rightElbow = new GameObject("Right Elbow Hint").transform;
            rightElbow.SetParent(parent, false);
            rightElbow.localPosition = new Vector3(-0.34f, 0.24f, OpponentSeatZ + 0.16f);

            Transform leftElbow = new GameObject("Left Elbow Hint").transform;
            leftElbow.SetParent(parent, false);
            leftElbow.localPosition = new Vector3(0.38f, 0.20f, OpponentSeatZ + 0.10f);

            OpponentAvatar driver = model.AddComponent<OpponentAvatar>();
            driver.Bind(
                armRig != null ? armRig.gripAnchor : null,
                leftHand,
                rightElbow,
                leftElbow,
                // 注意：IK 目标必须用世界坐标，这里要把装置坐标系里的点转换成世界点
                parent.TransformPoint(new Vector3(0.17f, feetY, OpponentSeatZ - 0.18f)),
                parent.TransformPoint(new Vector3(-0.17f, feetY, OpponentSeatZ - 0.18f)));

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                SkinnedMeshRenderer skin = renderer as SkinnedMeshRenderer;
                if (skin != null)
                {
                    // 蒙皮包围盒在移动/缩放后经常算错，会被视锥剔除掉——强制每帧重算
                    skin.updateWhenOffscreen = true;
                }
            }
            foreach (Renderer renderer in renderers)
            {
                SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
                Debug.Log("[ArmWrestling] 渲染器: " + renderer.name
                          + " 启用=" + renderer.enabled
                          + " 包围盒中心=" + renderer.bounds.center.ToString("F2")
                          + " 尺寸=" + renderer.bounds.size.ToString("F2")
                          + " 材质=" + (renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "无")
                          + (skinned != null && skinned.sharedMesh != null ? " 顶点=" + skinned.sharedMesh.vertexCount : string.Empty));
            }
            Camera cam = Camera.main;
            if (cam != null && renderers.Length > 0)
            {
                Vector3 sp = cam.WorldToScreenPoint(renderers[0].bounds.center);
                Debug.Log("[ArmWrestling] 相机=" + cam.transform.position.ToString("F2")
                          + " 朝向=" + cam.transform.forward.ToString("F2")
                          + " 模型屏幕坐标=" + sp.ToString("F0")
                          + " 视口=" + cam.pixelWidth + "x" + cam.pixelHeight
                          + " 近裁剪=" + cam.nearClipPlane + " 远裁剪=" + cam.farClipPlane
                          + " 掩码=" + cam.cullingMask);
            }

            Debug.Log("[ArmWrestling] 对手模型: 根位置=" + model.transform.position.ToString("F2")
                      + " 缩放=" + model.transform.lossyScale.x.ToString("F3")
                      + " 控制器=" + (animator.runtimeAnimatorController != null));
            return true;
        }

        /// <summary>没有模型时的兜底：原来的胶囊体对手。</summary>
        private static void BuildCapsuleOpponent(Transform parent)
        {
            GameObject body = new GameObject("Virtual Opponent");
            body.transform.SetParent(parent, false);
            body.transform.localPosition = new Vector3(0f, 0f, 0.22f);

            CreateCapsule(body.transform, "Torso", new Vector3(0f, 0.26f, OpponentSeatZ),
                new Vector3(0.40f, 0.24f, 0.24f), OpponentShirtColor);

            CreateSphere(body.transform, "Head", new Vector3(0f, 0.55f, OpponentSeatZ + 0.03f),
                new Vector3(0.19f, 0.22f, 0.19f), OpponentSkinColor);

            CreateCapsule(body.transform, "Left Upper Arm", new Vector3(0.21f, 0.30f, OpponentSeatZ - 0.04f),
                new Vector3(0.12f, 0.13f, 0.12f), OpponentShirtColor);

            CreateCapsule(body.transform, "Left Forearm", new Vector3(0.23f, 0.15f, OpponentSeatZ - 0.17f),
                new Vector3(0.10f, 0.13f, 0.10f), OpponentSkinColor);

            CreateCapsule(body.transform, "Left Leg", new Vector3(0.17f, -0.16f, OpponentSeatZ - 0.06f),
                new Vector3(0.15f, 0.19f, 0.15f), DarkMetalColor);

            CreateCapsule(body.transform, "Right Leg", new Vector3(-0.17f, -0.16f, OpponentSeatZ - 0.06f),
                new Vector3(0.15f, 0.19f, 0.15f), DarkMetalColor);
        }

        private static void BuildSeats(Transform parent)
        {
            CreateBox(parent, "Player Seat", new Vector3(0f, -0.30f, PlayerSeatZ), new Vector3(0.48f, 0.07f, 0.44f), ChairColor);
            CreateBox(parent, "Player Seat Back", new Vector3(0f, -0.02f, PlayerSeatZ - 0.24f), new Vector3(0.48f, 0.52f, 0.07f), ChairColor);

            // 对手只坐一个圆凳：带靠背的椅子会从对手身体两侧露出来，在两手之间形成一块灰板。
            CreateBox(parent, "Opponent Seat", new Vector3(0f, -0.30f, OpponentSeatZ), new Vector3(0.48f, 0.07f, 0.44f), ChairColor);
        }

        // ------------------------------------------------------------------
        // 基础几何体
        // ------------------------------------------------------------------

        public static GameObject CreateBox(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            return Create(PrimitiveType.Cube, parent, name, localPosition, localScale, color);
        }

        public static GameObject CreateSphere(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            return Create(PrimitiveType.Sphere, parent, name, localPosition, localScale, color);
        }

        public static GameObject CreateCylinder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            return Create(PrimitiveType.Cylinder, parent, name, localPosition, localScale, color);
        }

        public static GameObject CreateCapsule(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            return Create(PrimitiveType.Capsule, parent, name, localPosition, localScale, color);
        }

        private static GameObject Create(PrimitiveType type, Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject instance = GameObject.CreatePrimitive(type);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPosition;
            instance.transform.localScale = localScale;

            Renderer renderer = instance.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetMaterial(color);
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            return instance;
        }

        /// <summary>自发光材质（顶灯面片用）。光照不参与，只是让灯看起来是亮的。</summary>
        public static Material GetEmissiveMaterial(Color color, float intensity)
        {
            Material material;
            if (EmissiveCache.TryGetValue(color, out material) && material != null)
            {
                return material;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                material = CreateOrLoadMaterialAsset(color, intensity, true);
                if (material != null)
                {
                    EmissiveCache[color] = material;
                    return material;
                }
            }
#endif

            Shader shader = Shader.Find("Standard");
            material = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            material.name = "ARW_Emissive_" + ColorUtility.ToHtmlStringRGB(color);
            material.color = color;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * intensity);

            EmissiveCache[color] = material;
            return material;
        }

        /// <summary>按颜色复用材质，避免每个物体一个材质实例。</summary>
        public static Material GetMaterial(Color color)
        {
            Material material;
            if (MaterialCache.TryGetValue(color, out material) && material != null)
            {
                return material;
            }

#if UNITY_EDITOR
            // 编辑器里生成场景时必须使用真正的材质资源。
            // 场景里内嵌（非资源）的材质在打包时无法正确序列化，会导致 level0 损坏、
            // 播放器启动即崩溃，因此这里在编辑期把材质创建为 Assets/ArmWrestling/Materials 下的资源。
            if (!Application.isPlaying)
            {
                material = CreateOrLoadMaterialAsset(color);
                if (material != null)
                {
                    MaterialCache[color] = material;
                    return material;
                }
            }
#endif

            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Diffuse");
            }

            material = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            material.name = "ARW_" + ColorUtility.ToHtmlStringRGB(color);
            ApplyMaterialLook(material, color);

            MaterialCache[color] = material;
            return material;
        }

        private static void ApplyMaterialLook(Material material, Color color)
        {
            material.color = color;

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.22f);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }
        }

#if UNITY_EDITOR
        private const string MaterialFolder = "Assets/ArmWrestling/Materials";

        private static Material CreateOrLoadMaterialAsset(Color color, float intensity = 1f, bool emissive = false)
        {
            string name = "ARW_" + (emissive ? "E_" : string.Empty) + ColorUtility.ToHtmlStringRGB(color);
            string path = MaterialFolder + "/" + name + ".mat";

            Material existing = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            EnsureEditorFolder(MaterialFolder);

            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Diffuse");
            }

            if (shader == null)
            {
                return null;
            }

            Material created = new Material(shader);
            created.name = name;
            ApplyMaterialLook(created, color);

            if (emissive)
            {
                created.EnableKeyword("_EMISSION");
                created.SetColor("_EmissionColor", color * intensity);
            }

            UnityEditor.AssetDatabase.CreateAsset(created, path);
            UnityEditor.AssetDatabase.SaveAssets();
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static void EnsureEditorFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!UnityEditor.AssetDatabase.IsValidFolder(next))
                {
                    UnityEditor.AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
#endif

        public static void ClearMaterialCache()
        {
            MaterialCache.Clear();
            EmissiveCache.Clear();
        }
    }
}






