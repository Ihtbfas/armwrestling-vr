using System;
using UnityEngine;

namespace ArmWrestlingVR
{
    public sealed partial class AvatarCollisionExperiment
    {
        public bool previewClosedPose;
        public float? PreviewAuthoredClosure { get; set; }
        internal ArmWrestlingApp AuthoredMatch { get; set; }
        public float AuthoredClosure { get; private set; }
        private PairedGripPose contactPose;
        public PairedGripPose ResolvedContactPose => contactPose;
        private readonly Quaternion[] contactPlayer = new Quaternion[15];
        private readonly Quaternion[] contactOpponent = new Quaternion[15];
        private readonly float[] closure = new float[10];
        private readonly bool[] blocked = new bool[10];
        private readonly bool[] reject = new bool[10];
        private readonly float[] previousPairDepth = new float[480];
        public double MaxContactSolveMilliseconds { get; private set; }
        public bool reverseContactOrder;
        public float OpenContactDepth { get; private set; }
        public float InitialRootContactDepth { get; private set; }
        public float FinalRootContactDepth { get; private set; }
        private float RootContactDepth()
        {
            float max = 0f;
            if (Overlap(playerContactPalm,opponentContactPalm,out _,out float palm)) max = palm;
            if (Overlap(playerHand[1],opponentContactPalm,out _,out float a)) max = Mathf.Max(max,a);
            if (Overlap(opponentHand[1],playerContactPalm,out _,out float b)) max = Mathf.Max(max,b);
            return max;
        }
        public double ContactBuildMilliseconds { get; private set; }
        public int ContactBuildQueries { get; private set; }
        private Vector3 contactRelativePosition;
        private Quaternion contactRelativeRotation;
        private int contactPoseHash;
        private bool contactCacheValid;
        public int ContactBuilds { get; private set; }
        public int ClosureQueries { get; private set; }
        public double ContactSolveMilliseconds { get; private set; }

        private void SolveAuthoredContact()
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            if (contactPose == null) contactPose = Resources.Load<PairedGripPose>(PairedGripPose.ContactPoseResource);
            if (contactPose == null || !contactPose.HasContactEndpoints)
                throw new InvalidOperationException("ContactGrip requires authored open and closed paired poses.");
            Transform a = playerHand[1].joint.parent, b = opponentHand[1].joint.parent;
            Vector3 relativePosition = b.InverseTransformPoint(a.position);
            Quaternion relativeRotation = Quaternion.Inverse(b.rotation) * a.rotation;
            EnsureContactSurfaces();
            int hash = contactPose.fingerStart.GetHashCode() ^ reverseContactOrder.GetHashCode();
            for (int i = 0; i < 15; i++)
            {
                unchecked
                {
                    hash = hash * 31 + contactPose.player.fingerRotations[i].GetHashCode();
                    hash = hash * 31 + contactPose.opponent.fingerRotations[i].GetHashCode();
                    hash = hash * 31 + contactPose.player.openFingerRotations[i].GetHashCode();
                    hash = hash * 31 + contactPose.opponent.openFingerRotations[i].GetHashCode();
                }
            }
            playerContactPalm.pose = playerContactPalm.sample();
            opponentContactPalm.pose = opponentContactPalm.sample();
            ClosureQueries = 0;
            if (!contactCacheValid || hash != contactPoseHash
                || (relativePosition - contactRelativePosition).sqrMagnitude > 0.0005f * 0.0005f
                || Quaternion.Angle(relativeRotation, contactRelativeRotation) > 0.05f)
            {
                long buildStart = System.Diagnostics.Stopwatch.GetTimestamp();
                BuildContact();
                ContactBuildMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - buildStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                ContactBuildQueries = ClosureQueries;
                contactPoseHash = hash;
                contactRelativePosition = relativePosition;
                contactRelativeRotation = relativeRotation;
                contactCacheValid = true;
            }
            if (contactPose.usesAuthoredEndpoints)
            {
                bool contest = AuthoredMatch != null && (AuthoredMatch.state == AppState.Fighting ||
                    AuthoredMatch.state == AppState.Paused || AuthoredMatch.state == AppState.Finished);
                AuthoredClosure = PreviewAuthoredClosure ??
                    Mathf.MoveTowards(AuthoredClosure, contest ? 1f : 0f, Time.deltaTime * 5f);
            }
            for (int i = 0; i < 15; i++)
            {
                var playerClosed = previewClosedPose ? contactPose.player.fingerRotations[i] : contactPlayer[i];
                var opponentClosed = previewClosedPose ? contactPose.opponent.fingerRotations[i] : contactOpponent[i];
                playerHand[i + 1].joint.localRotation = contactPose.usesAuthoredEndpoints
                    ? Quaternion.Slerp(contactPose.player.openFingerRotations[i], playerClosed, AuthoredClosure) : playerClosed;
                opponentHand[i + 1].joint.localRotation = contactPose.usesAuthoredEndpoints
                    ? Quaternion.Slerp(contactPose.opponent.openFingerRotations[i], opponentClosed, AuthoredClosure) : opponentClosed;
                MaxFingerCorrection = Mathf.Max(MaxFingerCorrection,
                    Quaternion.Angle(contactPlayer[i], contactPose.player.fingerRotations[i]),
                    Quaternion.Angle(contactOpponent[i], contactPose.opponent.fingerRotations[i]));
            }
            RefreshHand(playerHand); RefreshHand(opponentHand);
            ContactSolveMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            MaxContactSolveMilliseconds = Math.Max(MaxContactSolveMilliseconds, ContactSolveMilliseconds);
        }

