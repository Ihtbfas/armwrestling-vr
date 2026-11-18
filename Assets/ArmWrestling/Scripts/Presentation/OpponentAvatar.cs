using UnityEngine;

namespace ArmWrestlingVR
{
    /// <summary>
    /// 用 Animator IK 驱动导入的人物模型：
    /// 右手跟住握持点（跟着摆臂角度动），左手搭在桌上，双脚踩地形成坐姿。
    ///
    /// </summary>
    public class OpponentAvatar : MonoBehaviour
    {
        public Transform rightHandTarget;
        public Transform leftHandTarget;
        public Transform rightElbowHint;
        public Transform leftElbowHint;

        public Vector3 leftFootLocal;
        public Vector3 rightFootLocal;
        public bool poseFeet = true;

        /// <summary>调试开关：关掉后模型保持 T-pose，用来区分"没渲染"和"IK 摆坏了"。</summary>
        public static bool IkEnabled = true;

        private Animator animator;
        private bool ready;
        private VirtualArmRig armRig;
        private Vector3 restBodyPosition;
        private Quaternion restBodyRotation;
        private bool hasRestBodyPose;
        private OpponentGripPose gripPose;
        private ArmWrestlingApp match;
        private Vector3 bodyPosition, shoulderFromBody;
        private Quaternion bodyRotation;
        private float upperArmLength, forearmLength;
        private bool useStableElbow;
        public bool UsedStableElbow { get; private set; }
        public float BodyCorrection { get; private set; }
        public bool UsesAuthoredMotion { get; private set; }
        private static readonly int Effort = Animator.StringToHash("Effort");

        // Ch03's seated poses in the device frame. At neutral, bring the shoulder
        // 41 mm forward to increase elbow flex without moving the wrist goal.
        private static readonly Pose OpponentAhead = new Pose(new Vector3(-0.012f, 0.005f, -0.026f),
            Quaternion.Euler(-31f, -4f, 7f));
        private static readonly Pose Neutral = new Pose(new Vector3(0f, 0f, -0.041f), Quaternion.Euler(-27f, 0f, 0f));
        private static readonly Pose PlayerAhead = new Pose(new Vector3(0.012f, -0.012f, -0.027f),
            Quaternion.Euler(-30f, -10f, -9f));

        // Pole offsets use the fitted demo pad and Ch03's unmodified two-bone reach.
        // The grip aim is kept separate so wrist orientation can be tuned independently.
        private static readonly Vector3 OpponentAheadPole = new Vector3(-0.004f, 0.038f, 0.026f);
        private static readonly Vector3 NeutralPole = new Vector3(0.039f, -0.034f, -0.022f);
        private static readonly Vector3 PlayerAheadPole = new Vector3(0.026f, -0.033f, -0.008f);
        // At the demo limits the grip drops further. Blend these corrections
        // outside the authored ±30° poses to keep the elbow above the pad.
        private static readonly Vector3 OpponentLimitBodyOffset = new Vector3(0.033f, -0.021f, 0.034f);
        private static readonly Vector3 OpponentLimitPoleOffset = new Vector3(-0.040f, 0.024f, 0.021f);
        private static readonly Vector3 OpponentLimitAimOffset = new Vector3(-0.003f, 0.023f, 0.026f);
        private static readonly Vector3 PlayerLimitPoleOffset = new Vector3(-0.002f, 0.005f, 0f);

        public int IkPassCount { get; private set; }
        public Vector3 DesiredRightWrist { get; private set; }
        [Tooltip("Blend the three seated body poses by visual angle before solving hand and foot contact.")]
        public bool BlendBodyPoses = true;
        [Tooltip("Local Ch03 trial. Apply an authored right-hand grip during the existing IK pass.")]
        public bool useGripPose;

        public void EnableCharacterMotion(ArmWrestlingApp app)
        {
            useStableElbow = true;
            useGripPose = true;
            match = app;
            var controller = Resources.Load<RuntimeAnimatorController>("Characters/OpponentMotion");
            if (controller == null || animator == null) return;
            animator.runtimeAnimatorController = controller;
            UsesAuthoredMotion = true;
        }

