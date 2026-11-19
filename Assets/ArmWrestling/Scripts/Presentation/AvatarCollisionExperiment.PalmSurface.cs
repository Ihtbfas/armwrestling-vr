using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArmWrestlingVR
{
    public sealed partial class AvatarCollisionExperiment
    {
        private Proxy playerContactPalm, opponentContactPalm;
        private readonly List<Mesh> contactPalmMeshes = new List<Mesh>();

        private void EnsureContactSurfaces()
        {
            if (playerContactPalm != null) return;
            playerContactPalm = PalmSurface(player.GetComponent<Animator>(), player.Skin);
            SkinnedMeshRenderer body = null;
            var wrist = bodyAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            foreach (var skin in bodyAnimator.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (Array.IndexOf(skin.bones, wrist) >= 0) { body = skin; break; }
            opponentContactPalm = PalmSurface(bodyAnimator, body);
        }

        private Proxy PalmSurface(Animator animator, SkinnedMeshRenderer skin)
        {
            if (skin == null) throw new InvalidOperationException("No palm skin for contact hull.");
            var wrist = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            Vector3 forward = (middle.position - wrist.position).normalized;
            int wristIndex = Array.IndexOf(skin.bones, wrist);
            var bakedPalm = new Mesh(); skin.BakeMesh(bakedPalm);
            Vector3[] vertices = bakedPalm.vertices;
            BoneWeight[] weights = skin.sharedMesh.boneWeights;
            var hullPoints = new List<Vector3>();
            Quaternion inverse = Quaternion.Inverse(wrist.rotation);
            for (int i = 0; i < vertices.Length; i++)
            {
                BoneWeight w = weights[i];
                float weight = (w.boneIndex0 == wristIndex ? w.weight0 : 0f)
                    + (w.boneIndex1 == wristIndex ? w.weight1 : 0f)
                    + (w.boneIndex2 == wristIndex ? w.weight2 : 0f)
                    + (w.boneIndex3 == wristIndex ? w.weight3 : 0f);
                if (weight < 0.55f) continue;
                Vector3 point = skin.transform.TransformPoint(vertices[i]) - wrist.position;
                float along = Vector3.Dot(point, forward);
                if (along < 0.012f || along > 0.085f) continue;
                hullPoints.Add(inverse * point);
            }
            Destroy(bakedPalm);
            if (hullPoints.Count < 8) throw new InvalidOperationException("Palm hull has too few skin points.");
            // PhysX cooks a convex hull from the referenced skin points. The
            // fan includes every point; it is not used as a visual triangle mesh.
            var triangles = new int[(hullPoints.Count - 2) * 3];
            for (int i = 0; i < hullPoints.Count - 2; i++)
            { triangles[3 * i] = 0; triangles[3 * i + 1] = i + 1; triangles[3 * i + 2] = i + 2; }
            var mesh = new Mesh { name = "Contact palm skin hull", vertices = hullPoints.ToArray(), triangles = triangles };
            contactPalmMeshes.Add(mesh);
            var collider = Make<MeshCollider>(mesh.name);
            collider.sharedMesh = mesh; collider.convex = true;
            return new Proxy { shape = collider, sample = () => new Pose(wrist.position, wrist.rotation),
                pose = new Pose(wrist.position, wrist.rotation), joint = wrist, name = mesh.name };
        }

        private void DisposeContactSurfaces()
        {
            foreach (var mesh in contactPalmMeshes) if (mesh != null) Destroy(mesh);
        }
    }
}
