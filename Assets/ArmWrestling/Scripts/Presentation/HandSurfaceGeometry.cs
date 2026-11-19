using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArmWrestlingVR
{
    public static class HandSurfaceGeometry
    {
        public static HashSet<int> BoneIndices(Animator animator, SkinnedMeshRenderer skin)
        {
            var indices = new HashSet<int>();
            indices.Add(Array.IndexOf(skin.bones,animator.GetBoneTransform(HumanBodyBones.RightHand)));
            for (int i = 0; i < 15; i++) indices.Add(Array.IndexOf(skin.bones,animator.GetBoneTransform(PairedGripPose.Finger(i))));
            indices.Remove(-1); return indices;
        }
        public static float Weight(BoneWeight w, HashSet<int> indices) =>
            (indices.Contains(w.boneIndex0) ? w.weight0 : 0f) + (indices.Contains(w.boneIndex1) ? w.weight1 : 0f)
            + (indices.Contains(w.boneIndex2) ? w.weight2 : 0f) + (indices.Contains(w.boneIndex3) ? w.weight3 : 0f);

        // Direct LBS avoids renderer-update timing when calibrating a hidden Avatar at startup.
        public static Vector3[] WorldVertices(SkinnedMeshRenderer skin)
        {
            var mesh = skin.sharedMesh; var vertices = mesh.vertices; var weights = mesh.boneWeights;
            var matrices = new Matrix4x4[skin.bones.Length]; var bind = mesh.bindposes;
            for (int i = 0; i < matrices.Length; i++) matrices[i] = skin.bones[i].localToWorldMatrix * bind[i];
            for (int i = 0; i < vertices.Length; i++)
            {
                var w = weights[i]; Vector3 v = vertices[i];
                vertices[i] = matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0 + matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1
                    + matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2 + matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
            }
            return vertices;
        }

        public static Vector3[] WorldNormals(SkinnedMeshRenderer skin)
        {
            var mesh = skin.sharedMesh; var normals = mesh.normals; var weights = mesh.boneWeights;
            var matrices = new Matrix4x4[skin.bones.Length]; var bind = mesh.bindposes;
            for (int i = 0; i < matrices.Length; i++) matrices[i] = (skin.bones[i].localToWorldMatrix * bind[i]).inverse.transpose;
            for (int i = 0; i < normals.Length; i++)
            {
                var w = weights[i]; Vector3 n = normals[i];
                normals[i] = (matrices[w.boneIndex0].MultiplyVector(n)*w.weight0 + matrices[w.boneIndex1].MultiplyVector(n)*w.weight1
                    + matrices[w.boneIndex2].MultiplyVector(n)*w.weight2 + matrices[w.boneIndex3].MultiplyVector(n)*w.weight3).normalized;
            }
            return normals;
        }

        public static Vector3 PalmNormal(Animator animator, Vector3 forward, Vector3 boneNormal, float length)
        {
            var wrist = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Vector3 across = Vector3.Cross(forward,boneNormal).normalized;
            float width = Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.RightIndexProximal).position,
                animator.GetBoneTransform(HumanBodyBones.RightLittleProximal).position);
            Vector3 sum = Vector3.zero;
            foreach (var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var indices = BoneIndices(animator,skin); if (indices.Count < 5) continue;
                var points = WorldVertices(skin); var normals = WorldNormals(skin); var weights = skin.sharedMesh.boneWeights;
                for (int i = 0; i < points.Length; i++)
                {
                    if (Weight(weights[i],indices) < 0.75f) continue;
                    Vector3 p = points[i]-wrist.position;
                    float along = Vector3.Dot(p,forward);
                    if (along < length*0.25f || along > length*0.7f || Mathf.Abs(Vector3.Dot(p,across)) > width*0.5f) continue;
                    float dot = Vector3.Dot(normals[i],boneNormal);
                    if (Mathf.Abs(dot) < 0.5f) continue;
                    sum += normals[i]*Mathf.Sign(dot)*dot*dot;
                }
            }
            return sum.sqrMagnitude > 0.001f ? sum.normalized : boneNormal;
        }
    }
}
