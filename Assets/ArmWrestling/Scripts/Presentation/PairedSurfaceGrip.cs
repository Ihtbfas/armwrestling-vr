using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArmWrestlingVR
{
    // Author once against actual skin regions, resolve for the selected two rigs.
    // Only rotations are changed; no bone translations/scales and no live finger IK.
    public static partial class PairedSurfaceGrip
    {
        public static void Resolve(PairedGripPose pose, GripContactTargets targets, GripAvatarProfile player, GripAvatarProfile opponent, Vector3 centre)
        {
            if (targets.jointLimited) PairedPalmContactFit.Fit(pose,pose.player.wristRotation*pose.player.palmDorsal,0.001f);
            using (var a = new Hand(player,pose.player))
            using (var b = new Hand(opponent,pose.opponent))
            {
                // A positive constraint between two authored surface points, not a convex-hull MTD.
                Vector3 ap = a.Surface(targets.player.heel+a.Offset(0),-1f), bp = b.Surface(targets.opponent.heel+b.Offset(0),-1f);
                if (targets.jointLimited) FitPalmRegions(pose,a,b,targets);
                else
                {
                    a.Move(centre-ap+a.dorsal*targets.palmClearance*0.5f);
                    b.Move(centre-bp+b.dorsal*targets.palmClearance*0.5f);
                }
                // Both read the same frozen partner skin before either hand is posed.
                a.Author(b,targets.player,targets.opponent,targets);
                b.Author(a,targets.opponent,targets.player,targets);
                pose.fingerStart = 0f;
                pose.usesSurfaceTargets = true;
                pose.usesJointConstraints = targets.jointLimited;
                Debug.Log($"[P2-D] Surface grip: palm centre separation={Vector3.Distance(a.Centre,b.Centre)*1000f:F2}mm");
            }
        }

        private sealed partial class Hand : IDisposable
        {
            private readonly GameObject model;
            private readonly GripAvatarProfile profile;
            internal Vector3 Offset(int index) => profile.contactOffsets != null && profile.contactOffsets.Length == 7 ? profile.contactOffsets[index] : Vector3.zero;
            private readonly PairedGripPose.Hand result;
            private readonly Transform[] bones;
            private Vector3[] skin;
            private readonly int[] triangles;
            private readonly float[] distalLength = new float[5], radius = new float[5];
            internal readonly Transform wrist;
            internal readonly Vector3 forward, dorsal, across;
            internal Vector3 Centre => Vector3.Lerp(wrist.position,bones[6].position,0.55f);

            internal Hand(GripAvatarProfile profile, PairedGripPose.Hand hand)
            {
                result = hand; this.profile = profile;
                model = UnityEngine.Object.Instantiate(profile.prefab);
                try
                {
                    model.hideFlags = HideFlags.HideAndDontSave;
                    model.transform.localScale = Vector3.one*profile.modelScale;
                    var animator = model.GetComponent<Animator>(); animator.enabled = false;
                    foreach (var r in model.GetComponentsInChildren<Renderer>()) r.enabled = false;
                    wrist = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    bones = PairedGripPose.MapFingers(animator);
                    result.proxyDistalOffsets = new Vector3[5];
                    for(int d=0;d<5;d++) result.proxyDistalOffsets[d] = bones[d*3+2].InverseTransformVector(bones[d*3+2].position-bones[d*3+1].position)*0.58f;
                    model.transform.rotation = hand.wristRotation*Quaternion.Inverse(wrist.rotation)*model.transform.rotation;
                    model.transform.position += hand.wristPosition-wrist.position;
                    for (int i = 0; i < 15; i++) bones[i].localRotation = hand.openFingerRotations[i];
                    dorsal = wrist.rotation*hand.palmDorsal;
                    forward = Vector3.ProjectOnPlane(bones[6].position-wrist.position,dorsal).normalized;
                    across = Vector3.Cross(forward,dorsal).normalized;
                    var vertices = new List<Vector3>(); var indices = new List<int>();
                    foreach (var mesh in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        var handBones = HandSurfaceGeometry.BoneIndices(animator,mesh);
                        if (handBones.Count < 5) continue;
                        var points = HandSurfaceGeometry.WorldVertices(mesh);
                        var weights = mesh.sharedMesh.boneWeights;
                        int offset = vertices.Count; vertices.AddRange(points);
                        var tris = mesh.sharedMesh.triangles;
                        for (int i = 0; i < tris.Length; i+=3)
                            if (HandSurfaceGeometry.Weight(weights[tris[i]],handBones) >= 0.5f
                                && HandSurfaceGeometry.Weight(weights[tris[i+1]],handBones) >= 0.5f
                                && HandSurfaceGeometry.Weight(weights[tris[i+2]],handBones) >= 0.5f)
                            { indices.Add(offset+tris[i]); indices.Add(offset+tris[i+1]); indices.Add(offset+tris[i+2]); }
                        for (int d = 0; d < 5; d++)
                        {
                            int bone = Array.IndexOf(mesh.bones,bones[d*3+2]);
                            var digit = new HashSet<int> { bone };
                            Vector3 axis = (bones[d*3+2].position-bones[d*3+1].position).normalized;
                            for (int i = 0; i < points.Length; i++)
                                if (bone >= 0 && HandSurfaceGeometry.Weight(weights[i],digit) > 0.6f)
                                {
                                    Vector3 p = points[i]-bones[d*3+2].position;
                                    distalLength[d] = Mathf.Max(distalLength[d],Vector3.Dot(p,axis));
                                    radius[d] = Mathf.Max(radius[d],-Vector3.Dot(p,dorsal));
                                }
                        }
                    }
                    skin = vertices.ToArray(); triangles = indices.ToArray();
                    if (triangles.Length == 0) throw new InvalidOperationException("No weighted hand surface triangles.");
                    for (int d = 0; d < 5; d++)
                    {
                        float middle = Vector3.Distance(bones[d*3+1].position,bones[d*3+2].position);
                        distalLength[d] = Mathf.Clamp(distalLength[d],middle*0.4f,middle*1.1f);
                        radius[d] = Mathf.Clamp(radius[d],middle*0.15f,middle*0.4f);
                    }
                }
                catch { Dispose(); throw; }
            }

            internal void Move(Vector3 offset)
            {
                model.transform.position += offset;
                for (int i = 0; i < skin.Length; i++) skin[i] += offset;
                result.wristPosition = wrist.position;
            }

            internal Vector3 Surface(Vector3 region,float side)
            {
                Vector3 origin = wrist.position+across*(region.x*result.palmWidth)+forward*(region.y*result.palmLength)+dorsal*(side*result.palmLength*1.5f);
                Vector3 direction = -side*dorsal;
                float nearest = float.PositiveInfinity;
                for (int i = 0; i < triangles.Length; i+=3)
                {
                    Vector3 a = skin[triangles[i]], e1 = skin[triangles[i+1]]-a, e2 = skin[triangles[i+2]]-a;
                    Vector3 p = Vector3.Cross(direction,e2); float det = Vector3.Dot(e1,p);
                    if (Mathf.Abs(det)<1e-9f) continue;
                    Vector3 t = origin-a; float u = Vector3.Dot(t,p)/det;
                    Vector3 q = Vector3.Cross(t,e1); float v = Vector3.Dot(direction,q)/det;
                    float distance = Vector3.Dot(e2,q)/det;
                    if (u>=0f && v>=0f && u+v<=1f && distance>=0f) nearest = Mathf.Min(nearest,distance);
                }
                if (float.IsInfinity(nearest)) throw new InvalidOperationException($"Contact marker misses hand surface: {region}");
                return origin+direction*nearest+dorsal*(side*region.z*result.palmLength);
            }

            internal void Author(Hand partner, GripContactTargets.Hand marks, GripContactTargets.Hand partnerMarks, GripContactTargets layout)
            {
                result.contactPoints = new Vector3[7]; result.padPoints = new Vector3[5]; result.padLocalPoints = new Vector3[5]; result.contactNormals = new Vector3[5];
                result.contactPoints[0] = Surface(marks.heel+Offset(0),-1f);
                result.contactPoints[1] = Surface(marks.web+Offset(1),-1f);
                for (int digit = 0; digit < 5; digit++)
                {
                    Vector3 region = marks.pads[digit]+Offset(digit+2);
                    if (digit == 0) { region.x += partnerMarks.web.x+partner.Offset(1).x; region.y += partnerMarks.web.y+partner.Offset(1).y; }
                    if (digit > 0)
                    {
                        Vector3 fromPartner = bones[digit*3].position-partner.wrist.position;
                        region.x = Mathf.Clamp(Vector3.Dot(fromPartner,partner.across)/partner.result.palmWidth+region.x,-0.42f,0.42f);
                        region.y = Mathf.Clamp(Vector3.Dot(fromPartner,partner.forward)/partner.result.palmLength+region.y,0.45f,0.98f);
                    }
                    float surfaceSide = digit == 0 ? -1f : 1f;
                    Vector3 outward = partner.dorsal*surfaceSide;
                    Vector3 target = partner.Surface(region,surfaceSide);
                    result.contactPoints[2+digit] = target; result.contactNormals[digit] = outward;
                    var a = bones[digit*3]; var b = bones[digit*3+1]; var c = bones[digit*3+2];
                    if (layout.jointLimited) { AuthorLimited(partner,digit,target,outward); continue; }
                    Vector3 last = (c.position-b.position).normalized;
                    Quaternion oldFrame = Quaternion.LookRotation(last,Vector3.ProjectOnPlane(dorsal,last));
                    Quaternion restRotation = c.rotation;
                    Vector3 tipDirection = digit == 0 ? layout.thumbDirection.normalized : -partner.forward;
                    float padAlong = distalLength[digit]*0.5f;
                    Vector3 landing = target+outward*radius[digit]-tipDirection*padAlong;
                    Vector3 pole = a.position+forward*(result.palmLength*layout.knuckleLift)
                        +(digit == 0 ? across : dorsal)*result.palmLength*0.15f;
                    Debug.Log($"[P2-D] Digit {digit}: segments={Vector3.Distance(a.position,b.position)*1000f:F1}/{Vector3.Distance(b.position,c.position)*1000f:F1}mm, requested={Vector3.Distance(a.position,landing)*1000f:F1}mm, pad length/radius={padAlong*1000f:F1}/{radius[digit]*1000f:F1}mm");
                    ArmChain.SolveJoints(a.position,landing,pole,dorsal,
                        Vector3.Distance(a.position,b.position),Vector3.Distance(b.position,c.position),out Vector3 bend,out Vector3 end);
                    a.rotation = Quaternion.FromToRotation(b.position-a.position,bend-a.position)*a.rotation;
                    b.rotation = Quaternion.FromToRotation(c.position-b.position,end-b.position)*b.rotation;
                    c.rotation = Quaternion.LookRotation(tipDirection,outward)*Quaternion.Inverse(oldFrame)*restRotation;
                    result.padPoints[digit] = c.position+tipDirection*padAlong-outward*radius[digit];
                    result.padLocalPoints[digit] = c.InverseTransformPoint(result.padPoints[digit]);
                    Debug.Log($"[P2-D] {(digit == 0 ? "Thumb" : "Finger"+digit)} pad marker residual={Vector3.Distance(result.padPoints[digit],target)*1000f:F2}mm");
                }
                for (int i = 0; i < 15; i++)
                {
                    result.fingerRotations[i] = bones[i].localRotation;
                    result.openFingerRotations[i] = Quaternion.Slerp(result.fingerRotations[i],result.openFingerRotations[i],layout.release);
                }
            }

            public void Dispose()
            {
                if (model == null) return;
                model.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(model); else UnityEngine.Object.DestroyImmediate(model);
            }
        }
    }
}
