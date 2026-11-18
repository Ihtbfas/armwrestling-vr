using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArmWrestlingVR
{
    public static class ArmMeshCrop
    {
        public static Mesh RightArm(SkinnedMeshRenderer skin, Transform arm, bool keepMaterials = false)
        {
            Transform[] bones = skin.bones;
            if (arm == null || Array.IndexOf(bones, arm) < 0) return null;
            var armBones = new HashSet<int>();
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null && (bones[i] == arm || bones[i].IsChildOf(arm))) armBones.Add(i);
            var source = skin.sharedMesh;
            var keep = new bool[source.vertexCount];
            var weights = source.boneWeights;
            for (int i = 0; i < weights.Length; i++)
            {
                var w = weights[i];
                if (!armBones.Contains(w.boneIndex0)) w.weight0 = 0;
                if (!armBones.Contains(w.boneIndex1)) w.weight1 = 0;
                if (!armBones.Contains(w.boneIndex2)) w.weight2 = 0;
                if (!armBones.Contains(w.boneIndex3)) w.weight3 = 0;
                float sum = w.weight0 + w.weight1 + w.weight2 + w.weight3;
                keep[i] = sum >= 0.55f;
                if (keep[i])
                {
                    w.weight0 /= sum;
                    w.weight1 /= sum;
                    w.weight2 /= sum;
                    w.weight3 /= sum;
                    weights[i] = w;
                }
            }
            int slots = keepMaterials ? source.subMeshCount : 1;
            var mesh = UnityEngine.Object.Instantiate(source);
            mesh.subMeshCount = slots;
            int total = 0;
            for (int slot = 0; slot < slots; slot++)
            {
                var triangles = keepMaterials ? source.GetTriangles(slot) : source.triangles;
                var crop = new List<int>();
                for (int i = 0; i < triangles.Length; i += 3)
                    if (keep[triangles[i]] && keep[triangles[i + 1]] && keep[triangles[i + 2]])
                    {
                        crop.Add(triangles[i]);
                        crop.Add(triangles[i + 1]);
                        crop.Add(triangles[i + 2]);
                    }
                mesh.SetTriangles(crop, slot);
                total += crop.Count;
            }
            if (total == 0)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(mesh);
                else UnityEngine.Object.DestroyImmediate(mesh);
                return null;
            }
            // Keep the original vertex indices, UVs and bind poses.
            mesh.boneWeights = weights;
            return mesh;
        }
    }
}
