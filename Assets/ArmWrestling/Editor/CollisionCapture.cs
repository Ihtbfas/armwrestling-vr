using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ArmWrestlingVR.EditorTools
{
    internal sealed class CollisionCapture : IDisposable
    {
        internal static readonly string Header = ",hand_proxy_pairs,hand_proxy_max_mm,torso_proxy_pairs,torso_proxy_max_mm,pad_proxy_pairs,pad_proxy_max_mm,digit_adjustment_deg,arm_swivel_deg,stopped_joints,chest_turn_deg,blocked_closure_excess_mm,contact_builds,closure_queries,contact_solve_ms,contact_build_ms,contact_build_queries,open_contact_depth_mm,contact_init_peak_ms,evaluation_ms,initial_root_contact_mm,final_root_contact_mm" + string.Concat(Enumerable.Range(0,20).Select(i => ","+(i<10 ? "pad_marker_error_" : "pad_skin_vertex_gap_")+(i%10<5 ? "player_" : "opponent_")+(i%5)+"_mm")) + ",joint_arc_excess_deg,palm_registration_mm";
        private static readonly CultureInfo Format = CultureInfo.InvariantCulture;
        private readonly AvatarCollisionExperiment experiment;
        private readonly StreamWriter details;
        private readonly Transform device;
        private readonly GripTargetSamples targetSamples;

        internal CollisionCapture(AvatarCollisionExperiment experiment, string folder, Transform device, VirtualArmRig rig, Animator opponent)
        {
            this.experiment = experiment != null && experiment.ProxyCount > 0
                ? experiment : throw new InvalidOperationException("The P2-B collision proxies did not start.");
            this.device = device;
            targetSamples = new GripTargetSamples(rig,opponent,experiment);
            details = new StreamWriter(Path.Combine(folder, "collision-pairs.csv"));
            details.WriteLine("frame,angle,region,first,second,depth_mm,dir_x,dir_y,dir_z,origin_x,origin_y,origin_z");
        }

        internal string Record(int frame, float angle)
        {
            foreach (var hit in experiment.Contacts)
            {
                Vector3 direction = device.InverseTransformDirection(hit.direction);
                Vector3 origin = device.InverseTransformPoint(hit.position);
                details.WriteLine(string.Format(Format, "{0},{1:F4},{2},{3},{4},{5:F4},{6:F4},{7:F4},{8:F4},{9:F4},{10:F4},{11:F4}",
                    frame, angle, hit.region, hit.first, hit.second, 1000f * hit.depth, direction.x, direction.y,
                    direction.z, origin.x, origin.y, origin.z));
            }

            string Columns(AvatarCollisionExperiment.Region region)
            {
                var hits = experiment.Contacts.Where(hit => hit.region == region && hit.depth > 0.002f).ToArray();
                return string.Format(Format, ",{0},{1:F4}", hits.Length, hits.Select(hit => 1000f * hit.depth).DefaultIfEmpty().Max());
            }
            return Columns(AvatarCollisionExperiment.Region.Hands) + Columns(AvatarCollisionExperiment.Region.Torso)
                + Columns(AvatarCollisionExperiment.Region.Pads)
                + string.Format(Format, ",{0:F4},{1:F4},{2},{3:F4},{4:F4},{5},{6},{7:F4},{8:F4},{9},{10:F4},{11:F4},{12:F4},{13:F4},{14:F4}", experiment.MaxFingerCorrection, experiment.ArmCorrection, experiment.StoppedJoints, experiment.ChestTurn, experiment.BlockedClosureExcess * 1000f, experiment.ContactBuilds, experiment.ClosureQueries, experiment.ContactSolveMilliseconds, experiment.ContactBuildMilliseconds, experiment.ContactBuildQueries, experiment.OpenContactDepth * 1000f, experiment.MaxContactSolveMilliseconds, experiment.EvaluationMilliseconds, experiment.InitialRootContactDepth*1000f, experiment.FinalRootContactDepth*1000f) + targetSamples.Record();
        }

        internal void DrawOverlay(Camera camera, Texture2D image, AvatarCollisionExperiment.Region region)
        {
            // The dot is the moving proxy's centre. The arrow points along the
            // MTD, drawn larger than scale to keep it visible at wide framing.
            foreach (var hit in experiment.Contacts.Where(h => h.region == region && h.depth > 0.002f))
            {
                Vector3 point = camera.WorldToScreenPoint(hit.position);
                Vector3 end = camera.WorldToScreenPoint(hit.position + hit.direction * (hit.depth * 2f + 0.018f));
                if (point.z <= 0 || end.z <= 0) continue;
                Vector2 tangent = new Vector2(end.x - point.x, end.y - point.y).normalized;
                Vector2 side = new Vector2(-tangent.y, tangent.x);
                Line(image, point, end, Color.yellow);
                Line(image, end, (Vector2)end - 8f * tangent + 4f * side, Color.yellow);
                Line(image, end, (Vector2)end - 8f * tangent - 4f * side, Color.yellow);
                for (int x = -3; x <= 3; x++)
                    for (int y = -3; y <= 3; y++)
                        Pixel(image, Mathf.RoundToInt(point.x) + x, Mathf.RoundToInt(point.y) + y, Color.red);
            }
        }

        private static void Line(Texture2D image, Vector2 start, Vector2 end, Color color)
        {
            int steps = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(start, end)), 1, 2000);
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(start, end, (float)i / steps);
                Pixel(image, Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), color);
            }
        }

        private static void Pixel(Texture2D image, int x, int y, Color color)
        {
            if (x >= 0 && y >= 0 && x < image.width && y < image.height)
                image.SetPixel(x, y, color);
        }

        public void Dispose() => details.Dispose();
    }
}
