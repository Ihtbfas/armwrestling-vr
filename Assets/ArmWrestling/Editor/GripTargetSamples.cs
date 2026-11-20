using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace ArmWrestlingVR.EditorTools
{
    // Unsigned distance to actual distal-pad SKIN VERTICES: coarse upper bound,
    // not triangle clearance, penetration depth, pressure or the analytical pad marker.
    internal sealed class GripTargetSamples
    {
        private readonly VirtualArmRig rig;
        private readonly AvatarCollisionExperiment experiment;
        private readonly SkinnedMeshRenderer[] skins;
        private readonly Transform[][] fingers;
        private readonly int[][][] indices;
        internal GripTargetSamples(VirtualArmRig rig, Animator opponent, AvatarCollisionExperiment experiment)
        {
            this.rig = rig; this.experiment = experiment;
            var opponentHand = opponent.GetBoneTransform(HumanBodyBones.RightHand);
            skins = new[] { rig.playerVisual.Skin,opponent.GetComponentsInChildren<SkinnedMeshRenderer>().First(s => Array.IndexOf(s.bones,opponentHand)>=0) };
            fingers = new[] { PairedGripPose.MapFingers(skins[0].GetComponentInParent<Animator>()),PairedGripPose.MapFingers(opponent) };
            indices = new int[2][][];
            for (int s = 0; s < 2; s++)
            {
                indices[s] = new int[5][];
                var weights = skins[s].sharedMesh.boneWeights;
                for (int d = 0; d < 5; d++)
                {
                    int bone = Array.IndexOf(skins[s].bones,fingers[s][d*3+2]);
                    var set = new HashSet<int> { bone };
                    indices[s][d] = Enumerable.Range(0,weights.Length).Where(i => bone>=0 && HandSurfaceGeometry.Weight(weights[i],set)>0.6f).ToArray();
                }
            }
        }

        internal string Record()
        {
            var pose = experiment.ResolvedContactPose;
            if (pose == null || !pose.usesSurfaceTargets) return new string(',',22);
            var values = Enumerable.Repeat(float.NaN,20).ToArray();
            for (int s = 0; s < 2; s++)
            {
                var hand = s == 0 ? pose.player : pose.opponent;
                var vertices = HandSurfaceGeometry.WorldVertices(skins[s]);
                for (int d = 0; d < 5; d++)
                {
                    Vector3 target = rig.gripAnchor.TransformPoint(hand.contactPoints[d+2]);
                    Vector3 normal = rig.gripAnchor.TransformDirection(hand.contactNormals[d]);
                    var distal = fingers[s][d*3+2];
                    values[s*5+d] = 1000f*Vector3.Distance(distal.TransformPoint(hand.padLocalPoints[d]),target);
                    float distance = float.PositiveInfinity;
                    foreach (int i in indices[s][d])
                        if (Vector3.Dot(vertices[i]-distal.position,normal)<0f)
                            distance = Mathf.Min(distance,Vector3.Distance(vertices[i],target));
                    if (!float.IsInfinity(distance)) values[10+s*5+d] = distance*1000f;
                }
            }
            float arcExcess=0f;
            if(pose.usesJointConstraints)
                for(int s=0;s<2;s++) for(int j=0;j<15;j++)
                {
                    var h=s==0 ? pose.player : pose.opponent;
                    float length=Quaternion.Angle(h.jointArcOpen[j],h.jointArcClosed[j]);
                    float from=Quaternion.Angle(h.jointArcOpen[j],fingers[s][j].localRotation);
                    float to=Quaternion.Angle(h.jointArcClosed[j],fingers[s][j].localRotation);
                    arcExcess=Mathf.Max(arcExcess,from+to-length,from-length,to-length);
                }
            return ","+string.Join(",",values.Select(v => v.ToString("F4",CultureInfo.InvariantCulture)))
                +(pose.usesJointConstraints ? string.Format(CultureInfo.InvariantCulture,",{0:F4},{1:F4}",arcExcess,pose.palmRegistrationResidual*1000f) : ",,");
        }
    }
}