        private void Update()
        {
            if (!UsesAuthoredMotion || match == null || !match.enabled) return;
            float effort = match.state == AppState.Fighting
                ? Mathf.Lerp(0.65f, 1f, Mathf.Abs(armRig.VisualAngleDeg) / 30f) : 0f;
            animator.SetFloat(Effort, effort, 0.25f, Time.deltaTime);
        }

        public void Bind(Transform rightHand, Transform leftHand, Transform rightElbow, Transform leftElbow,
            Vector3 leftFoot, Vector3 rightFoot, VirtualArmRig rig)
        {
            rightHandTarget = rightHand;
            leftHandTarget = leftHand;
            rightElbowHint = rightElbow;
            leftElbowHint = leftElbow;
            leftFootLocal = leftFoot;
            rightFootLocal = rightFoot;
            armRig = rig;

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
            IkPassCount++;
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

            if (armRig == null) armRig = GetComponentInParent<VirtualArmRig>();
            if (armRig == null) return;
            Transform frame = armRig.transform;
            if (!hasRestBodyPose)
            {
                restBodyPosition = frame.InverseTransformPoint(animator.bodyPosition);
                restBodyRotation = Quaternion.Inverse(frame.rotation) * animator.bodyRotation;
                hasRestBodyPose = true;
            }
            float angle = armRig.VisualAngleDeg;
            float limitWeight = Mathf.InverseLerp(30f, 42f, Mathf.Abs(angle));
            ApplyBodyPose(frame, angle, limitWeight);
            bodyPosition = animator.bodyPosition;
            bodyRotation = animator.bodyRotation;
            UsedStableElbow = false;
            BodyCorrection = 0f;

            if (rightHandTarget != null && rightElbowHint != null)
            {
                Vector3 aim = rightElbowHint.localPosition;
                float gripX = frame.InverseTransformPoint(rightHandTarget.position).x;
                aim.x = Mathf.Clamp(gripX - 0.15f, -0.28f, -0.11f);
                Vector3 endPole = angle < 0f ? OpponentAheadPole : PlayerAheadPole;
                Vector3 limitPole = angle < 0f ? OpponentLimitPoleOffset : PlayerLimitPoleOffset;
                Vector3 pole = Vector3.Lerp(NeutralPole, endPole, Mathf.Abs(angle) / 30f) + limitPole * limitWeight;
                Vector3 elbow = frame.TransformPoint(aim + pole);
                // At the negative limit, align the palm more closely with the
                // forearm while keeping the same grip centre and wrist setback.
                if (angle < 0f) aim += OpponentLimitAimOffset * limitWeight;
                Vector3 fingers = (rightHandTarget.position - frame.TransformPoint(aim)).normalized;
                if (useStableElbow && TrySupportedElbow(frame, rightHandTarget.position, out Vector3 supported))
                {
                    elbow = supported;
                    fingers = (rightHandTarget.position - elbow).normalized;
                    UsedStableElbow = true;
                }
                // The swing anchor is the centre of the joined palms, ahead of the wrist.
                DesiredRightWrist = rightHandTarget.position - fingers * 0.045f;
                ApplyHand(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow,
                    DesiredRightWrist, Quaternion.LookRotation(fingers, -frame.right), elbow);
            }
            if (leftHandTarget != null && leftElbowHint != null)
                ApplyHand(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow,
                    leftHandTarget.position, leftHandTarget.rotation, leftElbowHint.position);

            if (poseFeet)
            {
                ApplyFoot(frame, AvatarIKGoal.LeftFoot, AvatarIKHint.LeftKnee, leftFootLocal);
                ApplyFoot(frame, AvatarIKGoal.RightFoot, AvatarIKHint.RightKnee, rightFootLocal);
            }
            if (useGripPose) gripPose?.Apply(animator);
        }

