using UnityEngine;
using UnityEngine.XR;

namespace ArmWrestlingVR
{
    /// <summary>
    /// 虚实空间标定：把虚拟桌面放到真实桌面上。
    ///
    /// 原理（对应开题报告 4.1.4 (3)、4.2.4 (1)）：
    /// PICO 建立的追踪坐标系与机械装置之间没有固有关联。如果夹具能把 VR 手柄以
    /// 可重复的位置和朝向固定住，那么“手柄在装置坐标系中的位姿”是一个已知常量
    /// <see cref="fixturePoseInDeviceSpace"/>。于是：
    /// <code>
    ///   T_追踪←装置 = T_追踪←手柄 × inverse(T_装置←手柄)
    /// </code>
    /// 即可把整个虚拟装置摆到正确位置。两点方案作为备选：用两个基准点确定方向，
    /// 并用“装置水平放置”的竖直约束确定滚转角。
    /// </summary>
    public class DeviceFrameCalibration : MonoBehaviour
    {
        private const string KeyPrefix = "ArmWrestling.Calib.";

        public Transform deviceRoot;
        public XrPlayerRig rig;

        [Tooltip("手柄放在夹具中时，手柄（grip 姿态）在装置坐标系中的位置。必须由机械组实测。")]
        public Vector3 fixturePositionInDevice = new Vector3(0f, 0.06f, -0.42f);

        [Tooltip("手柄放在夹具中时，手柄在装置坐标系中的朝向。+Z 与装置 Z 轴同向时填入 identity。")]
        public Vector3 fixtureEulerInDevice = Vector3.zero;

        [Tooltip("第二基准点在装置坐标系中的位置，用于两点标定。")]
        public Vector3 secondReferenceInDevice = new Vector3(0f, 0f, 0.45f);

        [SerializeField] private bool calibrated;
        [SerializeField] private float lastRepeatabilityMeters;

        public bool IsCalibrated
        {
            get { return calibrated; }
        }

        /// <summary>两次单点标定得到的装置位置之差，可作为夹具重复定位精度的粗略指标。</summary>
        public float LastRepeatabilityMeters
        {
            get { return lastRepeatabilityMeters; }
        }

        public CalibrationPose FixturePoseInDeviceSpace
        {
            get
            {
                return new CalibrationPose(
                    fixturePositionInDevice,
                    Quaternion.Euler(fixtureEulerInDevice));
            }
        }

        // ------------------------------------------------------------------
        // 单点标定
        // ------------------------------------------------------------------

        /// <summary>根据手柄当前位姿执行单点标定。</summary>
        public bool TryCaptureSingle(Pose trackedHandPose, out string message)
        {
            if (deviceRoot == null)
            {
                message = "未指定虚拟装置根节点";
                return false;
            }

            CalibrationPose fixture = FixturePoseInDeviceSpace;
            Quaternion deviceRotation = trackedHandPose.rotation * Quaternion.Inverse(fixture.rotation);
            Vector3 devicePosition = trackedHandPose.position - deviceRotation * fixture.position;

            Pose candidate = new Pose(devicePosition, deviceRotation);

            if (!IsPlausible(candidate, out message))
            {
                return false;
            }

            if (calibrated)
            {
                lastRepeatabilityMeters = Vector3.Distance(deviceRoot.position, candidate.position);
            }

            deviceRoot.SetPositionAndRotation(candidate.position, candidate.rotation);
            calibrated = true;
            Save();
            message = "标定完成，桌面高度 " + candidate.position.y.ToString("F2") + " m";
            return true;
        }

        // ------------------------------------------------------------------
        // 两点标定（备选）
        // ------------------------------------------------------------------

        /// <summary>用两个分离的基准点确定装置位姿，并用水平面约束消除滚转二义性。</summary>
        public bool TryCaptureTwoPoint(Pose firstHandPose, Pose secondHandPose, out string message)
        {
            if (deviceRoot == null)
            {
                message = "未指定虚拟装置根节点";
                return false;
            }

            CalibrationPose fixture = FixturePoseInDeviceSpace;

            Vector3 deviceA = fixture.position;
            Vector3 deviceB = secondReferenceInDevice;

            Vector3 deviceDirection = deviceB - deviceA;
            Vector3 trackedDirection = secondHandPose.position - firstHandPose.position;

            if (deviceDirection.sqrMagnitude < 0.0004f || trackedDirection.sqrMagnitude < 0.0004f)
            {
                message = "两点距离过近，请拉开两个基准点";
                return false;
            }

            Quaternion deviceRotation =
                Quaternion.LookRotation(trackedDirection.normalized, Vector3.up)
                * Quaternion.Inverse(Quaternion.LookRotation(deviceDirection.normalized, Vector3.up));

            Vector3 devicePosition = firstHandPose.position - deviceRotation * deviceA;

            Pose candidate = new Pose(devicePosition, deviceRotation);
            if (!IsPlausible(candidate, out message))
            {
                return false;
            }

            deviceRoot.SetPositionAndRotation(candidate.position, candidate.rotation);
            calibrated = true;
            Save();
            message = "两点标定完成";
            return true;
        }

        // ------------------------------------------------------------------
        // 应急方案
        // ------------------------------------------------------------------

