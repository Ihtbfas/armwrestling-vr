using UnityEngine;

namespace ArmWrestlingVR
{
    /// <summary>
    /// 用 Animator IK 驱动导入的人物模型：
    /// 右手跟住握持点（跟着摆臂角度动），左手搭在桌上，双脚踩地形成坐姿。
    ///
    /// 为什么不用动画片段：我们的角度是实时算出来的，播放固定动画会对不上；
    /// IK 的好处是"给个目标点，模型自己摆姿势"。
    /// </summary>
    public class OpponentAvatar : MonoBehaviour
    {
        public Transform rightHandTarget;
        public Transform leftHandTarget;
        public Transform rightElbowHint;
        public Transform leftElbowHint;

        public Vector3 leftFootWorld;
        public Vector3 rightFootWorld;
        public bool poseFeet = true;

        /// <summary>调试开关：关掉后模型保持 T-pose，用来区分"没渲染"和"IK 摆坏了"。</summary>
        public static bool IkEnabled = true;

        private Animator animator;
        private bool ready;

        public void Bind(Transform rightHand, Transform leftHand, Transform rightElbow, Transform leftElbow,
            Vector3 leftFoot, Vector3 rightFoot)
        {
            rightHandTarget = rightHand;
            leftHandTarget = leftHand;
            rightElbowHint = rightElbow;
            leftElbowHint = leftElbow;
            leftFootWorld = leftFoot;
            rightFootWorld = rightFoot;

            animator = GetComponent<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                ready = animator.isHuman;
            }

            if (!ready)
            {
                Debug.LogWarning("[ArmWrestling] 人物模型不是 Humanoid，IK 不可用");
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (!IkEnabled)
            {
                return;
            }

            if (!ready)
            {
                animator = GetComponent<Animator>();
                ready = animator != null && animator.isHuman;
                if (!ready)
                {
                    return;
                }
            }

            ApplyHand(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, rightHandTarget, rightElbowHint);
            ApplyHand(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, leftHandTarget, leftElbowHint);

            if (poseFeet)
            {
                ApplyFoot(AvatarIKGoal.LeftFoot, AvatarIKHint.LeftKnee, leftFootWorld, new Vector3(0.25f, 1f, 0.2f));
                ApplyFoot(AvatarIKGoal.RightFoot, AvatarIKHint.RightKnee, rightFootWorld, new Vector3(-0.25f, 1f, 0.2f));
            }
        }

        private void ApplyHand(AvatarIKGoal goal, AvatarIKHint hint, Transform target, Transform hintTarget)
        {
            if (target != null)
            {
                animator.SetIKPositionWeight(goal, 1f);
                animator.SetIKRotationWeight(goal, 1f);
                animator.SetIKPosition(goal, target.position);
                animator.SetIKRotation(goal, target.rotation);
            }

            if (hintTarget != null)
            {
                animator.SetIKHintPositionWeight(hint, 1f);
                animator.SetIKHintPosition(hint, hintTarget.position);
            }
        }

        private void ApplyFoot(AvatarIKGoal goal, AvatarIKHint hint, Vector3 worldPosition, Vector3 hintOffset)
        {
            // 让膝盖朝外前方，脚才不会被 IK 拧成反关节
            animator.SetIKHintPositionWeight(hint, 1f);
            animator.SetIKHintPosition(hint, worldPosition + hintOffset * 0.5f + Vector3.forward * 0.45f);

            animator.SetIKPositionWeight(goal, 1f);
            animator.SetIKRotationWeight(goal, 0.2f);
            animator.SetIKPosition(goal, worldPosition);
            animator.SetIKRotation(goal, Quaternion.LookRotation(Vector3.forward, Vector3.up));
        }
    }
}