        private void LateUpdate()
        {
            if (gripPose == null && ready && IkPassCount > 0)
            {
                gripPose = new OpponentGripPose(animator);
                if (armRig != null && armRig.playerVisual != null) armRig.playerVisual.SetGripPose(gripPose);
            }
            if (useStableElbow && upperArmLength == 0f && ready && IkPassCount > 0)
            {
                var shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                var elbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                var wrist = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (shoulder == null || elbow == null || wrist == null) return;
                upperArmLength = Vector3.Distance(shoulder.position, elbow.position);
                forearmLength = Vector3.Distance(elbow.position, wrist.position);
                shoulderFromBody = Quaternion.Inverse(bodyRotation) * (shoulder.position - bodyPosition);
            }
        }

        private bool TrySupportedElbow(Transform frame, Vector3 grip, out Vector3 supported)
        {
            supported = default;
            if (upperArmLength <= 0f || forearmLength <= 0f) return false;
            // The pad top is at 95 mm. The elbow bone sits above the sampled
            // skin. A straight forearm-to-palm line gives a sphere about grip.
            const float boneY = 0.109f;
            Vector3 g = frame.InverseTransformPoint(grip);
            float radius = (forearmLength + 0.045f) / frame.lossyScale.x;
            float vertical = g.y - boneY;
            float horizontalSq = radius * radius - vertical * vertical;
            if (horizontalSq <= 0f) return false;
            float horizontal = Mathf.Sqrt(horizontalSq);
            Vector2 toward = new Vector2(-0.025f - g.x, 0.14f - g.z).normalized;
            float x = Mathf.Clamp(g.x + toward.x * horizontal, -0.29f, -0.021f);
            float depthSq = horizontalSq - (x - g.x) * (x - g.x);
            if (depthSq < 0f) return false;
            float z = g.z + Mathf.Sqrt(depthSq);
            if (z < 0.055f || z > 0.23f) return false;
            supported = frame.TransformPoint(new Vector3(x, boneY, z));

            Vector3 shoulder = bodyPosition + bodyRotation * shoulderFromBody;
            Vector3 toElbow = supported - shoulder;
            Vector3 shift = toElbow.normalized * (toElbow.magnitude - upperArmLength);
            // Keep the fallback if the fitted posture needs a large torso move.
            if (shift.sqrMagnitude > 0.07f * 0.07f) return false;
            animator.bodyPosition = bodyPosition + shift;
            BodyCorrection = shift.magnitude;
            return true;
        }

        private void ApplyBodyPose(Transform frame, float visualAngle, float limitWeight)
        {
            // Ease across both pose intervals with zero tangent at neutral.
            float angle = BlendBodyPoses ? visualAngle : 0f;
            Pose endpoint = angle < 0f ? OpponentAhead : PlayerAhead;
            float weight = Mathf.SmoothStep(0f, 1f, Mathf.Abs(angle) / 30f);
            Vector3 offset = Vector3.Lerp(Neutral.position, endpoint.position, weight);
            if (angle < 0f) offset += OpponentLimitBodyOffset * limitWeight;
            Quaternion lean = Quaternion.Slerp(Neutral.rotation, endpoint.rotation, weight);
            animator.bodyPosition = frame.TransformPoint(restBodyPosition + offset);
            animator.bodyRotation = frame.rotation * lean * restBodyRotation;
        }

        private void ApplyHand(AvatarIKGoal goal, AvatarIKHint hint, Vector3 position, Quaternion rotation, Vector3 elbow)
        {
            animator.SetIKPositionWeight(goal, 1f);
            animator.SetIKRotationWeight(goal, 1f);
            animator.SetIKPosition(goal, position);
            animator.SetIKRotation(goal, rotation);
            animator.SetIKHintPositionWeight(hint, 1f);
            animator.SetIKHintPosition(hint, elbow);
        }

        private void ApplyFoot(Transform frame, AvatarIKGoal goal, AvatarIKHint hint, Vector3 localPosition)
        {
            Vector3 knee = localPosition + new Vector3(0f, 0.42f, -0.04f);
            animator.SetIKHintPositionWeight(hint, 1f);
            animator.SetIKHintPosition(hint, frame.TransformPoint(knee));
            animator.SetIKPositionWeight(goal, 1f);
            animator.SetIKRotationWeight(goal, 1f);
            animator.SetIKPosition(goal, frame.TransformPoint(localPosition));
            animator.SetIKRotation(goal, Quaternion.LookRotation(-frame.forward, frame.up));
        }
    }
}
