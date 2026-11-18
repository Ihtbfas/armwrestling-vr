using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace ArmWrestlingVR
{
    /// <summary>
    /// 用 PICO 4 的手部追踪驱动虚拟手臂 —— 不用手柄、不用机械装置，直接用你自己的手。
    ///
    /// 取右手掌根（Palm）的位置换算到装置坐标系，再取它在摆臂旋转平面（XY 平面）内
    /// 相对转轴的极角：
    /// <code>
    ///   θ = atan2(手掌.x, 手掌.y − 转轴高度)
    /// </code>
    /// 与手柄模式完全相同的几何模型，只是输入从手柄换成了手掌关节。
    ///
    /// 它和 UDP / 手柄数据源实现同一个 <see cref="IDeviceConnection"/> 接口，
    /// 因此上层角度管线、动作映射、胜负判定、界面一行都不用改。
    /// </summary>
    public class HandAngleSource : IDeviceConnection
    {
        private const float SmoothingSeconds = 0.02f;

        private readonly ArmWrestlingConfig config;
        private readonly Transform deviceRoot;

        private XRHandSubsystem subsystem;
        private bool tracked;
        private float visualAngleDeg;
        private float angularVelocity;
        private bool hasNewSample;
        private ulong frameCounter;
        private float lastSampleTime;
        private AngleSample latestSample;

        public HandAngleSource(ArmWrestlingConfig config, Transform deviceRoot)
        {
            this.config = config;
            this.deviceRoot = deviceRoot;
        }

        /// <summary>供骨骼可视化读取，未初始化时返回 null。</summary>
        public XRHandSubsystem Subsystem
        {
            get { return subsystem; }
        }

        public string Name
        {
            get { return "手部追踪"; }
        }

        public LinkStatus Status
        {
            get { return tracked ? LinkStatus.Connected : LinkStatus.Stale; }
        }

        public string Detail
        {
            get
            {
                if (subsystem == null)
                {
                    return "没有找到手部追踪子系统";
                }

                return tracked ? "右手已追踪" : "把手放进视野里";
            }
        }

        public bool IsDataAvailable
        {
            get { return tracked; }
        }

        public float SecondsSinceLastData
        {
            get { return tracked ? 0f : 999f; }
        }

        public float LastTorqueNm
        {
            get { return 0f; }
        }

        public DeviceState LastDeviceState
        {
            get { return DeviceState.Running; }
        }

        public MatchPhase HostMatchPhase
        {
            get { return MatchPhase.Fighting; }
        }

        public void Tick(float deltaTime, bool matchActive)
        {
            EnsureSubsystem();

            if (subsystem == null || deviceRoot == null)
            {
                tracked = false;
                return;
            }

            XRHand hand = subsystem.rightHand;
            tracked = hand.isTracked;

            if (!tracked)
            {
                return;
            }

            XRHandJoint palm = hand.GetJoint(XRHandJointID.Palm);
            Pose pose;
            if (!palm.TryGetPose(out pose))
            {
                tracked = false;
                return;
            }

            Vector3 local = deviceRoot.InverseTransformPoint(pose.position);
            float theta = Mathf.Atan2(local.x, local.y - ArenaFactory.SwingPivotHeight) * Mathf.Rad2Deg;
            float targetVisual = Mathf.Clamp(theta, -config.visualLimitDeg, config.visualLimitDeg);

            float smoothing = 1f - Mathf.Exp(-Mathf.Max(0.001f, deltaTime) / SmoothingSeconds);
            float previous = visualAngleDeg;
            visualAngleDeg = Mathf.Lerp(visualAngleDeg, targetVisual, Mathf.Clamp01(smoothing));

            if (deltaTime > 0.0001f)
            {
                angularVelocity = (visualAngleDeg - previous) / deltaTime;
            }

            double now = Time.unscaledTimeAsDouble;
            if (now - lastSampleTime >= 1.0 / 90.0)
            {
                lastSampleTime = (float)now;
                hasNewSample = true;
                latestSample = new AngleSample
                {
                    localTime = now,
                    angleDeg = config.ToMechanicalDegrees(visualAngleDeg),
                    angularVelocityDegPerSecond = angularVelocity,
                    torqueNm = 0f,
                    deviceState = DeviceState.Running,
                    valid = true,
                    faultCode = 0,
                    frameCounter = ++frameCounter
                };
            }
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
                Debug.Log("[ArmWrestling][手部] 事件 " + code);
            }
        }

        public void Reconnect()
        {
            tracked = false;
            visualAngleDeg = 0f;
            subsystem = null;
            EnsureSubsystem();
        }

        public void Shutdown()
        {
        }

        private void EnsureSubsystem()
        {
            if (subsystem != null && subsystem.running)
            {
                return;
            }

            var found = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(found);
            if (found.Count == 0)
            {
                return;
            }

            subsystem = found[0];
            if (!subsystem.running)
            {
                try
                {
                    subsystem.Start();
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning("[ArmWrestling][手部] 启动手部子系统失败: " + exception.Message);
                }
            }
        }
    }
}
