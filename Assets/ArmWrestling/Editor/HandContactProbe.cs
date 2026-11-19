using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace ArmWrestlingVR.EditorTools
{
    // Local estimate from posed hand vertices. Nearest samples and normals can
    // miss thin intersections and can give the wrong sign at folds.
    internal sealed class HandContactProbe : IDisposable
    {
        internal const string Header = ",opponent_hand_samples,player_hand_samples,opponent_inside,player_inside,opponent_peak_mm,player_peak_mm,near_opposed_samples,palm_normal_angle_deg,player_wrist_bend_deg,thumb_distal_separation_mm";
        private const int First = (int)HumanBodyBones.RightThumbProximal;
        private readonly Hand opponent;
        private readonly Hand player;
        private readonly Mesh playerBaked = new Mesh();
        private readonly List<Vector3> playerVertices = new List<Vector3>();

        internal HandContactProbe(Animator animator, SkinnedMeshRenderer opponentSkin, SkinnedMeshRenderer playerSkin)
        {
            opponent = new Hand(animator, opponentSkin);
            player = new Hand(playerSkin.GetComponentInParent<Animator>(), playerSkin);
        }

        internal string Record(Mesh opponentBaked, List<Vector3> opponentVertices)
        {
            player.Skin.BakeMesh(playerBaked,true);
            playerBaked.GetVertices(playerVertices);
            opponent.UpdateSurface(opponentBaked, opponentVertices);
            player.UpdateSurface(playerBaked, playerVertices);
            var fromOpponent = Estimate(opponent, player);
            var fromPlayer = Estimate(player, opponent);
            return string.Format(CultureInfo.InvariantCulture,
                ",{0},{1},{2},{3},{4:F3},{5:F3},{6},{7:F3},{8:F3},{9:F3}",
                opponent.Points.Length, player.Points.Length, fromOpponent.inside, fromPlayer.inside,
                fromOpponent.peak * 1000f, fromPlayer.peak * 1000f, fromOpponent.near + fromPlayer.near,
                Vector3.Angle(opponent.PalmNormal, player.PalmNormal), player.WristBend,
                Vector3.Distance(opponent.ThumbDistal, player.ThumbDistal) * 1000f);
        }

        private static (int inside, float peak, int near) Estimate(Hand source, Hand target)
        {
            int inside = 0, near = 0;
            float peak = 0;
            for (int i = 0; i < source.Points.Length; i++)
            {
                int nearest = -1;
                float distanceSq = 0.020f * 0.020f;
                for (int j = 0; j < target.Points.Length; j++)
                {
                    float sq = (source.Points[i] - target.Points[j]).sqrMagnitude;
                    if (sq >= distanceSq) continue;
                    distanceSq = sq;
                    nearest = j;
                }
                if (nearest < 0 || Vector3.Dot(source.Normals[i], target.Normals[nearest]) > -0.2f) continue;
                float depth = Vector3.Dot(target.Points[nearest] - source.Points[i], target.Normals[nearest]);
                if (depth >= 0.0015f)
                {
                    inside++;
                    peak = Mathf.Max(peak, depth);
                }
                if (distanceSq <= 0.004f * 0.004f) near++;
            }
            return (inside, peak, near);
        }

        public void Dispose() => UnityEngine.Object.DestroyImmediate(playerBaked);

        private sealed class Hand
        {
            internal readonly SkinnedMeshRenderer Skin;
            internal readonly Vector3[] Points, Normals;
            private readonly Animator animator;
            private readonly int[] indices;
            private readonly List<Vector3> normals = new List<Vector3>();
            internal Hand(Animator animator, SkinnedMeshRenderer skin)
            {
                this.animator = animator;
                Skin = skin;
                var bones = skin.bones;
                var hand = new HashSet<int> { Array.IndexOf(bones, Bone(HumanBodyBones.RightHand)) };
                for (int i = 0; i < 15; i++)
                {
                    int index = Array.IndexOf(bones, Bone((HumanBodyBones)(First + i)));
                    if (index >= 0) hand.Add(index);
                }
                var weights = skin.sharedMesh.boneWeights;
                // Deduplicate UV seams while keeping the actual surface samples.
                var vertices = skin.sharedMesh.vertices;
                indices = skin.sharedMesh.triangles.Distinct().Where(i =>
                {
                    var w = weights[i];
                    return (hand.Contains(w.boneIndex0) ? w.weight0 : 0)
                         + (hand.Contains(w.boneIndex1) ? w.weight1 : 0)
                         + (hand.Contains(w.boneIndex2) ? w.weight2 : 0)
                         + (hand.Contains(w.boneIndex3) ? w.weight3 : 0) >= 0.75f;
                }).GroupBy(i => vertices[i]).Select(group => group.First()).ToArray();
                Points = new Vector3[indices.Length];
                Normals = new Vector3[indices.Length];
                if (indices.Length == 0) throw new InvalidOperationException("No skinned hand vertices.");
            }

            private Transform Bone(HumanBodyBones bone) => animator.GetBoneTransform(bone);
            private Vector3 Wrist => Bone(HumanBodyBones.RightHand).position;
            private Vector3 Forward => Bone(HumanBodyBones.RightMiddleProximal).position - Wrist;
            internal Vector3 PalmNormal => Vector3.Cross(Bone(HumanBodyBones.RightIndexProximal).position -
                Bone(HumanBodyBones.RightLittleProximal).position, Forward).normalized;
            internal float WristBend => Vector3.Angle(Wrist - Bone(HumanBodyBones.RightLowerArm).position, Forward);
            internal Vector3 ThumbDistal => Bone(HumanBodyBones.RightThumbDistal).position;

            internal void UpdateSurface(Mesh baked, List<Vector3> vertices)
            {
                baked.GetNormals(normals);
                for (int i = 0; i < indices.Length; i++)
                {
                    Points[i] = Skin.transform.TransformPoint(vertices[indices[i]]);
                    Normals[i] = Skin.transform.TransformDirection(normals[indices[i]]).normalized;
                }
            }
        }
    }
}
