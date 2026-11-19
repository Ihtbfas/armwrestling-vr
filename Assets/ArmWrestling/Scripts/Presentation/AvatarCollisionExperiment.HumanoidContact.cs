using System;
using UnityEngine;

namespace ArmWrestlingVR
{
    public sealed partial class AvatarCollisionExperiment
    {
        // A proximal contact must not freeze the two free joints of that digit.
        // Both hands propose the same joint level together; no hand has priority.
        private void BuildHumanoidContact()
        {
            ContactBuilds++;
            StoppedJoints = 0; BlockedClosureExcess = 0f; ChestTurn = 0f;
            var amounts = new float[30];
            var stopped = new bool[30];
            for (int i = 0; i < 30; i++) amounts[i] = contactPose.fingerStart;
            void Apply()
            {
                for (int i = 0; i < 15; i++)
                {
                    playerHand[i+1].joint.localRotation = Quaternion.Slerp(contactPose.player.openFingerRotations[i],contactPose.player.fingerRotations[i],amounts[i]);
                    opponentHand[i+1].joint.localRotation = Quaternion.Slerp(contactPose.opponent.openFingerRotations[i],contactPose.opponent.fingerRotations[i],amounts[15+i]);
                }
                RefreshHand(playerHand); RefreshHand(opponentHand);
            }
            Apply();
            OpenContactDepth = 0f; UpdatePairDepths();
            float startingDepth = OpenContactDepth;
            InitialRootContactDepth = RootContactDepth();
            // Once distal joints curl, a previously blocked MCP may now advance.
            // Three bounded passes replace a permanent first-contact freeze.
            for (int round = 0; round < 3; round++)
            for (int joint = 0; joint < 3; joint++)
            {
                Array.Clear(stopped,0,stopped.Length);
                var stageStart = (float[])amounts.Clone();
                for (int step = 1; step <= 16; step++)
                {
                    for (int digit = 0; digit < 10; digit++)
                        if (!stopped[digit*3+joint]) amounts[digit*3+joint] = Mathf.Lerp(stageStart[digit*3+joint],1f,step/16f);
                    Apply();
                    for (int pass = 0; pass < 10; pass++)
                    {
                        bool any = false;
                        for (int query = 0; query < 10; query++)
                        {
                            int digit = reverseContactOrder ? 9-query : query;
                            float excess = ContactDigitExcess(digit,false);
                            reject[digit] = !stopped[digit*3+joint] && excess > 0f;
                            if (reject[digit]) { any = true; BlockedClosureExcess = Mathf.Max(BlockedClosureExcess,excess); }
                        }
                        if (!any) break;
                        for (int digit = 0; digit < 10; digit++)
                            if (reject[digit])
                            {
                                stopped[digit*3+joint] = true;
                                amounts[digit*3+joint] = Mathf.Lerp(stageStart[digit*3+joint],1f,(step-1)/16f);

                            }
                        Apply();
                    }
                    UpdatePairDepths();
                }
            }
            for (int i = 0; i < 30; i++) if (amounts[i] < 0.999f) StoppedJoints++;
            OpenContactDepth = startingDepth;
            FinalRootContactDepth = RootContactDepth();
            for (int i = 0; i < 15; i++)
            {
                contactPlayer[i] = playerHand[i+1].joint.localRotation;
                contactOpponent[i] = opponentHand[i+1].joint.localRotation;
            }
        }
    }
}