        private void BuildContact()
        {
            if (UsesHumanoidAdapter && !contactPose.usesAuthoredEndpoints) { BuildHumanoidContact(); return; }
            ContactBuilds++;
            StoppedJoints = 0; BlockedClosureExcess = 0f; ChestTurn = 0f;
            Array.Clear(blocked, 0, blocked.Length);
            OpenContactDepth = 0f;
            for (int digit = 0; digit < 10; digit++) closure[digit] = contactPose.fingerStart;
            ApplyClosure();
            UpdatePairDepths();
            float startingDepth = OpenContactDepth;
            InitialRootContactDepth = RootContactDepth();
            // All ten digits propose the same step before any collision is queried.
            // Decisions use that shared pose and are rolled back together. No hand wins by order.
            for (int step = 1; step <= 16; step++)
            {
                for (int digit = 0; digit < 10; digit++)
                    if (!blocked[digit]) closure[digit] = Mathf.Lerp(contactPose.fingerStart, 1f, step / 16f);
                ApplyClosure();
                // A rollback can expose a previously safe digit. Propagate
                // simultaneous rejections until stable (at most ten freezes).
                for (int pass = 0; pass < 10; pass++)
                {
                    bool any = false;
                    for (int query = 0; query < 10; query++)
                    {
                        int digit = reverseContactOrder ? 9 - query : query;
                        float excess = ContactDigitExcess(digit, false);
                        reject[digit] = !blocked[digit] && excess > 0f;
                        if (reject[digit])
                        {
                            any = true;
                            BlockedClosureExcess = Mathf.Max(BlockedClosureExcess, excess);
                        }
                    }
                    if (!any) break;
                    for (int digit = 0; digit < 10; digit++)
                    {
                        if (!reject[digit]) continue;
                        blocked[digit] = true;
                        closure[digit] = Mathf.Lerp(contactPose.fingerStart, 1f, (step - 1) / 16f);
                        StoppedJoints += 3;
                    }
                    ApplyClosure();
                }
                UpdatePairDepths();
            }
            OpenContactDepth = startingDepth;
            FinalRootContactDepth = RootContactDepth();
            for (int i = 0; i < 15; i++)
            {
                contactPlayer[i] = playerHand[i + 1].joint.localRotation;
                contactOpponent[i] = opponentHand[i + 1].joint.localRotation;
            }
        }

        private void ApplyClosure()
        {
            for (int i = 0; i < 15; i++)
            {
                playerHand[i + 1].joint.localRotation = Quaternion.Slerp(contactPose.player.openFingerRotations[i],
                    contactPose.player.fingerRotations[i], closure[i / 3]);
                opponentHand[i + 1].joint.localRotation = Quaternion.Slerp(contactPose.opponent.openFingerRotations[i],
                    contactPose.opponent.fingerRotations[i], closure[5 + i / 3]);
            }
            RefreshHand(playerHand); RefreshHand(opponentHand);
        }

        private void UpdatePairDepths()
        {
            for (int digit = 0; digit < 10; digit++) ContactDigitExcess(digit, true);
        }

        private float ContactDigitExcess(int digit, bool remember)
        {
            var hand = digit < 5 ? playerHand : opponentHand;
            var other = digit < 5 ? opponentHand : playerHand;
            int first = 1 + digit % 5 * 3;
            float excess = 0f;
            for (int i = first; i < first + 3; i++)
                for (int j = 0; j < other.Count; j++)
                {
                    ClosureQueries++;
                    Proxy obstacle = j == 0 ? (digit < 5 ? opponentContactPalm : playerContactPalm) : other[j];
                    float depth = Overlap(hand[i], obstacle, out _, out float hitDepth) ? hitDepth : 0f;
                    int pair = digit * 48 + (i - first) * 16 + j;
                    if (remember)
                    {
                        previousPairDepth[pair] = depth;
                        OpenContactDepth = Mathf.Max(OpenContactDepth, depth);
                    }
                    else
                    {
                        // Test each contact separately. A deep pre-existing root
                        // contact must not hide a new distal-joint penetration.
                        float limit = Mathf.Max(0.002f, previousPairDepth[pair] + 0.00002f);
                        excess = Mathf.Max(excess, depth - limit);
                    }
                }
            return excess;
        }
    }
}
