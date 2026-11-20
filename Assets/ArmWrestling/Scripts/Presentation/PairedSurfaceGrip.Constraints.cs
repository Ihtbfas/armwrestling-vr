using System;
using UnityEngine;

namespace ArmWrestlingVR
{
    public static partial class PairedSurfaceGrip
    {
        private static void FitPalmRegions(PairedGripPose pose,Hand a,Hand b,GripContactTargets layout)
        {
            // Hold the authored wrist axes. Translation uses heel + web + central palm,
            // regularized to the comfortable seed; a root-depth guard has priority.
            var ac = new[] { a.Surface(layout.player.heel+a.Offset(0),-1f),a.Surface(layout.player.web+a.Offset(1),-1f),a.Surface(new Vector3(0f,0.5f,0f),-1f) };
            var bc = new[] { b.Surface(layout.opponent.heel+b.Offset(0),-1f),b.Surface(layout.opponent.web+b.Offset(1),-1f),b.Surface(new Vector3(0f,0.5f,0f),-1f) };
            Vector3 delta = Vector3.zero;
            for (int i=0;i<3;i++) delta += bc[i]-ac[i];
            delta = Vector3.ClampMagnitude(delta/4f,Mathf.Min(pose.player.palmLength,pose.opponent.palmLength)*0.15f);
            Vector3 pa=pose.player.wristPosition,pb=pose.opponent.wristPosition;
            float accepted=0f;
            using (var query = new PairedPalmContactFit.RootQuery(pose))
                for (int i=0;i<8;i++)
                {
                    float t=Mathf.Pow(0.5f,i);
                    pose.player.wristPosition=pa+delta*t*0.5f;pose.opponent.wristPosition=pb-delta*t*0.5f;
                    if(query.Depth(pose)<=0.0015f) {accepted=t;break;}
                }
            pose.player.wristPosition=pa;pose.opponent.wristPosition=pb;
            a.Move(delta*accepted*0.5f);b.Move(-delta*accepted*0.5f);
            float squared=0f;
            for(int i=0;i<3;i++) squared+=(ac[i]-bc[i]+delta*accepted).sqrMagnitude;
            pose.palmRegistrationResidual=Mathf.Sqrt(squared/3f);
            Debug.Log($"[P2-D] Limited palm fit: accepted={accepted:F4}, three-region RMS={pose.palmRegistrationResidual*1000f:F2}mm");
        }

        private sealed partial class Hand
        {
            private void AuthorLimited(Hand partner,int digit,Vector3 target,Vector3 outward)
            {
                if(result.jointArcOpen.Length!=15)
                {
                    result.jointArcOpen=(Quaternion[])result.openFingerRotations.Clone();
                    result.jointArcClosed=(Quaternion[])result.fingerRotations.Clone();
                }
                int first=digit*3;
                var distal=bones[first+2];
                Vector3 axis=(distal.position-bones[first+1].position).normalized;
                Vector3 padLocal=distal.InverseTransformPoint(distal.position+axis*distalLength[digit]*0.5f-dorsal*radius[digit]);
                Vector3 normalLocal=distal.InverseTransformDirection(dorsal);
                var amount=new float[3];
                void Apply()
                {
                    for(int j=0;j<3;j++) bones[first+j].localRotation=Quaternion.Slerp(result.jointArcOpen[first+j],result.jointArcClosed[first+j],amount[j]);
                }
                var pair=ScriptableObject.CreateInstance<PairedGripPose>();
                pair.player=result;pair.opponent=partner.result;
                float best=float.PositiveInfinity;
                try
                {
                    using(var query=new PairedPalmContactFit.RootQuery(pair))
                    {
                        float Cost()
                        {
                            float penetration=0f;
                            for(int j=0;j<3;j++)
                            {
                                Vector3 from=bones[first+j].position;
                                Vector3 to=j<2 ? bones[first+j+1].position : distal.TransformPoint(result.proxyDistalOffsets[digit]);
                                float r=Vector3.Distance(bones[first].position,bones[first+1].position)*0.27f*(1f-j*0.12f)*result.collisionRadiusScale;
                                float depth=query.FingerDepth(partner.result,false,j<2 ? Vector3.Lerp(from,to,0.17f) : from,j<2 ? Vector3.Lerp(from,to,0.83f) : to,r);
                                if(depth>0.0025f) return float.PositiveInfinity;
                                penetration+=Mathf.Pow(Mathf.Max(0f,depth-0.002f)/result.palmLength,2f);
                            }
                            float distance=(distal.TransformPoint(padLocal)-target).sqrMagnitude/(result.palmLength*result.palmLength);
                            float alignment=1f-Mathf.Clamp(Vector3.Dot(distal.TransformDirection(normalLocal),outward),-1f,1f);
                            float coupling=(amount[1]-amount[2])*(amount[1]-amount[2]);
                            return distance+40f*penetration+0.025f*alignment+0.03f*coupling+0.004f*(amount[0]*amount[0]+amount[1]*amount[1]+amount[2]*amount[2]);
                        }
                        Apply();best=Cost();
                        // One fixed bounded search on three scalar arcs. No arbitrary quaternion
                        // aiming, no anatomical stretch and no new axial twist DOF.
                        for(int pass=0;pass<3;pass++) for(int j=0;j<3;j++)
                        {
                            float chosen=amount[j];
                            for(int k=0;k<=4;k++)
                            {
                                amount[j]=k*0.25f;Apply();float cost=Cost();
                                if(cost<best) {best=cost;chosen=amount[j];}
                            }
                            amount[j]=chosen;Apply();
                        }
                    }
                    if(float.IsInfinity(best) || float.IsNaN(best))
                        throw new InvalidOperationException("No feasible finger arc pose within the palm collision budget; reject the constrained grip.");
                    result.padLocalPoints[digit]=padLocal;
                    result.padPoints[digit]=distal.TransformPoint(padLocal);
                    Debug.Log($"[P2-D] Limited digit {digit}: arc={amount[0]:F2}/{amount[1]:F2}/{amount[2]:F2}, residual={Vector3.Distance(result.padPoints[digit],target)*1000f:F2}mm");
                }
                finally {if(Application.isPlaying) UnityEngine.Object.Destroy(pair);else UnityEngine.Object.DestroyImmediate(pair);}
            }
        }
    }
}
