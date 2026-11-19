using System;
using UnityEditor;
using UnityEngine;

namespace ArmWrestlingVR.EditorTools
{
    [CustomEditor(typeof(PairedContactPreview))]
    public sealed class PairedContactPreviewInspector : UnityEditor.Editor
    {
        private int side, marker;
        private static readonly string[] Labels = { "Heel", "Web", "Thumb latch", "Index pad", "Middle pad", "Ring pad", "Little pad" };
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var view = (PairedContactPreview)target;
            side = EditorGUILayout.Popup("Edit hand",side,new[] { "Player", "Opponent" });
            marker = EditorGUILayout.Popup("Contact marker",marker,Labels);
            if (GUILayout.Button("Rebuild both hands from markers")) view.Rebuild();
            if (GUILayout.Button("Apply open / closed preview")) view.ApplyClosure();
            if (GUILayout.Button("Save shared contact targets")) AssetDatabase.SaveAssets();
            EditorGUILayout.HelpBox("Drag the selected marker in Scene view, then Rebuild. Green targets lie on partner skin; yellow lines show pad marker residuals. Changing profiles tests the same asset on another rig. The preview is not saved into the main scene.",MessageType.Info);
        }

        private void OnSceneGUI()
        {
            var view = (PairedContactPreview)target;
            if (view.pose == null || view.definition.contactTargets == null) return;
            var hand = side == 0 ? view.pose.player : view.pose.opponent;
            var layout = view.definition.contactTargets;
            var marks = side == 0 ? layout.player : layout.opponent;
            for (int s = 0; s < 2; s++)
            {
                var data = s == 0 ? view.pose.player : view.pose.opponent;
                for (int i = 0; i < data.contactPoints.Length; i++)
                {
                    Vector3 p = view.transform.TransformPoint(data.contactPoints[i]);
                    Handles.color = s == 0 ? Color.cyan : Color.green;
                    Handles.SphereHandleCap(0,p,Quaternion.identity,0.004f,EventType.Repaint);
                    Handles.Label(p,$"{(s==0 ? "P" : "O")} {Labels[i]}");
                    if (i >= 2)
                    {
                        Handles.color = Color.yellow;
                        Handles.DrawLine(p,view.transform.TransformPoint(data.padPoints[i-2]));
                    }
                }
            }
            if (hand.contactPoints.Length != 7) return;
            Vector3 point = view.transform.TransformPoint(hand.contactPoints[marker]);
            EditorGUI.BeginChangeCheck();
            Vector3 next = Handles.PositionHandle(point,Quaternion.identity);
            if (!EditorGUI.EndChangeCheck()) return;
            var frameHand = marker < 2 ? hand : side == 0 ? view.pose.opponent : view.pose.player;
            var frameAnimator = view.hands[marker < 2 ? side : 1-side];
            var wrist = frameAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            Vector3 normal = wrist.rotation*frameHand.palmDorsal;
            Vector3 forward = Vector3.ProjectOnPlane(frameAnimator.GetBoneTransform(HumanBodyBones.RightMiddleProximal).position-wrist.position,normal).normalized;
            Vector3 across = Vector3.Cross(forward,normal).normalized;
            Vector3 delta = next-point;
            Vector3 normalized = new Vector3(Vector3.Dot(delta,across)/frameHand.palmWidth,
                Vector3.Dot(delta,forward)/frameHand.palmLength,Vector3.Dot(delta,normal)*(marker<3 ? -1f : 1f)/frameHand.palmLength);
            Undo.RecordObject(layout,"Move paired contact marker");
            if (marker == 0) marks.heel += normalized;
            else if (marker == 1) marks.web += normalized;
            else marks.pads[marker-2] += normalized;
            hand.contactPoints[marker] = view.transform.InverseTransformPoint(next);
            EditorUtility.SetDirty(layout);
        }
    }
}
