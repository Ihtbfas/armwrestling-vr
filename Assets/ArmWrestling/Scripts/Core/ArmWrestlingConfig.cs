using System;
using UnityEngine;

namespace ArmWrestlingVR
{
    /// <summary>
    /// VR 端全部可调参数的唯一来源。
    ///
    /// 设计原则：凡是需要和机械组、控制组协商的量（零位、方向、判定阈值、超时时间、
    /// 通信地址）都放在这里，改参数不需要改代码。资源位于
    /// <c>Assets/ArmWrestling/Resources/ArmWrestlingConfig.asset</c>，运行时会自动加载；
    /// 若资源缺失则使用代码中的默认值，保证空工程也能直接运行。
    /// </summary>
    [CreateAssetMenu(fileName = "ArmWrestlingConfig", menuName = "Arm Wrestling/Config", order = 0)]
    public class ArmWrestlingConfig : ScriptableObject
    {
        public const string ResourcesPath = "ArmWrestlingConfig";

        // ------------------------------------------------------------------
        // 1. 机械角度 -> 虚拟手臂角度映射
        // ------------------------------------------------------------------

        [Header("机械角度映射")]
        [Tooltip("机械角度零位（度）。摆臂竖直、双方手部处于中央时下位机上报的角度值。")]
        public float mechanicalZeroDeg = 0f;

        [Tooltip("机械量程（度）。相对零位的正向行程，用于把机械角度归一化到 1 个单位。")]
        public float mechanicalFullScaleDeg = 40f;

        [Tooltip("方向。+1 表示机械角度增大 = 我方推进；-1 表示相反。与机械组确认后设置。")]
        public int mechanicalDirection = 1;

        [Tooltip("视觉旋转换算系数：1 度机械角度对应多少度模型旋转。默认 1:1。")]
        public float visualDegPerMechanicalDeg = 1f;

        [Tooltip("视觉角度偏置（度）。用于让起始姿态看起来自然。")]
        public float visualOffsetDeg = 0f;

        [Tooltip("视觉旋转限幅（度）。超过该角度后模型不再继续转动。")]
        public float visualLimitDeg = 42f;

        [Tooltip("机械角度有效范围（度）。超出该范围的数据判为无效，避免异常值把画面拉飞。")]
        public float mechanicalValidMinDeg = -60f;
        public float mechanicalValidMaxDeg = 60f;

        // ------------------------------------------------------------------
        // 2. 对战规则
        // ------------------------------------------------------------------

        [Header("对战规则")]
        [Tooltip("判定阈值（视觉角度，度）。正向超过该角度并保持 holdSeconds 秒，我方获胜。")]
        public float playerWinVisualDeg = 32f;

        [Tooltip("判定阈值（视觉角度，度）。负向超过该角度并保持 holdSeconds 秒，对手获胜。")]
        public float opponentWinVisualDeg = -32f;

        [Tooltip("判定保持时间（秒）。减少角度在阈值附近抖动造成的误判。")]
        public float holdSeconds = 0.35f;

        [Tooltip("倒计时时长（秒）。")]
        public float countdownSeconds = 3f;

        [Tooltip("准备阶段自动进入倒计时的等待时间（秒）。≤0 表示需要手动确认。")]
        public float readyAutoStartSeconds = 2.5f;

        [Tooltip("单局最长时长（秒）。≤0 表示不限制。")]
        public float matchTimeLimitSeconds = 60f;

        // ------------------------------------------------------------------
        // 3. 数据质量与延迟
        // ------------------------------------------------------------------

        [Header("数据质量")]
        [Tooltip("角度低通滤波时间常数（秒）。越大画面越稳、响应越慢；0 表示不滤波。")]
        public float filterTimeConstant = 0.035f;

        [Tooltip("数据超时时间（秒）。超过该时间没有新数据则锁定最后一次姿态并提示。")]
        public float dataTimeoutSeconds = 0.5f;

        [Tooltip("链路丢失时间（秒）。超过该时间没有任何报文则断开连接。")]
        public float linkLostSeconds = 1.5f;

        [Tooltip("最大外推时间（秒）。两个数据帧之间用角速度外推的最大时长。")]
        public float maxExtrapolationSeconds = 0.08f;

        [Tooltip("是否启用角速度外推，使画面在低刷新率下更连续。")]
        public bool enableExtrapolation = true;

        // ------------------------------------------------------------------
        // 4. 通信
        // ------------------------------------------------------------------

        [Header("通信")]
        [Tooltip("上位机（控制端）UDP 监听端口。VR 端向该端口发送心跳与事件。")]
        public int hostPort = 47010;

        [Tooltip("VR 端本地接收端口。0 表示由系统分配。")]
        public int localPort = 47011;

        [Tooltip("是否自动发现上位机（向广播地址发送 HELLO，收到 HELLO_ACK 后自动连接）。")]
        public bool autoDiscoverHost = true;

        [Tooltip("手动指定的上位机 IP。留空则使用自动发现的结果。")]
        public string manualHostIp = "";

        [Tooltip("心跳发送间隔（秒）。")]
        public float heartbeatIntervalSeconds = 1f;

        [Tooltip("事件重发间隔（秒）。上位机未确认时按该间隔重发。")]
        public float eventRetrySeconds = 0.2f;

