using UnityEngine;
using UnityEngine.XR;

namespace ArmWrestlingVR
{
    /// <summary>
    /// VR 端主控制器：把“数据源 → 角度处理 → 虚拟手臂 → 状态机 → 界面”串成一条完整流程。
    ///
    /// 职责边界（开题报告 4.1.4 (6)、5.1 (4)）：
    /// VR 端只负责显示、人机交互流程和胜负判定，并把“角色选择 / 开始 / 暂停 / 结束 / 重置”
    /// 等事件发给控制端；磁粉制动器的电流、目标力矩、急停与安全限幅一律不在这里计算。
    /// </summary>
    public class ArmWrestlingApp : MonoBehaviour
    {
        [Header("引用")]
        public ArmWrestlingConfig config;
        public XrPlayerRig rig;
        public DeviceFrameCalibration calibration;
        public HudPresenter hud;
        public VirtualArmRig armRig;
        public Transform deviceRoot;

        [Header("运行状态（只读）")]
        public DataSourceMode dataSource = DataSourceMode.Simulated;
        public OpponentRole role = OpponentRole.Balanced;
        [Range(0.4f, 1.8f)] public float opponentScale = 1f;
        public bool autoDemo;
        public AppState state = AppState.Boot;
        public MatchOutcome outcome = MatchOutcome.None;

        private IDeviceConnection connection;
        private HandSkeletonView handSkeleton;
        private ArmAnglePipeline pipeline;
        private SessionLogger logger;

        private float stateTimer;
        private float countdown;
        private float matchTimer;
        private float playerHoldTimer;
        private float opponentHoldTimer;
        private float lastSampleAge;
        private string transientMessage = string.Empty;
        private float transientTimer;

        private bool pendingAutoAlign;
        private bool lastPrimaryButtonDown;
        private bool lastSecondaryButtonDown;
        private float bootTime;
        private const float AutoAlignTimeout = 3f;

        private const float RoleMin = 0.55f;
        private const float RoleMax = 1.45f;

        // ------------------------------------------------------------------
        // 生命周期
        // ------------------------------------------------------------------

        private void Awake()
        {
            Application.targetFrameRate = 72;
            QualitySettings.vSyncCount = 0;

            // 场景里有暖色射灯 + 两盏色光，默认 Android 只算 1 盏像素光会让画面变平。
            QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, 4);
        }

        private void Start()
        {
            // 注意：Awake 在 AddComponent 时立刻执行，那时外部还没把 Inspector 引用赋进来，
            // 所以所有依赖引用的初始化都放在 Start（此时 ArenaFactory 已经完成赋值）。
            if (config == null)
            {
                config = ArmWrestlingConfig.Load();
            }

            pipeline = new ArmAnglePipeline(config);
            dataSource = config.defaultDataSource;

            // 命令行覆盖，便于自动化测试与现场排查：
            //   -network             强制使用网络数据源
            //   -simulated           强制使用模拟数据源
            //   -hostip 192.168.1.8  指定控制端 IP（关闭自动发现）
            //   -hostport 47010      指定控制端端口
            //   -verbosecomms        打印通信调试日志
            ApplyCommandLineOverrides();

            if (HasCommandLineArgument("-hand"))
            {
                dataSource = DataSourceMode.Hand;
            }
            else if (HasCommandLineArgument("-controller"))
            {
                dataSource = DataSourceMode.Controller;
            }
            else if (HasCommandLineArgument("-network"))
            {
                dataSource = DataSourceMode.Network;
            }
            else if (HasCommandLineArgument("-simulated"))
            {
                dataSource = DataSourceMode.Simulated;
            }

            if (config.enableSessionLog)
            {
                logger = new SessionLogger(config.logIntervalSeconds);
            }

            EnsureHandSkeleton();
            OpenConnection(dataSource);
            UpdateHandSkeleton(dataSource);

            if (calibration != null)
            {
                pendingAutoAlign = !calibration.Load();
            }

            if (hud != null)
            {
                hud.RoleSelected += OnRoleSelected;
                hud.StrengthStepped += OnStrengthStepped;
                hud.NudgeRequested += OnNudgeRequested;
                hud.RecenterRequested += OnRecenterRequested;
                hud.SavePoseRequested += OnSavePoseRequested;
                hud.SetRoleHighlight(role);
                hud.SetStrength(opponentScale);
            }

            EnterState(AppState.DeviceCheck);
        }

