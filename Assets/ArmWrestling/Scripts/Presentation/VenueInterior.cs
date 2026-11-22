using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArmWrestlingVR
{
    // Dimensions are in metres above the floor, in the same X/Z frame as the table.
    // Each finish uses a single combined mesh. Seating, wall bars and signage have no physics.
    internal sealed class VenueInterior
    {
        private static readonly Color Chalk = new Color32(234, 228, 210, 255);
        private static readonly Color Timber = new Color32(202, 146, 90, 255);
        private static readonly Color Teal = new Color32(32, 104, 102, 255);
        private static readonly Color Ink = new Color32(35, 47, 60, 255);
        private static readonly Color Brass = new Color32(242, 173, 68, 255);
        private static readonly Color Crimson = new Color32(180, 47, 62, 255);
        private static readonly Color Blue = new Color32(52, 100, 159, 255);
        private static readonly Color Glass = new Color32(183, 220, 223, 255);
        private readonly Dictionary<Color, List<CombineInstance>> surfaces = new Dictionary<Color, List<CombineInstance>>();
        private readonly Transform root;
        private readonly Mesh cube, cylinder;

        private VenueInterior(Transform root)
        {
            this.root = root;
            cube = PrimitiveMesh(PrimitiveType.Cube);
            cylinder = PrimitiveMesh(PrimitiveType.Cylinder);
        }

        internal static void Build(Transform root, ArenaTheme theme, List<Mesh> owned)
        {
            var interior = new VenueInterior(root);
            bool gym = theme == ArenaTheme.TrainingGym;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = gym ? new Color(0.74f, 0.76f, 0.73f) : new Color(0.44f, 0.47f, 0.58f);
            RenderSettings.fog = false;
            interior.Light("House light", LightType.Directional, Vector3.zero,
                Quaternion.Euler(49f, gym ? -34f : 151f, 0f), gym ? new Color(1f, 0.94f, 0.82f) : new Color(0.82f, 0.87f, 1f),
                gym ? 0.90f : 0.70f);
            if (gym) interior.Gym();
            else interior.Competition();
            interior.Finish(owned);
        }

        private void Gym()
        {
            Room(5f, 5.4f, 4.2f, Timber, Chalk, Chalk);
            // The panelled wall and high clerestory wrap behind the opponent.
            Box(Teal, 0, 0.51f, 5.28f, 9.96f, 1.02f, 0.08f);
            Box(Chalk, 0, 1.01f, 5.22f, 9.96f, 0.055f, 0.06f);
            for (int bay = -2; bay <= 2; bay++)
            {
                float x = bay * 1.9f;
                Box(Ink, x, 3.04f, 5.25f, 1.74f, 1.45f, 0.12f);
                Box(Glass, x, 3.04f, 5.15f, 1.61f, 1.32f, 0.07f);
                Box(Chalk, x, 3.04f, 5.1f, 0.05f, 1.33f, 0.04f);
                Box(Timber, x, 2.29f, 5.15f, 1.84f, 0.085f, 0.25f);
            }
            for (int bay = -2; bay <= 2; bay++)
            {
                float z = bay * 2.15f;
                Box(Timber, 0, 3.98f, z, 9.95f, 0.34f, 0.19f);
                Box(Chalk, 0, 3.78f, z, 4.2f, 0.055f, 0.10f);
                Box(Timber, -4.88f, 1.98f, z, 0.17f, 3.96f, 0.20f);
                Box(Timber, 4.88f, 1.98f, z, 0.17f, 3.96f, 0.20f);
            }
            // Two contrasting court lines and a parquet pattern readable from standing height.
            for (int plank = -9; plank <= 9; plank++)
            {
                float x = plank * 0.52f;
                Box(new Color32(176, 126, 82, 255), x, 0.004f, 0, 0.015f, 0.006f, 10.7f);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Box(Chalk, side * 2.18f, 0.009f, 0, 0.054f, 0.008f, 6.30f);
                Box(Chalk, 0, 0.009f, side * 3.15f, 4.41f, 0.008f, 0.054f);
            }
            // Left hand weight station: stacked colour-coded bumper plates on a racked bar.
            Box(Teal, -2.67f, 1.76f, 5.19f, 2.80f, 0.81f, 0.10f);
            Word("CLUB", -2.67f, 1.76f, 5.12f, 0.062f, Chalk);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = -2.67f + side * 0.77f;
                Box(Ink, x, 0.68f, 3.92f, 0.075f, 1.36f, 0.09f);
                Box(Ink, x, 0.05f, 3.92f, 0.14f, 0.1f, 0.78f);
                for (int plate = 0; plate < 3; plate++)
                    Disc(plate == 1 ? Brass : Teal, -2.67f + side * (0.93f + 0.09f * plate), 1.19f,
                        3.91f, 0.085f, plate == 2 ? 0.39f : 0.53f);
            }
            Box(Ink, -2.67f, 1.19f, 3.92f, 2.6f, 0.047f, 0.047f);
            Box(Teal, -2.67f, 0.47f, 3.61f, 0.46f, 0.11f, 1.30f);
            Box(Ink, -2.67f, 0.23f, 3.61f, 0.13f, 0.46f, 1.0f);

            // Two oak wall bars; gymnastic rings hang from the front beam.
            for (int bay = 0; bay < 2; bay++)
            {
                float x = 2.35f + bay * 1.15f;
                for (int side = -1; side <= 1; side += 2)
                    Box(Timber, x + 0.50f * side, 1.19f, 4.99f, 0.085f, 2.30f, 0.15f);
                for (int rung = 0; rung < 9; rung++)
                    Box(Timber, x, 0.31f + rung * 0.23f, 4.86f, 1.06f, 0.057f, 0.075f);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                float x = 2.96f + side * 0.24f;
                Box(Ink, x, 2.81f, 2.15f, 0.037f, 2.0f, 0.032f);
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * Mathf.PI / 6f;
                    Box(Timber, x + Mathf.Sin(angle) * 0.117f, 1.71f + Mathf.Cos(angle) * 0.117f,
                        2.15f, 0.073f, 0.027f, 0.035f, Quaternion.Euler(0, 0, -30f * i));
                }
            }
        }

        private void Competition()
        {
            Room(5.7f, 6.25f, 5.2f, Ink, new Color32(31, 43, 70, 255), Ink);
            // Raised decks outside the competition platform, with a central access aisle.
            for (int side = -1; side <= 1; side += 2)
            {
                Color team = side < 0 ? Blue : Crimson;
                for (int row = 0; row < 3; row++)
                {
                    float z = 3.4f + row * 0.75f, rise = 0.18f + row * 0.31f;
                    Box(Ink, side * 3.18f, rise * 0.5f, z, 4.4f, rise, 0.74f);
                    Box(Brass, side * 3.18f, rise, z - 0.35f, 4.4f, 0.028f, 0.035f);
                    for (int seat = 0; seat < 6; seat++)
                    {
                        float x = side * (1.43f + 0.69f * seat);
                        Box(team, x, rise + 0.19f, z, 0.53f, 0.09f, 0.46f);
                        Box(team, x, rise + 0.40f, z + 0.20f, 0.53f, 0.40f, 0.08f, Quaternion.Euler(8f, 0, 0));
                        Box(Ink, x, rise + 0.09f, z, 0.13f, 0.18f, 0.24f);
                    }
                }
                // Tall team pennants surround the event sign, with a chevron woven into each.
                for (int i = 0; i < 2; i++)
                {
                    float x = side * (2.85f + i * 1.43f);
                    Box(team, x, 2.94f, 6.07f, 1.08f, 3.34f, 0.07f);
                    Box(Brass, x, 4.58f, 5.99f, 1.15f, 0.072f, 0.11f);
                    for (int stripe = 0; stripe < 2; stripe++)
                    {
                        float y = 2.08f + 0.29f * stripe;
                        Box(Chalk, x - 0.20f, y, 6.01f, 0.51f, 0.105f, 0.06f, Quaternion.Euler(0, 0, -28f));
                        Box(Chalk, x + 0.20f, y, 6.01f, 0.51f, 0.105f, 0.06f, Quaternion.Euler(0, 0, 28f));
                    }
                }
                // Lit edge and corner marks on the platform keep the fixed table at floor height.
                Box(Brass, side * 2.08f, 0.008f, 0, 0.05f, 0.01f, 4.12f);
                Box(team, 0, 0.009f, side * 2.06f, 4.21f, 0.011f, 0.07f);
            }
            Box(Ink, 0, 3.23f, 5.94f, 4.28f, 1.37f, 0.26f);
            Box(Brass, 0, 3.89f, 5.79f, 4.26f, 0.07f, 0.06f);
            Box(Brass, 0, 2.57f, 5.79f, 4.26f, 0.045f, 0.06f);
            Word("FINALS", 0, 3.25f, 5.78f, 0.096f, Chalk);

            // A suspended lighting bridge with two rows of diagonal steel braces.
            for (int rail = 0; rail < 2; rail++)
            {
                float z = rail == 0 ? 2.8f : -2.8f;
                Box(Ink, 0, 4.33f, z, 10f, 0.07f, 0.09f);
                Box(Ink, 0, 4.72f, z, 10f, 0.07f, 0.09f);
                for (int i = 0; i < 20; i++)
                    Box(Ink, -4.75f + i * 0.5f, 4.52f, z, 0.64f, 0.047f, 0.05f, Quaternion.Euler(0, 0, i % 2 == 0 ? 38f : -38f));
                for (int i = -2; i <= 2; i++)
                {
                    Box(Ink, i * 2.04f, 4.1f, z, 0.40f, 0.43f, 0.40f);
                    Box(Brass, i * 2.04f, 3.9f, z - 0.02f, 0.32f, 0.055f, 0.33f);
                }
            }
            Light("Match spotlight", LightType.Spot, new Vector3(-1.1f, 3.55f, -1.40f),
                Quaternion.LookRotation(new Vector3(1.1f, -3.2f, 1.7f)), new Color(1f, 0.88f, 0.7f), 2.5f);
        }

        private void Room(float halfX, float halfZ, float height, Color floor, Color wall, Color ceiling)
        {
            Box(floor, 0, -0.04f, 0, 2f * halfX, 0.08f, 2f * halfZ);
            var ground = root.gameObject.AddComponent<BoxCollider>();
            ground.center = new Vector3(0, -ArenaFactory.TableHeight - 0.04f, 0);
            ground.size = new Vector3(2f * halfX, 0.08f, 2f * halfZ);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(wall, side * halfX, height / 2, 0, 0.12f, height, halfZ * 2);
                Box(wall, 0, height / 2, side * halfZ, halfX * 2, height, 0.12f);
            }
            Box(ceiling, 0, height, 0, halfX * 2, 0.12f, halfZ * 2);
        }

        private void Light(string name, LightType type, Vector3 position, Quaternion rotation, Color color, float intensity)
        {
            var lamp = new GameObject(name).AddComponent<Light>();
            lamp.transform.SetParent(root, false);
            lamp.transform.localPosition = position;
            lamp.transform.localRotation = rotation;
            lamp.type = type;
            lamp.color = color;
            lamp.intensity = intensity;
            lamp.shadows = type == LightType.Directional ? LightShadows.Hard : LightShadows.None;
            lamp.shadowStrength = 0.42f;
            if (type == LightType.Spot) { lamp.range = 8f; lamp.spotAngle = 58f; }
        }

        // Original five-column display lettering. No external typeface, textures or draw calls per letter.
        private static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
        {
            {'C', "01111100001000010000100001000001111"}, {'L', "10000100001000010000100001000011111"},
            {'U', "10001100011000110001100011000101110"}, {'B', "11110100011000111110100011000111110"},
            {'F', "11111100001000011110100001000010000"}, {'I', "11111001000010000100001000010011111"},
            {'N', "10001110011100110101100111001110001"}, {'A', "01110100011000111111100011000110001"},
            {'S', "01111100001000001110000010000111110"}
        };

        private void Word(string word, float center, float y, float z, float dot, Color color)
        {
            float left = center - (word.Length * 6 - 2) * dot / 2f;
            for (int i = 0; i < word.Length; i++)
            {
                string glyph = Glyphs[word[i]];
                for (int row = 0; row < 7; row++)
                    for (int col = 0; col < 5; col++)
                        if (glyph[row * 5 + col] == '1')
                            Box(color, left + (i * 6 + col) * dot, y + (3 - row) * dot, z, dot * 0.94f, dot * 0.94f, 0.024f);
            }
        }

        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            var sample = GameObject.CreatePrimitive(type);
            var mesh = sample.GetComponent<MeshFilter>().sharedMesh;
            sample.SetActive(false);
            ArenaEnvironment.Release(sample);
            return mesh;
        }

        private void Box(Color color, float x, float y, float z, float width, float height, float depth, Quaternion? rotation = null)
        {
            Add(cube, color, new Vector3(x, y, z), new Vector3(width, height, depth), rotation ?? Quaternion.identity);
        }

        private void Disc(Color color, float x, float y, float z, float width, float diameter)
        {
            Add(cylinder, color, new Vector3(x, y, z), new Vector3(diameter, width * 0.5f, diameter), Quaternion.Euler(0, 0, 90f));
        }

        private void Add(Mesh mesh, Color color, Vector3 position, Vector3 scale, Quaternion rotation)
        {
            if (!surfaces.TryGetValue(color, out var pieces))
                surfaces.Add(color, pieces = new List<CombineInstance>());
            position.y -= ArenaFactory.TableHeight;
            pieces.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, rotation, scale) });
        }

        private void Finish(List<Mesh> owned)
        {
            // Share a standard material already used by the table; per-renderer colour needs no material instance.
            Material standard = ArenaFactory.GetMaterial(new Color(0.16f, 0.17f, 0.19f));
            var properties = new MaterialPropertyBlock();
            foreach (var surface in surfaces)
            {
                var part = new GameObject("Finish " + ColorUtility.ToHtmlStringRGB(surface.Key));
                part.transform.SetParent(root, false);
                var mesh = new Mesh { name = part.name };
                owned.Add(mesh);
                mesh.CombineMeshes(surface.Value.ToArray());
                mesh.UploadMeshData(true);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = part.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = standard;
                properties.SetColor("_Color", surface.Key);
                renderer.SetPropertyBlock(properties);
            }
        }
    }
}
