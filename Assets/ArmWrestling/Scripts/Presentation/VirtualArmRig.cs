using System;
using UnityEngine;

namespace ArmWrestlingVR
{
    /// <summary>一条手臂的三段可视骨骼（上臂、前臂、手），由解析式两骨骼 IK 驱动。</summary>
    [Serializable]
    public class ArmChain
    {
        public Transform upperArm;
        public Transform forearm;
        public Transform hand;
        public Transform shoulderAnchor;
        public Transform elbowHint;

        public float upperLength = 0.30f;
        public float forearmLength = 0.27f;
        public float upperRadius = 0.055f;
        public float forearmRadius = 0.045f;
        public float handRadius = 0.055f;

        /// <summary>
        /// 把肘部固定在肘垫上、手固定在握持点上，求上臂与前臂姿态。
        /// 采用解析式两骨骼 IK：肩到手距离已知，骨长已知，肘关节位置由“极点方向”决定。
        /// 这样即使只有单自由度输入，虚拟手臂也不会出现关节扭曲或手掌脱离握持位置。
        /// </summary>
        public void Solve(Vector3 shoulder, Vector3 handTarget, Vector3 poleHint, Vector3 handUp, Quaternion handRotation)
        {
            if (upperArm == null || forearm == null || hand == null)
            {
                return;
            }

            SolveJoints(shoulder, handTarget, poleHint, handUp, upperLength, forearmLength, out var elbow, out var wrist);
            PlaceBone(upperArm, shoulder, elbow, upperRadius);
            PlaceBone(forearm, elbow, wrist, forearmRadius);
            hand.position = wrist;
            hand.rotation = handRotation != Quaternion.identity ? handRotation
                : Quaternion.FromToRotation(Vector3.up, (wrist - elbow).normalized);
            hand.localScale = Vector3.one * (handRadius * 2f);
        }

        public static void SolveJoints(Vector3 shoulder, Vector3 handTarget, Vector3 poleHint, Vector3 handUp,
            float upperLength, float forearmLength, out Vector3 elbow, out Vector3 wrist)
        {
            Vector3 toTarget = handTarget - shoulder;
            float distance = toTarget.magnitude;
            float maxReach = upperLength + forearmLength - 0.001f;

            Vector3 direction = distance > 0.0001f ? toTarget / distance : Vector3.down;
            distance = Mathf.Clamp(distance, 0.02f, maxReach);

            float along = (upperLength * upperLength - forearmLength * forearmLength + distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));

            Vector3 pole = Vector3.ProjectOnPlane(poleHint - shoulder, direction);
            if (pole.sqrMagnitude < 1e-6f)
            {
                pole = Vector3.ProjectOnPlane(handUp, direction);
            }

            if (pole.sqrMagnitude < 1e-6f)
            {
                pole = Vector3.right;
            }

            pole.Normalize();

            elbow = shoulder + direction * along + pole * height;
            wrist = shoulder + direction * distance;
        }

        /// <summary>把一根胶囊骨骼摆到两点之间，并拉伸到对应长度。</summary>
        public static void PlaceBone(Transform bone, Vector3 from, Vector3 to, float radius)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.0005f)
            {
                return;
            }

            bone.position = from + delta * 0.5f;
            bone.rotation = Quaternion.FromToRotation(Vector3.up, delta / length);

            // Unity 胶囊默认高 2、直径 1：y 缩放 = 长度/2，xz 缩放 = 直径。
            bone.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
        }
    }

    /// <summary>
    /// 把机械角度翻译成虚拟手臂姿态。
    ///
    /// 所有几何都在“装置坐标系”内定义（原点 = 桌面中心，+Z 指向对手，+Y 向上）：
    /// 摆臂绕 +Z 轴旋转，握持点位于摆臂末端；玩家与对手的肘部各自压在自己的肘垫上，
    /// 两条手臂都通过 IK 把手收拢到同一个握持点，因此视觉上始终是“双手扣在一起”。
    /// </summary>
    public class VirtualArmRig : MonoBehaviour
    {
        [Header("机械摆臂")]
        public Transform swingPivot;
        public Transform gripAnchor;
        public float swingLength = 0.30f;

        [Header("手臂")]
        public ArmChain playerArm;
        public ArmChain opponentArm;
        public PlayerArmVisual playerVisual;

        [Tooltip("两只手在握持点两侧的微小错开距离，避免穿模。")]
        public float handSeparation = 0.035f;

        [Header("状态")]
        [SerializeField] private float visualAngleDeg;

        private bool initialised;

        public float VisualAngleDeg
        {
            get { return visualAngleDeg; }
        }

        /// <summary>握持点的世界坐标。</summary>
        public Vector3 GripWorldPosition
        {
            get { return gripAnchor != null ? gripAnchor.position : transform.position; }
        }

        public void ApplyAngle(float visualAngle)
        {
            visualAngleDeg = visualAngle;

            if (swingPivot != null)
            {
                // 视觉角度为正表示“我方推进”，对应握持点转向玩家右侧（-Z 轴旋转）。
                swingPivot.localRotation = Quaternion.Euler(0f, 0f, -visualAngle);
            }

            Vector3 grip = GripWorldPosition;
            Vector3 lateral = transform.right;

            if (playerArm != null && playerArm.shoulderAnchor != null)
            {
                Vector3 pole = playerArm.elbowHint != null ? playerArm.elbowHint.position : grip + Vector3.down;
                playerArm.Solve(
                    playerArm.shoulderAnchor.position,
                    grip - lateral * handSeparation,
                    pole,
                    Vector3.up,
                    Quaternion.LookRotation(lateral, Vector3.up));
            }

            if (opponentArm != null && opponentArm.shoulderAnchor != null)
            {
                Vector3 pole = opponentArm.elbowHint != null ? opponentArm.elbowHint.position : grip + Vector3.down;
                opponentArm.Solve(
                    opponentArm.shoulderAnchor.position,
                    grip + lateral * handSeparation,
                    pole,
                    Vector3.up,
                    Quaternion.LookRotation(-lateral, Vector3.up));
            }

            if (playerVisual != null && !playerVisual.UsesHandContact) playerVisual.ApplyGrip(transform, grip);
            initialised = true;
        }

        private void LateUpdate()
        {
            // 场景初次加载或手臂被外部改动时，保持姿态一致。
            if (!initialised)
            {
                ApplyAngle(visualAngleDeg);
            }
        }
    }
}
