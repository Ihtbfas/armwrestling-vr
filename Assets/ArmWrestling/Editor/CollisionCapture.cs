using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ArmWrestlingVR.EditorTools
{
    internal sealed class CollisionCapture : IDisposable
    {
        internal const string Header = ",hand_proxy_pairs,hand_proxy_max_mm,torso_proxy_pairs,torso_proxy_max_mm,pad_proxy_pairs,pad_proxy_max_mm,digit_adjustment_deg,arm_swivel_deg";
        private static readonly CultureInfo Format = CultureInfo.InvariantCulture;
        private readonly AvatarCollisionExperiment experiment;
        private readonly StreamWriter details;
        private readonly Transform device;

        internal CollisionCapture(AvatarCollisionExperiment experiment, string folder, Transform device)
        {
            this.experiment = experiment != null && experiment.ProxyCount > 0
                ? experiment : throw new InvalidOperationException("The P2-B collision proxies did not start.");
            this.device = device;
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
                + string.Format(Format, ",{0:F4},{1:F4}", experiment.MaxFingerCorrection, experiment.ArmCorrection);
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
