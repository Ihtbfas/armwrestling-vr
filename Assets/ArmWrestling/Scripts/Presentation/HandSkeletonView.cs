using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace ArmWrestlingVR
{
    /// <summary>
    /// 把追踪到的手画出来：每个关节一个小球，指节之间用细杆连起来。
    /// 用途是让人一眼确认"手部追踪真的在工作"——没有它，玩家只会看到虚拟手臂在动，
    /// 却不知道自己的手有没有被识别到。
    /// </summary>
    public class HandSkeletonView : MonoBehaviour
    {
        private const float JointRadius = 0.009f;
        private const float BoneRadius = 0.005f;

        // 每人一只手要画的关节；按 XRHandJointID 顺序排列，方便和骨骼连线。
        private static readonly XRHandJointID[] Joints =
        {
            XRHandJointID.Wrist,
            XRHandJointID.Palm,
            XRHandJointID.ThumbMetacarpal, XRHandJointID.ThumbProximal, XRHandJointID.ThumbDistal, XRHandJointID.ThumbTip,
            XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip,
            XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip,
            XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip,
            XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip
        };

        // 连线：关节 -> 它的父关节（画成一根细杆）。
        private static readonly int[] ParentIndex =
        {
            0, 0, 1, 2, 3, 4, 1, 6, 7, 8, 1, 10, 11, 12, 1, 14, 15, 16, 1, 18, 19, 20
        };

        private readonly Dictionary<XRHandJointID, Transform> jointViews = new Dictionary<XRHandJointID, Transform>();
        private readonly Dictionary<int, Transform> boneViews = new Dictionary<int, Transform>();

        private XRHandSubsystem subsystem;
        private Material jointMaterial;
        private Material boneMaterial;
        private bool visible;

        public void SetVisible(bool value)
        {
            visible = value;
            if (!value)
            {
                HideAll();
            }
        }

        private void Awake()
        {
            jointMaterial = ArenaFactory.GetEmissiveMaterial(new Color(0.45f, 0.85f, 0.95f), 1.4f);
            boneMaterial = ArenaFactory.GetMaterial(new Color(0.25f, 0.55f, 0.65f));
        }

        private void Update()
        {
            if (!visible)
            {
                return;
            }

            if (subsystem == null)
            {
                var found = new List<XRHandSubsystem>();
                SubsystemManager.GetSubsystems(found);
                if (found.Count == 0)
                {
                    return;
                }

                subsystem = found[0];
            }

            XRHand hand = subsystem.rightHand;
            if (!hand.isTracked)
            {
                HideAll();
                return;
            }

            // 手柄追踪空间就是父物体所在空间，直接放世界坐标即可。
            for (int i = 0; i < Joints.Length; i++)
            {
                XRHandJointID id = Joints[i];
                XRHandJoint joint = hand.GetJoint(id);
                Pose pose;
                if (!joint.TryGetPose(out pose))
                {
                    continue;
                }

                Transform view = GetOrCreate(id);
                view.gameObject.SetActive(true);
                view.SetPositionAndRotation(pose.position, pose.rotation);
            }

            for (int i = 0; i < Joints.Length; i++)
            {
                int parentIndex = ParentIndex[i];
                if (parentIndex < 0 || !jointViews.ContainsKey(Joints[i]) || !jointViews.ContainsKey(Joints[parentIndex]))
                {
                    continue;
                }

                Vector3 from = jointViews[Joints[parentIndex]].position;
                Vector3 to = jointViews[Joints[i]].position;
                Vector3 delta = to - from;
                float length = delta.magnitude;
                if (length < 0.0005f)
                {
                    continue;
                }

                Transform bone = GetOrCreateBone(i);
                bone.gameObject.SetActive(true);
                bone.position = from + delta * 0.5f;
                bone.rotation = Quaternion.FromToRotation(Vector3.up, delta);
                bone.localScale = new Vector3(BoneRadius * 2f, length * 0.5f, BoneRadius * 2f);
            }
        }

        private Transform GetOrCreate(XRHandJointID id)
        {
            Transform view;
            if (jointViews.TryGetValue(id, out view) && view != null)
            {
                return view;
            }

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Joint " + id;
            sphere.transform.SetParent(transform, false);
            sphere.transform.localScale = Vector3.one * JointRadius * 2f;
            Object.Destroy(sphere.GetComponent<Collider>());

            Renderer renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = jointMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            view = sphere.transform;
            jointViews[id] = view;

            return view;
        }

        private Transform GetOrCreateBone(int index)
        {
            Transform bone;
            if (boneViews.TryGetValue(index, out bone) && bone != null)
            {
                return bone;
            }

            GameObject created = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            created.name = "Bone " + index;
            created.transform.SetParent(transform, false);
            Object.Destroy(created.GetComponent<Collider>());

            Renderer renderer = created.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = boneMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            boneViews[index] = created.transform;
            return created.transform;
        }

        private void HideAll()
        {
            foreach (var pair in jointViews)
            {
                if (pair.Value != null)
                {
                    pair.Value.gameObject.SetActive(false);
                }
            }

            foreach (var pair in boneViews)
            {
                if (pair.Value != null)
                {
                    pair.Value.gameObject.SetActive(false);
                }
            }
        }
    }
}

