using UnityEngine;

namespace ArmWrestlingVR
{
    /// <summary>
    /// 没有机械装置时使用的模拟数据源。
    ///
    /// 它并不只是“让手臂来回摆”，而是按单自由度摆臂的简化动力学积分：
    /// 我方施加的力矩与对手角色的阻力矩之差驱动角加速度，再由阻尼和限幅收敛。
    /// 这样在无硬件阶段就能验证角度映射、滤波、胜负判定、状态机与界面联动，
    /// 并且模拟出的数据在量纲（度、度/秒、N·m）上与真实上位机一致。
    ///
    /// 按键：A 或 ← 我方发力；D 或 → 放任对手发力；空格切换自动演示。
    /// </summary>
    public class SimulatedDeviceConnection : IDeviceConnection, ISimulationControl
    {
        private const float Inertia = 0.28f;      // 摆臂 + 手部等效转动惯量 kg·m²
        private const float Damping = 0.55f;      // 角速度阻尼
        private const float PlayerTorqueMax = 26f; // 我方最大力矩 N·m
        private const float OpponentTorqueBase = 15f;

        private readonly ArmWrestlingConfig config;

        private float angleDeg;
        private float omegaDegPerSecond;
        private float opponentTorque;
        private float playerTorque;

        private bool activeMatch;
        private bool autoPlayer;
        private OpponentRole role = OpponentRole.Balanced;
        private float opponentScale = 1f;
        private float elapsed;
        private float noisePhase;

        private bool hasNewSample;
        private AngleSample latestSample;
        private ulong frameCounter;
        private float sampleTimer;
        private const float SampleInterval = 1f / 100f; // 模拟 100 Hz 上报

        public SimulatedDeviceConnection(ArmWrestlingConfig config)
        {
            this.config = config;
            angleDeg = 0f;
            omegaDegPerSecond = 0f;
            noisePhase = Random.value * 10f;
        }

        public string Name
        {
            get { return "模拟数据"; }
        }

        public LinkStatus Status
        {
            get { return LinkStatus.Connected; }
        }

        public string Detail
        {
            get { return autoPlayer ? "自动演示中" : "键盘手动对抗"; }
        }

        public bool IsDataAvailable
        {
            get { return true; }
        }

        public float SecondsSinceLastData
        {
            get { return sampleTimer; }
        }

        public float LastTorqueNm
        {
            get { return opponentTorque; }
        }

        public DeviceState LastDeviceState
        {
            get { return activeMatch ? DeviceState.Running : DeviceState.Ready; }
        }

        public MatchPhase HostMatchPhase
        {
            get { return activeMatch ? MatchPhase.Fighting : MatchPhase.None; }
        }

        public string SimulationHint
        {
            get
            {
                if (autoPlayer)
                {
                    return "自动演示：空格切回手动";
                }

                return config.allowKeyboardInSimulation
                    ? "A/← 我方发力  D/→ 对手发力  空格 自动演示"
                    : "模拟模式（键盘已禁用）";
            }
        }

        public void SetActiveMatch(bool active, OpponentRole opponentRole, float scale, bool useAutoPlayer)
        {
            if (active && !activeMatch)
            {
                elapsed = 0f;
            }

            activeMatch = active;
            role = opponentRole;
            opponentScale = Mathf.Clamp(scale, 0.4f, 1.8f);
            autoPlayer = useAutoPlayer;
        }