        [Tooltip("事件最多发送次数，超过后判定失败并提示。")]
        public int eventMaxAttempts = 5;

        // ------------------------------------------------------------------
        // 5. 启动与调试
        // ------------------------------------------------------------------

        [Header("启动与调试")]
        [Tooltip("启动时默认使用的数据源。开发阶段选 Simulated，联调时选 Network。")]
        public DataSourceMode defaultDataSource = DataSourceMode.Simulated;

        [Tooltip("模拟模式下是否允许键盘操作（A/← 我方发力，D/→ 对手发力）。")]
        public bool allowKeyboardInSimulation = true;

        [Tooltip("是否把运行数据写入 CSV 日志（Application.persistentDataPath/sessions）。")]
        public bool enableSessionLog = true;

        [Tooltip("日志采样周期（秒）。")]
        public float logIntervalSeconds = 0.05f;

        [Tooltip("在控制台输出通信调试信息。")]
        public bool verboseComms = false;

        [Tooltip("桌面调试时用鼠标控制视角。")]
        public bool desktopMouseLook = true;

        /// <summary>角度归一化：把机械角度换算成 [-1,1] 的推进量，超过量程时截断。</summary>
        public float NormalizeMechanical(float mechanicalDeg)
        {
            if (Mathf.Abs(mechanicalFullScaleDeg) < 1e-4f)
            {
                return 0f;
            }

            float normalized = (mechanicalDeg - mechanicalZeroDeg) / mechanicalFullScaleDeg;
            return Mathf.Clamp(normalized, -1.5f, 1.5f);
        }

        public bool IsMechanicalAngleValid(float mechanicalDeg)
        {
            return !float.IsNaN(mechanicalDeg)
                   && !float.IsInfinity(mechanicalDeg)
                   && mechanicalDeg >= mechanicalValidMinDeg
                   && mechanicalDeg <= mechanicalValidMaxDeg;
        }

        /// <summary>把机械角度换算为模型旋转角度（度）。</summary>
        public float ToVisualDegrees(float mechanicalDeg)
        {
            float normalized = NormalizeMechanical(mechanicalDeg);
            float visual = normalized * Mathf.Abs(mechanicalFullScaleDeg) * visualDegPerMechanicalDeg * Mathf.Sign(mechanicalDirection);
            visual += visualOffsetDeg;
            return Mathf.Clamp(visual, -visualLimitDeg, visualLimitDeg);
        }

        /// <summary>上面那个映射的逆运算：把模型旋转角度换回机械角度（手柄模拟时用）。</summary>
        public float ToMechanicalDegrees(float visualDeg)
        {
            float scale = Mathf.Abs(mechanicalFullScaleDeg) * Mathf.Max(0.0001f, visualDegPerMechanicalDeg) * Mathf.Sign(mechanicalDirection);
            float normalized = (visualDeg - visualOffsetDeg) / scale;
            return mechanicalZeroDeg + normalized * mechanicalFullScaleDeg;
        }

        /// <summary>代码默认值。资源缺失时由 <see cref="Load"/> 使用。</summary>
        public static ArmWrestlingConfig CreateDefault()
        {
            ArmWrestlingConfig config = CreateInstance<ArmWrestlingConfig>();
            config.name = "ArmWrestlingConfig (runtime default)";
            return config;
        }

        public static ArmWrestlingConfig Load()
        {
            ArmWrestlingConfig asset = Resources.Load<ArmWrestlingConfig>(ResourcesPath);
            if (asset != null)
            {
                return asset;
            }

            Debug.LogWarning("[ArmWrestling] 未找到 Resources/" + ResourcesPath + "，使用代码默认参数。");
            return CreateDefault();
        }

        private void OnValidate()
        {
            if (mechanicalFullScaleDeg <= 1f)
            {
                mechanicalFullScaleDeg = 1f;
            }

            if (mechanicalDirection == 0)
            {
                mechanicalDirection = 1;
            }

            visualLimitDeg = Mathf.Max(1f, visualLimitDeg);
            playerWinVisualDeg = Mathf.Clamp(playerWinVisualDeg, 1f, visualLimitDeg);
            opponentWinVisualDeg = Mathf.Clamp(opponentWinVisualDeg, -visualLimitDeg, -1f);
            holdSeconds = Mathf.Max(0.05f, holdSeconds);
            dataTimeoutSeconds = Mathf.Max(0.05f, dataTimeoutSeconds);
            linkLostSeconds = Mathf.Max(dataTimeoutSeconds, linkLostSeconds);
            hostPort = Mathf.Clamp(hostPort, 1024, 65535);
            localPort = Mathf.Clamp(localPort, 0, 65535);
        }
    }

    [Serializable]
    public struct CalibrationPose
    {
        public Vector3 position;
        public Quaternion rotation;

        public CalibrationPose(Vector3 position, Quaternion rotation)
        {
            this.position = position;
            this.rotation = rotation;
        }

        public bool IsValid
        {
            get { return !float.IsNaN(position.x) && !float.IsNaN(rotation.w); }
        }

        public static CalibrationPose Identity
        {
            get { return new CalibrationPose(Vector3.zero, Quaternion.identity); }
        }
    }
}
