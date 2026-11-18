using UnityEngine;
using UnityEngine.XR;

namespace ArmWrestlingVR
{
    /// <summary>
    /// 极简 XR 玩家装置：头部与双手位姿直接来自 OpenXR 的追踪子系统。
    ///
    /// 这里刻意不使用 XR Interaction Toolkit 的控制器预制体，原因有两点：
    /// 1. 本系统不需要“抓取/投掷”这类通用交互，只需要位姿与按键；
    /// 2. 直接读 <see cref="InputDevices"/> 不依赖 Input System 的动作资源，
    ///    在 PC 串流与 PICO 4 独立 APK 两种运行方式下行为一致。
    ///
    /// 没有检测到头显时自动退化为桌面模式：鼠标看四周、键盘不动，方便在编辑器里调场景。
    /// </summary>
    public class XrPlayerRig : MonoBehaviour
    {
        [Header("追踪节点")]
        public Transform head;
        public Transform leftHand;
        public Transform rightHand;

        [Header("桌面调试")]
        public bool desktopMouseLook = true;
        public float mouseSensitivity = 2.2f;
        public Vector3 desktopEyeLocalPosition = new Vector3(0f, 1.15f, 0f);

        private bool headDeviceValid;
        private bool leftDeviceValid;
        private bool rightDeviceValid;
        private float desktopYaw;
        private float desktopPitch;

        public bool XrActive
        {
            get { return XRSettings.isDeviceActive; }
        }

        public bool HeadTracked
        {
            get { return headDeviceValid; }
        }

        public Pose HeadPose
        {
            get { return head != null ? new Pose(head.position, head.rotation) : new Pose(Vector3.zero, Quaternion.identity); }
        }

        private void Awake()
        {
            if (desktopEyeLocalPosition == Vector3.zero)
            {
                desktopEyeLocalPosition = new Vector3(0f, 1.15f, 0f);
            }
        }

        private void Update()
        {
            if (XrActive)
            {
                UpdateXrTracking();
            }
            else
            {
                UpdateDesktopView();
            }
        }

        private void UpdateXrTracking()
        {
            headDeviceValid = TryReadNode(XRNode.Head, head, CommonUsages.centerEyePosition, CommonUsages.centerEyeRotation);
            leftDeviceValid = TryReadNode(XRNode.LeftHand, leftHand, CommonUsages.devicePosition, CommonUsages.deviceRotation);
            rightDeviceValid = TryReadNode(XRNode.RightHand, rightHand, CommonUsages.devicePosition, CommonUsages.deviceRotation);
        }

        private static bool TryReadNode(XRNode node, Transform target, InputFeatureUsage<Vector3> positionUsage, InputFeatureUsage<Quaternion> rotationUsage)
        {
            if (target == null)
            {
                return false;
            }

            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid)
            {
                return false;
            }

            Vector3 position;
            Quaternion rotation;
            bool hasPosition = device.TryGetFeatureValue(positionUsage, out position);
            bool hasRotation = device.TryGetFeatureValue(rotationUsage, out rotation);

            if (hasPosition)
            {
                target.localPosition = position;
            }

            if (hasRotation)
            {
                target.localRotation = rotation;
            }

            return hasPosition || hasRotation;
        }

        /// <summary>
        /// 读取某一个手柄当前的位姿（追踪坐标系下）。标定流程用它采集手柄在夹具中的姿态。
        /// </summary>
        public bool TryGetControllerPose(XRNode node, out Pose pose)
        {
            pose = new Pose(Vector3.zero, Quaternion.identity);

            if (!XrActive)
            {
                return false;
            }

            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid)
            {
                return false;
            }

            Vector3 position;
            Quaternion rotation;
            if (!device.TryGetFeatureValue(CommonUsages.devicePosition, out position)
                || !device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation))
            {
                return false;
            }

            pose = new Pose(position, rotation);
            return true;
        }

        /// <summary>读取手柄扳机/握持键，用于 UI 点击。</summary>
        public bool TryGetTrigger(XRNode node, out float value)
        {
            value = 0f;
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid)
            {
                return false;
            }

            if (device.TryGetFeatureValue(CommonUsages.trigger, out value))
            {
                return true;
            }

            bool pressed;
            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out pressed) && pressed)
            {
                value = 1f;
                return true;
            }

            return false;
        }

        public bool TryGetPrimaryButton(XRNode node)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            bool pressed;
            if (device.isValid && device.TryGetFeatureValue(CommonUsages.primaryButton, out pressed))
            {
                return pressed;
            }

            return false;
        }

        public bool TryGetSecondaryButton(XRNode node)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            bool pressed;
            if (device.isValid && device.TryGetFeatureValue(CommonUsages.secondaryButton, out pressed))
            {
                return pressed;
            }

            return false;
        }

        private void UpdateDesktopView()
        {
            if (head != null)
            {
                head.localPosition = desktopEyeLocalPosition;
            }

            if (!desktopMouseLook || head == null)
            {
                return;
            }

            if (Input.GetMouseButton(1))
            {
                desktopYaw += Input.GetAxis("Mouse X") * mouseSensitivity;
                desktopPitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
                desktopPitch = Mathf.Clamp(desktopPitch, -75f, 75f);
            }

            head.localRotation = Quaternion.Euler(desktopPitch, desktopYaw, 0f);
        }

        /// <summary>标定后把虚拟装置摆到使用者正前方的默认位置（无夹具时的应急方案）。</summary>
        public Pose SuggestDevicePoseAheadOfView(float distance, float tableTopBelowEye)
        {
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            Vector3 origin = head.position + forward * distance + Vector3.down * tableTopBelowEye;
            return new Pose(origin, Quaternion.LookRotation(forward, Vector3.up));
        }
    }
}
