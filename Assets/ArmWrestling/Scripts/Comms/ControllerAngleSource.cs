using UnityEngine;
using UnityEngine.XR;

namespace ArmWrestlingVR
{
    /// <summary>
    /// 用 VR 手柄驱动虚拟手臂的数据源——机械装置还没装好时的"临时输入设备"。
    ///
    /// 原理：把右手柄在追踪空间中的位置换算到装置坐标系，再取它在摆臂旋转平面（XY 平面）
    /// 内相对转轴的极角，作为摆臂的当前角度：
    /// <code>
    ///   θ = atan2(手柄.x, 手柄.y − 转轴高度)
    /// </code>
    /// 也就是说"你把手往哪放，虚拟手臂就转到哪"，和真实机构里"手带着摆臂转"是同一个物理模型。
    ///
    /// 它与 UDP 数据源实现同一个 <see cref="IDeviceConnection"/> 接口，因此后续接上真实机械装置时，
    /// 只要在界面上切换到"网络数据"，角度管线、动作映射、胜负判定、界面完全不用改一行代码。
    /// </summary>
    public class ControllerAngleSource : IDeviceConnection
    {
        private readonly ArmWrestlingConfig config;
        private readonly XrPlayerRig rig;
        private readonly Transform deviceRoot;

        private bool tracked;
        private float mechanicalAngleDeg;
        private float visualAngleDeg;
        private float angularVelocity;
        private bool hasNewSample;
        private ulong frameCounter;
        private float lastSampleTime;
        private AngleSample latestSample;

        public ControllerAngleSource(ArmWrestlingConfig config, XrPlayerRig rig, Transform deviceRoot)
        {
            this.config = config;
            this.rig = rig;
            this.deviceRoot = deviceRoot;
        }

        public string Name
        {
            get { return "VR 手柄"; }
        }

        public LinkStatus Status
        {
            get { return tracked ? LinkStatus.Connected : LinkStatus.Stale; }
        }

        public string Detail
        {
            get { return tracked ? "右手柄实时控制" : "未追踪到右手柄"; }
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
            if (rig == null || deviceRoot == null)
            {
                tracked = false;
                return;
            }

            Pose pose;
            tracked = rig.TryGetControllerPose(XRNode.RightHand, out pose);
            if (!tracked)
            {
                return;
            }

            Vector3 local = deviceRoot.InverseTransformPoint(pose.position);

            // 摆臂旋转平面为装置坐标系的 XY 平面，转轴在 (0, SwingPivotHeight, 0)。
            float theta = Mathf.Atan2(local.x, local.y - ArenaFactory.SwingPivotHeight) * Mathf.Rad2Deg;
            float targetVisual = Mathf.Clamp(theta, -config.visualLimitDeg, config.visualLimitDeg);

            // 一阶平滑，避免手抖直接传到画面上。
            float smoothing = 1f - Mathf.Exp(-Mathf.Max(0.001f, deltaTime) / 0.02f);
            float previous = visualAngleDeg;
            visualAngleDeg = Mathf.Lerp(visualAngleDeg, targetVisual, Mathf.Clamp01(smoothing));

            if (deltaTime > 0.0001f)
            {
                angularVelocity = (visualAngleDeg - previous) / deltaTime;
            }

            mechanicalAngleDeg = config.ToMechanicalDegrees(visualAngleDeg);

            double now = Time.unscaledTimeAsDouble;
            if (now - lastSampleTime >= 1.0 / 90.0)
            {
                lastSampleTime = (float)now;
                hasNewSample = true;
                latestSample = new AngleSample
                {
                    localTime = now,
                    angleDeg = mechanicalAngleDeg,
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
                Debug.Log("[ArmWrestling][手柄] 事件 " + code);
            }
        }

        public void Reconnect()
        {
            tracked = false;
            visualAngleDeg = 0f;
        }

        public void Shutdown()
        {
        }
    }
}