        /// <summary>没有夹具时，把虚拟桌面放到使用者正前方（仅用于快速验证）。</summary>
        public void AlignAheadOfView(float distance = 0.62f, float tableTopBelowEye = 0.40f, bool persist = true)
        {
            if (deviceRoot == null)
            {
                return;
            }

            Pose pose;
            if (rig != null && rig.head != null)
            {
                pose = rig.SuggestDevicePoseAheadOfView(distance, tableTopBelowEye);
            }
            else
            {
                pose = new Pose(new Vector3(0f, 0.75f, distance), Quaternion.identity);
            }

            deviceRoot.SetPositionAndRotation(pose.position, pose.rotation);
            calibrated = persist;

            if (persist)
            {
                Save();
            }
        }

        /// <summary>
        /// 手动微调虚拟装置的位姿（没有定位夹具时的兜底方案）。
        /// axis: 0=X(左右) 1=Z(前后) 2=Y(上下) 3=绕竖轴旋转；direction: +1/-1。
        /// </summary>
        public void Nudge(int axis, int direction, float stepMeters = 0.02f, float stepDegrees = 3f)
        {
            if (deviceRoot == null)
            {
                return;
            }

            Vector3 delta = Vector3.zero;
            switch (axis)
            {
                case 0:
                    delta = deviceRoot.right * (direction * stepMeters);
                    break;
                case 1:
                    delta = deviceRoot.forward * (direction * stepMeters);
                    break;
                case 2:
                    delta = Vector3.up * (direction * stepMeters);
                    break;
                case 3:
                    deviceRoot.Rotate(Vector3.up, direction * stepDegrees, Space.World);
                    break;
            }

            if (delta != Vector3.zero)
            {
                deviceRoot.position += delta;
            }

            calibrated = true;
        }

        public void ResetCalibration()
        {
            calibrated = false;
            lastRepeatabilityMeters = 0f;
            PlayerPrefs.DeleteKey(KeyPrefix + "x");
            PlayerPrefs.DeleteKey(KeyPrefix + "y");
            PlayerPrefs.DeleteKey(KeyPrefix + "z");
            PlayerPrefs.DeleteKey(KeyPrefix + "qx");
            PlayerPrefs.DeleteKey(KeyPrefix + "qy");
            PlayerPrefs.DeleteKey(KeyPrefix + "qz");
            PlayerPrefs.DeleteKey(KeyPrefix + "qw");
            PlayerPrefs.Save();
        }

        public bool Load()
        {
            if (deviceRoot == null || !PlayerPrefs.HasKey(KeyPrefix + "x"))
            {
                return false;
            }

            Vector3 position = new Vector3(
                PlayerPrefs.GetFloat(KeyPrefix + "x"),
                PlayerPrefs.GetFloat(KeyPrefix + "y"),
                PlayerPrefs.GetFloat(KeyPrefix + "z"));

            Quaternion rotation = new Quaternion(
                PlayerPrefs.GetFloat(KeyPrefix + "qx"),
                PlayerPrefs.GetFloat(KeyPrefix + "qy"),
                PlayerPrefs.GetFloat(KeyPrefix + "qz"),
                PlayerPrefs.GetFloat(KeyPrefix + "qw"));

            deviceRoot.SetPositionAndRotation(position, rotation);
            calibrated = true;
            return true;
        }

        public void Save()
        {
            if (deviceRoot == null)
            {
                return;
            }

            Vector3 p = deviceRoot.position;
            Quaternion q = deviceRoot.rotation;
            PlayerPrefs.SetFloat(KeyPrefix + "x", p.x);
            PlayerPrefs.SetFloat(KeyPrefix + "y", p.y);
            PlayerPrefs.SetFloat(KeyPrefix + "z", p.z);
            PlayerPrefs.SetFloat(KeyPrefix + "qx", q.x);
            PlayerPrefs.SetFloat(KeyPrefix + "qy", q.y);
            PlayerPrefs.SetFloat(KeyPrefix + "qz", q.z);
            PlayerPrefs.SetFloat(KeyPrefix + "qw", q.w);
            PlayerPrefs.Save();
        }

        /// <summary>合理性检查：桌面高度与倾斜角都在可接受范围内，避免标定结果把场景甩到天上。</summary>
        private bool IsPlausible(Pose candidate, out string message)
        {
            float height = candidate.position.y;
            if (height < 0.3f || height > 1.3f)
            {
                message = "标定结果高度异常（" + height.ToString("F2") + " m），请检查手柄是否放在夹具中";
                return false;
            }

            float tilt = Vector3.Angle(candidate.rotation * Vector3.up, Vector3.up);
            if (tilt > 35f)
            {
                message = "标定结果倾斜过大（" + tilt.ToString("F1") + "°），请检查夹具朝向";
                return false;
            }

            message = string.Empty;
            return true;
        }

        /// <summary>标定流程中把右手柄放好后按扳机采集。返回 false 表示当帧未采集。</summary>
        public bool TryCaptureFromController(XRNode node, out string message)
        {
            message = string.Empty;
            if (rig == null)
            {
                message = "缺少玩家装置引用";
                return false;
            }

            Pose pose;
            if (!rig.TryGetControllerPose(node, out pose))
            {
                message = "未追踪到手柄，请先戴上头显并确认手柄已唤醒";
                return false;
            }

            return TryCaptureSingle(pose, out message);
        }
    }
}