        private void ApplyCommandLineOverrides()
        {
            string hostIp = GetCommandLineValue("-hostip");
            string hostPort = GetCommandLineValue("-hostport");
            bool verbose = HasCommandLineArgument("-verbosecomms");

            if (string.IsNullOrEmpty(hostIp) && string.IsNullOrEmpty(hostPort) && !verbose)
            {
                return;
            }

            if (config != null && !config.name.Contains("runtime default"))
            {
                // 避免把命令行参数写回工程里的参数资源。
                config = Instantiate(config);
                config.name = config.name + " (cli override)";
            }

            if (!string.IsNullOrEmpty(hostIp))
            {
                config.manualHostIp = hostIp;
                config.autoDiscoverHost = false;
            }

            int port;
            if (!string.IsNullOrEmpty(hostPort) && int.TryParse(hostPort, out port))
            {
                config.hostPort = Mathf.Clamp(port, 1024, 65535);
            }

            if (verbose)
            {
                config.verboseComms = true;
            }
        }

        private static string GetCommandLineValue(string key)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, System.StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static bool HasCommandLineArgument(string key)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], key, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void OnDestroy()
        {
            if (connection != null)
            {
                connection.Shutdown();
                connection = null;
            }

            if (logger != null)
            {
                logger.Dispose();
                logger = null;
            }
        }

        private void OnApplicationQuit()
        {
            if (logger != null)
            {
                logger.Dispose();
                logger = null;
            }
        }

        // ------------------------------------------------------------------
        // 每帧主循环
        // ------------------------------------------------------------------

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            bool matchActive = state == AppState.Fighting;
            double now = Time.unscaledTimeAsDouble;
            bootTime += deltaTime;

            if (pendingAutoAlign)
            {
                TryAutoAlign();
            }

            HandleGlobalShortcuts();

            if (connection != null)
            {
                UpdateVrSessionState();
            connection.Tick(deltaTime, matchActive);
            }

            PumpSamples();
            float visualAngle = pipeline.Evaluate(now, deltaTime);
            lastSampleAge = pipeline.SecondsSinceSample(now);

            if (armRig != null)
            {
                armRig.ApplyAngle(visualAngle);
            }

            UpdateSimulationTarget(matchActive);
            UpdateStateMachine(deltaTime, visualAngle, now);
            UpdateHud(visualAngle);
            UpdateLogging(visualAngle);
        }

        private void PumpSamples()
        {
            if (connection == null)
            {
                return;
            }

            AngleSample sample;
            int guard = 0;
            while (connection.TryConsumeSample(out sample) && guard++ < 8)
            {
                pipeline.Push(sample);
            }
        }

        private void UpdateSimulationTarget(bool matchActive)
        {
            ISimulationControl simulation = connection as ISimulationControl;
            if (simulation == null)
            {
                return;
            }

            simulation.SetActiveMatch(state == AppState.Fighting, role, opponentScale, autoDemo);
        }

        // ------------------------------------------------------------------
        // 状态机
        // ------------------------------------------------------------------

        private void EnterState(AppState next)
        {
            if (state == next)
            {
                return;
            }

            AppState previous = state;
            state = next;
            stateTimer = 0f;

            switch (next)
            {
                case AppState.Calibration:
                    pipeline.Reset();
                    if (hud != null)
                    {
                        hud.SetBig(string.Empty);
                    }

                    break;

                case AppState.Ready:
                    countdown = config.countdownSeconds;
                    playerHoldTimer = 0f;
                    opponentHoldTimer = 0f;
                    SendEvent(SessionEventCode.Ready, 0, 0f);
                    break;

                case AppState.Countdown:
                    countdown = config.countdownSeconds;
                    break;

                case AppState.Fighting:
                    if (previous != AppState.Paused)
                    {
                        matchTimer = 0f;
                        playerHoldTimer = 0f;
                        opponentHoldTimer = 0f;
                        outcome = MatchOutcome.None;
                        SendEvent(SessionEventCode.MatchStart, (byte)role, opponentScale);
                    }
                    else
                    {
                        SendEvent(SessionEventCode.Resume, 0, 0f);
                    }

                    break;

                case AppState.Paused:
                    SendEvent(SessionEventCode.Pause, 0, 0f);
                    break;

                case AppState.Finished:
                    SendEvent(SessionEventCode.MatchEnd, (byte)outcome, 0f);
                    break;
            }

            if (logger != null)
            {
                logger.LogEvent(next.ToString(), "进入状态 " + next);
            }

            if (hud != null)
            {
                hud.SetBig(string.Empty);
            }
        }

        private void UpdateStateMachine(float deltaTime, float visualAngle, double now)
        {
            stateTimer += deltaTime;

            // 标定页显示“位置微调”按钮组，其他页面显示角色选择。
            if (hud != null)
            {
                hud.SetCalibrationToolsVisible(state == AppState.Calibration);
            }

            switch (state)
            {
                case AppState.DeviceCheck:
                    UpdateDeviceCheck();
                    break;

                case AppState.Calibration:
                    UpdateCalibration();
                    break;

                case AppState.Lobby:
                    UpdateLobby();
                    break;

                case AppState.Ready:
                    UpdateReady(deltaTime);
                    break;

                case AppState.Countdown:
                    UpdateCountdown(deltaTime);
                    break;

                case AppState.Fighting:
                    UpdateFighting(deltaTime, visualAngle, now);
                    break;

                case AppState.Paused:
                    UpdatePaused();
                    break;

                case AppState.Finished:
                    UpdateFinished();
                    break;
            }

            if (transientTimer > 0f)
            {
                transientTimer -= deltaTime;
                if (transientTimer <= 0f)
                {
                    transientMessage = string.Empty;
                }
            }
        }

        private void ShowMessage(string message, float seconds = 4f)
        {
            transientMessage = message;
            transientTimer = seconds;
            Debug.Log("[ArmWrestling] " + message);
        }

        private void UpdateDeviceCheck()
        {
            bool network = dataSource == DataSourceMode.Network;
            bool controller = dataSource == DataSourceMode.Controller;
            bool hand = dataSource == DataSourceMode.Hand;
            bool linkUsable = connection != null && (connection.Status == LinkStatus.Connected || connection.Status == LinkStatus.Stale);
            bool xrActive = rig != null && rig.XrActive;

            if (hud == null)
            {
                return;
            }

            if (network)
            {
                hud.SetButtons(
                    linkUsable ? "下一步：空间标定" : null,
                    linkUsable ? (System.Action)(() => EnterState(AppState.Calibration)) : null,
                    "改用模拟数据",
                    () => SwitchSource(DataSourceMode.Simulated),
                    "重新搜索控制端",
                    () => { if (connection != null) connection.Reconnect(); });
            }
            else if (controller)
            {
                hud.SetButtons(
                    "下一步：空间标定",
                    () => EnterState(AppState.Calibration),
                    "改用模拟数据",
                    () => SwitchSource(DataSourceMode.Simulated),
                    "改用网络数据",
                    () => SwitchSource(DataSourceMode.Network));
            }
            else if (hand)
            {
                hud.SetButtons(
                    "下一步：空间标定",
                    () => EnterState(AppState.Calibration),
                    "改用模拟数据",
                    () => SwitchSource(DataSourceMode.Simulated),
                    "改用手柄",
                    () => SwitchSource(DataSourceMode.Controller));
            }
            else
            {
                hud.SetButtons(
                    "下一步：空间标定",
                    () => EnterState(AppState.Calibration),
                    "改用网络数据",
                    () => SwitchSource(DataSourceMode.Network),
                    xrActive ? "用手部追踪" : null,
                    xrActive ? (System.Action)(() => SwitchSource(DataSourceMode.Hand)) : null);
            }

            hud.SetState("设备检查", !linkUsable && network);

            if (hand)
            {
                hud.SetHint("手部追踪：把右手抬到桌前，手掌位置会带动虚拟手臂");
            }
            else if (controller)
            {
                hud.SetHint("手柄模式：把右手柄当握把，左右转动手腕即可带动虚拟手臂");
            }
            else if (network)
            {
                hud.SetHint("等待控制端数据…看不到面板时按右手柄 A 键重新对齐");
            }
            else
            {
                hud.SetHint("看不到面板？按右手柄 A 键重新对齐，B 键保存位置");
            }
        }

        private void UpdateCalibration()
        {
            if (hud == null)
            {
                return;
            }

            bool xrActive = rig != null && rig.XrActive;
            string link = connection != null ? DisplayNames.Of(connection.Status) : "无";

            hud.SetButtons(
                xrActive ? "用右手柄标定" : "标定（需要头显）",
                xrActive ? (System.Action)CaptureCalibrationWithController : null,
                "按视线快速对齐",
                () => { if (calibration != null) { calibration.AlignAheadOfView(); ShowMessage("已按当前视线快速对齐"); } },
                "跳过标定",
                () => EnterState(AppState.Lobby));

            string hint = xrActive
                ? "看不到面板或位置不对？按右手柄 A 键重新对齐；有夹具就放进夹具扣扳机"
                : "当前未检测到头显（桌面预览），可先用“按视线快速对齐”";

            if (transientTimer > 0f)
            {
                hint = transientMessage;
            }

            hud.SetHint(hint);
            hud.SetState("空间标定  数据:" + link, false);

            if (calibration != null && calibration.IsCalibrated)
            {
                hud.SetStatus("已标定  重复定位偏差 " + (calibration.LastRepeatabilityMeters * 1000f).ToString("F1") + " mm");
            }
            else
            {
                hud.SetStatus("尚未标定");
            }
        }

        private void UpdateLobby()
        {
            if (hud == null)
            {
                return;
            }

            hud.SetButtons(
                "准备对战",
                () => EnterState(AppState.Ready),
                "切换数据源",
                () => EnterState(AppState.DeviceCheck),
                "重新标定",
                () => EnterState(AppState.Calibration));

            hud.SetRoleInteractable(true);
            hud.SetRoleHighlight(role);
            hud.SetStrength(opponentScale);
            hud.SetHint("选择角色后点“准备对战”，角色与力量会发给控制端");
            hud.SetState("角色选择", false);
        }

        private void UpdateReady(float deltaTime)
        {
            if (hud == null)
            {
                return;
            }

            hud.SetButtons(
                "立即开始",
                () => EnterState(AppState.Countdown),
                "返回选择",
                () => EnterState(AppState.Lobby),
                null, null);

            hud.SetRoleInteractable(false);

            float remaining = Mathf.Max(0f, config.readyAutoStartSeconds - stateTimer);
            hud.SetHint(config.readyAutoStartSeconds > 0f
                ? "握住手柄，肘部放上肘垫。" + remaining.ToString("F1") + " s 后自动开始"
                : "握住手柄，肘部放上肘垫，然后点“立即开始”");

            hud.SetState("准备", false);

            if (config.readyAutoStartSeconds > 0f && stateTimer >= config.readyAutoStartSeconds)
            {
                EnterState(AppState.Countdown);
            }
        }

        private void UpdateCountdown(float deltaTime)
        {
            countdown -= deltaTime;

            if (hud != null)
            {
                hud.SetButtons(null, null, "取消", () => EnterState(AppState.Ready), null, null);
                hud.SetState("倒计时", false);
                hud.SetHint("准备好，握紧手柄。");
                hud.SetBig(countdown > 0.05f ? Mathf.CeilToInt(countdown).ToString() : "开始");
            }

            if (countdown <= -0.35f)
            {
                EnterState(AppState.Fighting);
            }
        }

        private void UpdateFighting(float deltaTime, float visualAngle, double now)
        {
            matchTimer += deltaTime;

            if (hud != null)
            {
                hud.SetButtons(null, null, "暂停", () => EnterState(AppState.Paused), null, null);
                hud.SetState("对战进行中", false);
                hud.SetBig(string.Empty);
                hud.SetHint("保持对抗，把对手手臂压到判定线。");
            }

            // 数据过期：停止正常对战，避免继续用旧数据做判定。
            if (pipeline.SecondsSinceSample(now) > config.dataTimeoutSeconds)
            {
                ShowMessage("数据超时（" + (pipeline.SecondsSinceSample(now) * 1000f).ToString("F0") + " ms），已暂停本局", 5f);
                EnterState(AppState.Paused);
                return;
            }

            if (connection != null && connection.Status == LinkStatus.Fault)
            {
                ShowMessage("控制端报告通信故障，已暂停本局", 5f);
                EnterState(AppState.Paused);
                return;
            }

            if (connection != null && connection.LastDeviceState == DeviceState.EmergencyStop)
            {
                ShowMessage("设备处于急停状态，已中断对战", 6f);
                FinishMatch(MatchOutcome.Draw);
                return;
            }

            if (visualAngle >= config.playerWinVisualDeg)
            {
                playerHoldTimer += deltaTime;
                opponentHoldTimer = 0f;
            }
            else if (visualAngle <= config.opponentWinVisualDeg)
            {
                opponentHoldTimer += deltaTime;
                playerHoldTimer = 0f;
            }
            else
            {
                playerHoldTimer = 0f;
                opponentHoldTimer = 0f;
            }

            if (playerHoldTimer >= config.holdSeconds)
            {
                FinishMatch(MatchOutcome.PlayerWins);
                return;
            }

            if (opponentHoldTimer >= config.holdSeconds)
            {
                FinishMatch(MatchOutcome.OpponentWins);
                return;
            }

            if (config.matchTimeLimitSeconds > 0f && matchTimer >= config.matchTimeLimitSeconds)
            {
                FinishMatch(MatchOutcome.Draw);
            }
        }

        private void UpdatePaused()
        {
            if (hud == null)
            {
                return;
            }

            string reason = pipeline.IsStale(Time.unscaledTimeAsDouble) ? "数据超时 / 链路异常" : "手动暂停";

            hud.SetButtons(
                "继续",
                () => EnterState(AppState.Fighting),
                "结束本局",
                () => FinishMatch(MatchOutcome.Draw),
                "重新标定",
                () => EnterState(AppState.Calibration));

            hud.SetState("已暂停（" + reason + "）", pipeline.IsStale(Time.unscaledTimeAsDouble));
            hud.SetHint(pipeline.IsStale(Time.unscaledTimeAsDouble)
                ? "等待控制端数据恢复。超过 " + config.linkLostSeconds.ToString("F1") + " s 仍未收到报文将断开连接。"
                : "已暂停。点击“继续”回到对战。");
        }

        private void UpdateFinished()
        {
            if (hud == null)
            {
                return;
            }

            hud.SetBig(DisplayNames.Of(outcome));
            hud.SetButtons(
                "再来一局",
                () =>
                {
                    SendEvent(SessionEventCode.Reset, 0, 0f);
                    EnterState(AppState.Ready);
                },
                "返回选择",
                () => EnterState(AppState.Lobby),
                "重新标定",
                () => EnterState(AppState.Calibration));

            hud.SetState("本局结束", false);
            hud.SetHint("判定依据：机构实测角度到达阈值并保持 " + config.holdSeconds.ToString("F2") + " s。");
        }

        private void FinishMatch(MatchOutcome result)
        {
            outcome = result;
            if (logger != null)
            {
                logger.LogEvent(state.ToString(), "本局结果:" + result);
            }

            EnterState(AppState.Finished);
        }

        // ------------------------------------------------------------------
        // 界面刷新
        // ------------------------------------------------------------------

        private void UpdateHud(float visualAngle)
        {
            if (hud == null)
            {
                return;
            }

            string sourceName = connection != null ? connection.Name : "无";
            LinkStatus linkStatus = connection != null ? connection.Status : LinkStatus.Disconnected;
            string linkDetail = connection != null ? connection.Detail : string.Empty;
            DeviceState deviceState = connection != null ? connection.LastDeviceState : DeviceState.Init;
            float torque = connection != null ? connection.LastTorqueNm : 0f;

            hud.SetMetrics(hud.FormatMetrics(
                pipeline.MechanicalAngleDeg,
                visualAngle,
                pipeline.AngularVelocityDegPerSecond,
                torque,
                lastSampleAge,
                sourceName,
                linkStatus,
                linkDetail,
                deviceState));

            if (dataSource == DataSourceMode.Simulated)
            {
                ISimulationControl simulation = connection as ISimulationControl;
                hud.SetStatus(simulation != null ? "模拟模式 · " + simulation.SimulationHint : "模拟模式");
            }
            else if (dataSource == DataSourceMode.Controller)
            {
                hud.SetStatus("手柄模式 · 把右手柄当握把，左右转动带动手臂");
            }
            else
            {
                hud.SetStatus("控制端：" + linkDetail);
            }

            if (state != AppState.Finished && state != AppState.Countdown)
            {
                hud.SetRoleHighlight(role);
                hud.SetStrength(opponentScale);
            }
        }

        private void UpdateLogging(float visualAngle)
        {
            if (logger == null || !logger.IsOpen)
            {
                return;
            }

            logger.Log(
                state.ToString(),
                pipeline.MechanicalAngleDeg,
                visualAngle,
                pipeline.AngularVelocityDegPerSecond,
                connection != null ? connection.LastTorqueNm : 0f,
                connection != null ? connection.LastDeviceState.ToString() : string.Empty,
                role.ToString(),
                transientMessage,
                false);
        }

        // ------------------------------------------------------------------
        // 交互
        // ------------------------------------------------------------------

        private void HandleGlobalShortcuts()
        {
            // XR 上的"救命键"：看不到面板时，按右手柄 A 键把虚拟装置重新摆到眼前。
            // 场景刚启动时头显可能还放在桌上，自动对齐会算错，这时靠这个键恢复。
            if (rig != null && rig.XrActive)
            {
                bool primary = rig.TryGetPrimaryButton(XRNode.RightHand);
                if (primary && !lastPrimaryButtonDown)
                {
                    OnRecenterRequested();
                    if (state != AppState.Calibration && state != AppState.Fighting)
                    {
                        EnterState(AppState.Calibration);
                    }
                }

                lastPrimaryButtonDown = primary;

                bool secondary = rig.TryGetSecondaryButton(XRNode.RightHand);
                if (secondary && !lastSecondaryButtonDown)
                {
                    OnSavePoseRequested();
                }

                lastSecondaryButtonDown = secondary;
            }

            // 桌面调试快捷键：不戴头显也能跑完整流程。
            if (Input.GetKeyDown(KeyCode.Space) && dataSource == DataSourceMode.Simulated)
            {
                autoDemo = !autoDemo;
                ShowMessage(autoDemo ? "自动演示已开启" : "自动演示已关闭", 2f);
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (state == AppState.Fighting)
                {
                    EnterState(AppState.Paused);
                }
                else if (state == AppState.Paused)
                {
                    EnterState(AppState.Fighting);
                }
            }

            if (Input.GetKeyDown(KeyCode.C) && (state == AppState.Lobby || state == AppState.DeviceCheck))
            {
                EnterState(AppState.Calibration);
            }

            if (Input.GetKeyDown(KeyCode.R) && state == AppState.Finished)
            {
                SendEvent(SessionEventCode.Reset, 0, 0f);
                EnterState(AppState.Ready);
            }
        }

        private void CaptureCalibrationWithController()
        {
            if (calibration == null)
            {
                return;
            }

            string message;
            bool ok = calibration.TryCaptureFromController(XRNode.RightHand, out message);
            ShowMessage(message, 5f);

            if (ok && hud != null)
            {
                int residualMillimeters = Mathf.RoundToInt(calibration.LastRepeatabilityMeters * 1000f);
                SendEvent(SessionEventCode.CalibrationDone, 0, calibration.LastRepeatabilityMeters);
                ShowMessage("标定完成，重复定位偏差 " + residualMillimeters + " mm", 5f);
            }
        }

        private void OnRoleSelected(OpponentRole selected)
        {
            role = selected;

            if (hud != null)
            {
                hud.SetRoleHighlight(role);
            }

            SendEvent(SessionEventCode.RoleSelect, (byte)role, opponentScale);
            ShowMessage("已选择 " + DisplayNames.Of(role), 2f);
        }

        private void OnStrengthStepped(int direction)
        {
            opponentScale = Mathf.Clamp(opponentScale + direction * 0.1f, RoleMin, RoleMax);

            if (hud != null)
            {
                hud.SetStrength(opponentScale);
            }

            SendEvent(SessionEventCode.RoleSelect, (byte)role, opponentScale);
        }

        /// <summary>标定页的手动微调：axis 0=X 1=Z 2=Y 3=绕竖轴旋转。</summary>
        private void OnNudgeRequested(int axis, int direction)
        {
            if (calibration == null)
            {
                return;
            }

            calibration.Nudge(axis, direction);
            ShowMessage("已微调，调好后点“保存位置”", 2.5f);
        }

        private void OnRecenterRequested()
        {
            if (calibration == null)
            {
                return;
            }

            calibration.AlignAheadOfView(0.62f, 0.42f, false);
            ShowMessage("已按当前视线重新对齐", 2.5f);
        }

        private void OnSavePoseRequested()
        {
            if (calibration == null)
            {
                return;
            }

            calibration.Save();
            ShowMessage("装置位置已保存，下次启动自动加载", 3f);
        }

        private void SendEvent(SessionEventCode code, byte parameter, float value)
        {
            if (connection == null)
            {
                return;
            }

            connection.SendEvent(code, parameter, value);

            if (logger != null)
            {
                logger.LogEvent(state.ToString(), "事件 " + code + " param=" + parameter + " value=" + value.ToString("F2"));
            }
        }

        // ------------------------------------------------------------------
        // 数据源
        // ------------------------------------------------------------------

        private void SwitchSource(DataSourceMode mode)
        {
            OpenConnection(mode);
            pipeline.Reset();
            EnterState(AppState.DeviceCheck);
            ShowMessage("已切换到" + SourceDisplayName(mode), 2f);
            UpdateHandSkeleton(mode);
        }

        private void EnsureHandSkeleton()
        {
            if (handSkeleton != null || deviceRoot == null)
            {
                return;
            }

            GameObject root = new GameObject("Hand Skeleton");
            root.transform.SetParent(deviceRoot, false);
            handSkeleton = root.AddComponent<HandSkeletonView>();
            handSkeleton.SetVisible(false);
        }

        /// <summary>把 VR 自己的流程状态翻译成上位机约定的四种比赛状态。</summary>
        private static string ToVrMatchState(AppState value)
        {
            switch (value)
            {
                case AppState.Paused:
                    return "paused";
                case AppState.Finished:
                    return "finished";
                case AppState.Ready:
                case AppState.Countdown:
                case AppState.Fighting:
                    return "running";
                default:
                    return "waiting";
            }
        }

        private void UpdateVrSessionState()
        {
            VrUdpClient client = connection as VrUdpClient;
            if (client == null)
            {
                return;
            }

            client.MatchState = ToVrMatchState(state);
            client.CharacterId = "opponent_" + ((int)role).ToString("00");
        }

        private static string SourceDisplayName(DataSourceMode mode)
        {
            switch (mode)
            {
                case DataSourceMode.Network: return "网络数据";
                case DataSourceMode.Controller: return "手柄";
                case DataSourceMode.Hand: return "手部追踪";
                default: return "模拟数据";
            }
        }

        /// <summary>只有手部模式才显示手骨，其它模式把它藏起来免得挡视线。</summary>
        private void UpdateHandSkeleton(DataSourceMode mode)
        {
            if (handSkeleton == null)
            {
                return;
            }

            handSkeleton.SetVisible(mode == DataSourceMode.Hand);
        }

        private void OpenConnection(DataSourceMode mode)
        {
            if (connection != null)
            {
                connection.Shutdown();
                connection = null;
            }

            dataSource = mode;

            switch (mode)
            {
                case DataSourceMode.Network:
                {
                    // 上位机仓库 docs/vr-udp-v01.md：PC 监听 UDP 9102，收发同一个 socket。
                    string host = string.IsNullOrEmpty(config.manualHostIp) ? "127.0.0.1" : config.manualHostIp;
                    VrUdpClient vrClient = new VrUdpClient(config, host, config.hostPort);
                    vrClient.CharacterId = "opponent_" + ((int)role).ToString("00");
                    connection = vrClient;
                    break;
                }

                case DataSourceMode.Controller:
                    connection = new ControllerAngleSource(config, rig, deviceRoot);
                    break;

                case DataSourceMode.Hand:
                    connection = new HandAngleSource(config, deviceRoot);
                    break;

                default:
                    connection = new SimulatedDeviceConnection(config);
                    break;
            }

            pipeline.Reset();
        }

        private void TryAutoAlign()
        {
            if (rig == null || calibration == null)
            {
                pendingAutoAlign = false;
                return;
            }

            bool ready = rig.XrActive ? rig.HeadTracked : true;

            // 头显平放在桌上或朝下时（俯仰角很大），此刻的朝向不能代表使用者视线，
            // 自动对齐会算歪。等到头显被戴上、视线大致水平后再对齐。
            if (ready && rig.XrActive && rig.head != null)
            {
                float pitch = Mathf.Abs(Mathf.Asin(Mathf.Clamp(rig.head.forward.y, -1f, 1f)) * Mathf.Rad2Deg);
                if (pitch > 45f && bootTime < 40f)
                {
                    return;
                }
            }

            if (!ready && bootTime < AutoAlignTimeout)
            {
                return;
            }

            if (!ready)
            {
                ShowMessage("未检测到头显追踪，暂时按默认位置摆放虚拟桌面");
            }

            calibration.AlignAheadOfView(0.62f, 0.42f, false);
            pendingAutoAlign = false;
        }

        // ------------------------------------------------------------------
        // 对外接口（供联调脚本或外部程序调用）
        // ------------------------------------------------------------------

        public void ForceState(AppState next)
        {
            EnterState(next);
        }

        public void SetDataSource(DataSourceMode mode)
        {
            SwitchSource(mode);
        }

        public IDeviceConnection Connection
        {
            get { return connection; }
        }

        public ArmAnglePipeline Pipeline
        {
            get { return pipeline; }
        }
    }
}






