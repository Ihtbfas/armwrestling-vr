using System;
using UnityEngine;

namespace ArmWrestlingVR
{
    // Bounded one-time fit of the two rest-palm hulls and thumb roots.
    // Changes relative wrist placement symmetrically; never changes Grip Anchor.
    public static class PairedPalmContactFit
    {
        public static void Fit(PairedGripPose pose, Vector3 normal, float allowance)
        {
            var a = new Shapes(pose.player); var b = new Shapes(pose.opponent);
            Vector3 pa = pose.player.wristPosition, pb = pose.opponent.wristPosition;
            normal.Normalize();
            try
            {
                float Depth(float distance)
                {
                    Vector3 aa = pa - normal*distance*0.5f, bb = pb + normal*distance*0.5f;
                    float max = Hit(a.palm,aa,pose.player.wristRotation,b.palm,bb,pose.opponent.wristRotation);
                    max = Mathf.Max(max,a.ThumbDepth(aa,pose.player.wristRotation,b.palm,bb,pose.opponent.wristRotation));
                    return Mathf.Max(max,b.ThumbDepth(bb,pose.opponent.wristRotation,a.palm,aa,pose.player.wristRotation));
                }
                float initial = Depth(0f), direction = initial > allowance ? -1f : 1f;
                float limit = Mathf.Min(pose.player.palmWidth,pose.opponent.palmWidth)*0.6f;
                float low = 0f,high = 0f;
                bool bracket = false;
                for (int i = 1; i <= 24; i++)
                {
                    high = limit*i/24f;
                    bool reached = direction > 0 ? Depth(high) >= allowance : Depth(-high) <= allowance;
                    if (reached) { bracket = true; break; }
                    low = high;
                }
                if (!bracket) throw new InvalidOperationException("Palm contact is outside the bounded fit. Calibrate the Avatar web offsets.");
                for (int i = 0; i < 12; i++)
                {
                    float mid = (low+high)*0.5f;
                    bool reached = direction > 0 ? Depth(mid) >= allowance : Depth(-mid) <= allowance;
                    if (reached) high = mid; else low = mid;
                }
                float shift = direction*(low+high)*0.5f;
                pose.player.wristPosition = pa-normal*shift*0.5f;
                pose.opponent.wristPosition = pb+normal*shift*0.5f;
                Vector3 aa = pose.player.wristPosition, bb = pose.opponent.wristPosition;
                Debug.Log($"[P2-D] Root components: palm={Hit(a.palm,aa,pose.player.wristRotation,b.palm,bb,pose.opponent.wristRotation)*1000f:F2}mm, player thumb={a.ThumbDepth(aa,pose.player.wristRotation,b.palm,bb,pose.opponent.wristRotation)*1000f:F2}mm, opponent thumb={b.ThumbDepth(bb,pose.opponent.wristRotation,a.palm,aa,pose.player.wristRotation)*1000f:F2}mm");
                Debug.Log($"[P2-D] Root fit: initial={initial*1000f:F2}mm, inward shift={shift*1000f:F2}mm, final={Depth(shift)*1000f:F2}mm");
            }
            finally { a.Dispose(); b.Dispose(); }
        }

        private static float Hit(Collider a,Vector3 pa,Quaternion qa,Collider b,Vector3 pb,Quaternion qb) =>
            Physics.ComputePenetration(a,pa,qa,b,pb,qb,out _,out float depth) ? depth : 0f;

        private sealed class Shapes
        {
            internal readonly MeshCollider palm;
            private readonly CapsuleCollider thumb;
            private readonly GameObject root;
            private readonly Mesh mesh;
            private readonly Vector3 start,end;
            internal Shapes(PairedGripPose.Hand hand)
            {
                root = new GameObject("Humanoid root contact calibration") { hideFlags = HideFlags.HideAndDontSave };
                // Query poses are supplied explicitly. Keep temporary static triggers away from gameplay.
                root.transform.position = Vector3.one*10000f;
                palm = root.AddComponent<MeshCollider>(); palm.convex = true; palm.isTrigger = true;
                int count = hand.palmContactPoints.Length; var triangles = new int[(count-2)*3];
                for (int i = 0; i < count-2; i++) { triangles[3*i]=0;triangles[3*i+1]=i+1;triangles[3*i+2]=i+2; }
                mesh = new Mesh { vertices = hand.palmContactPoints,triangles = triangles };
                palm.sharedMesh = mesh;
                thumb = root.AddComponent<CapsuleCollider>(); thumb.isTrigger = true;
                start = hand.thumbRootStart; end = hand.thumbRootEnd;
                thumb.radius = hand.thumbRootRadius; thumb.height = Vector3.Distance(start,end)+2f*thumb.radius;
            }
            internal float ThumbDepth(Vector3 position,Quaternion rotation,Collider other,Vector3 otherPosition,Quaternion otherRotation) =>
                Hit(thumb,position+rotation*((start+end)*0.5f),rotation*Quaternion.FromToRotation(Vector3.up,end-start),
                    other,otherPosition,otherRotation);
            internal void Dispose()
            {
                if (Application.isPlaying) { UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(mesh); }
                else { UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(mesh); }
            }
        }
    }
}