        public void Tick(float deltaTime, bool matchActive)
        {
            deltaTime = Mathf.Min(deltaTime, 0.05f);
            elapsed += deltaTime;

            UpdatePlayerTorque(deltaTime);
            UpdateOpponentTorque(deltaTime);

            float netTorque = playerTorque - opponentTorque - Damping * Mathf.Deg2Rad * omegaDegPerSecond;
            float angularAcceleration = netTorque / Inertia * Mathf.Rad2Deg;
            omegaDegPerSecond += angularAcceleration * deltaTime;

            // 机械限位：到达行程末端后速度衰减，模拟缓冲块。
            float limit = Mathf.Abs(config.mechanicalFullScaleDeg) + 4f;
            if (angleDeg > limit)
            {
                angleDeg = limit;
                omegaDegPerSecond = Mathf.Min(omegaDegPerSecond, 0f) * 0.4f;
            }
            else if (angleDeg < -limit)
            {
                angleDeg = -limit;
                omegaDegPerSecond = Mathf.Max(omegaDegPerSecond, 0f) * 0.4f;
            }

            angleDeg += omegaDegPerSecond * deltaTime;

            sampleTimer += deltaTime;
            if (sampleTimer < SampleInterval)
            {
                return;
            }

            sampleTimer = 0f;
            hasNewSample = true;

            float noise = Mathf.Sin(Time.unscaledTime * 37.3f + noisePhase) * 0.08f;
            latestSample = new AngleSample
            {
                localTime = Time.unscaledTimeAsDouble,
                angleDeg = angleDeg + noise,
                angularVelocityDegPerSecond = omegaDegPerSecond,
                torqueNm = opponentTorque,
                deviceState = matchActive ? DeviceState.Running : DeviceState.Ready,
                valid = true,
                faultCode = 0,
                frameCounter = ++frameCounter
            };
        }

        private void UpdatePlayerTorque(float deltaTime)
        {
            if (autoPlayer)
            {
                // 自动演示：用一个缓慢变化的力矩驱动，方便观察画面与判定。
                playerTorque = OpponentTorqueBase * opponentScale * (1.35f + 0.55f * Mathf.Sin(elapsed * 0.9f));
                return;
            }

            if (!activeMatch || !config.allowKeyboardInSimulation)
            {
                playerTorque = activeMatch ? 0f : 0f;
                return;
            }

            bool attack = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
            bool yield = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);

            if (attack)
            {
                playerTorque = Mathf.MoveTowards(playerTorque, PlayerTorqueMax, 90f * deltaTime);
            }
            else if (yield)
            {
                playerTorque = Mathf.MoveTowards(playerTorque, -PlayerTorqueMax * 0.25f, 90f * deltaTime);
            }
            else
            {
                playerTorque = Mathf.MoveTowards(playerTorque, PlayerTorqueMax * 0.55f, 45f * deltaTime);
            }
        }

        private void UpdateOpponentTorque(float deltaTime)
        {
            if (!activeMatch)
            {
                opponentTorque = Mathf.MoveTowards(opponentTorque, 0f, 60f * deltaTime);
                return;
            }

            float target = OpponentTorqueBase * opponentScale;
            float progress = Mathf.InverseLerp(-config.playerWinVisualDeg, config.playerWinVisualDeg, angleDeg);

            switch (role)
            {
                case OpponentRole.Defensive:
                    // 落后时抵抗更强：越接近被压制的阈值，阻力越大。
                    target *= Mathf.Lerp(1.55f, 0.75f, progress);
                    break;

                case OpponentRole.Rhythmic:
                    // 节奏变化：增强—僵持—减弱循环。
                    float cycle = Mathf.Repeat(elapsed * 0.45f, 1f);
                    target *= cycle < 0.45f ? 1.35f : (cycle < 0.75f ? 1.0f : 0.72f);
                    break;

                default:
                    target *= Mathf.Lerp(1.12f, 0.92f, progress);
                    break;
            }

            opponentTorque = Mathf.MoveTowards(opponentTorque, target, 40f * deltaTime);
        }

        public bool TryConsumeSample(out AngleSample sample)
        {
            sample = latestSample;
            if (!hasNewSample)
            {
                return false;
            }

            hasNewSample = false;
            return true;
        }

        public void SendEvent(SessionEventCode code, byte parameter, float value)
        {
            if (config.verboseComms)
            {
                Debug.Log("[ArmWrestling][模拟] 事件 " + code + " param=" + parameter + " value=" + value.ToString("F2"));
            }

            latestSample.torqueNm = opponentTorque;
        }

        public void Reconnect()
        {
            angleDeg = 0f;
            omegaDegPerSecond = 0f;
            opponentTorque = 0f;
            playerTorque = 0f;
        }

        public void Shutdown()
        {
        }
    }
}
