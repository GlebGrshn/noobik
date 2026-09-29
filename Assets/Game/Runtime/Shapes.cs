using System.Collections.Generic;
using UnityEngine;

namespace Nubik
{
    /// <summary>
    /// Builds the primitive art and shares one material per colour. Boxes get UVs in metres, with grain and boards
    /// along their longest side, so textures keep their size on any box and stay put on moving view models.
    /// </summary>
    public sealed class Shapes
    {
        private readonly GameObject cube, sphere;
        private readonly Material template;
        private readonly Dictionary<long, Material> materials = new Dictionary<long, Material>();
        private readonly Dictionary<Vector3Int, Mesh> boxes = new Dictionary<Vector3Int, Mesh>();
        private Mesh ball;

        public Shapes(GameObject cube, GameObject sphere, Material template)
        {
            this.cube = cube; this.sphere = sphere; this.template = template;
        }

        /// <summary>Material with a base colour, optional glow, immunity to the headlamp, the lawn pattern and a surface texture.</summary>
        public Material Mat(Color color, float glow = 0, bool extraLights = true, bool lawn = false, int surface = 0)
        {
            Color32 c = color;
            long key = (long)c.r << 24 | (long)c.g << 16 | (long)c.b << 8 | (long)Mathf.RoundToInt(Mathf.Clamp01(glow) * 100) << 32 |
                (extraLights ? 0L : 1L << 40) | (lawn ? 1L << 41 : 0L) | ((long)surface << 44);
            if (materials.TryGetValue(key, out var material)) return material;
            material = new Material(template) { name = "Nubik " + ColorUtility.ToHtmlStringRGB(color) };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Emission", color * glow);
            material.SetFloat("_ExtraLights", extraLights ? 1 : 0);
            material.SetFloat("_Lawn", lawn ? 1 : 0);
            material.SetFloat("_Surface", surface);
            // Only the hand-held view models ignore the headlamp; they are small and right at the eye, so denser texture.
            material.SetFloat("_SurfaceScale", extraLights ? 1 : 3);
            materials[key] = material;
            return material;
        }

        public GameObject Box(string name, Vector3 position, Vector3 scale, Color color, Transform parent = null, bool collider = false, float glow = 0) =>
            Make(name, cube, position, scale, Quaternion.identity, Mat(color, glow), parent, collider);

        public GameObject Box(string name, Vector3 position, Vector3 scale, Quaternion rotation, Color color, Transform parent = null, bool collider = false, float glow = 0) =>
            Make(name, cube, position, scale, rotation, Mat(color, glow), parent, collider);

        public GameObject Box(string name, Vector3 position, Vector3 scale, Material material, Transform parent = null, bool collider = false) =>
            Make(name, cube, position, scale, Quaternion.identity, material, parent, collider);

        public GameObject Ball(string name, Vector3 position, Vector3 scale, Color color, Transform parent = null, bool collider = false, float glow = 0) =>
            Make(name, sphere, position, scale, Quaternion.identity, Mat(color, glow), parent, collider);

        public GameObject Make(string name, GameObject prefab, Vector3 position, Vector3 scale, Quaternion rotation, Material material, Transform parent, bool collider)
        {
            int surface = Surface(name);
            if (surface != 0 && material.GetFloat("_Surface") < .5f && material.GetFloat("_Lawn") < .5f)
            {
                var color = material.GetColor("_BaseColor");
                float glow = material.GetColor("_Emission").maxColorComponent / Mathf.Max(.001f, color.maxColorComponent);
                material = Mat(color, glow, material.GetFloat("_ExtraLights") > .5f, false, surface);
            }
            // Serialized prefab references keep native mesh/collider types in stripped WebGL builds.
            var obj = Object.Instantiate(prefab);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localRotation = rotation;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            var filter = obj.GetComponent<MeshFilter>();
            if (prefab == cube) filter.sharedMesh = MetricBox(filter.sharedMesh, scale);
            else if (prefab == sphere) filter.sharedMesh = FlaggedBall(filter.sharedMesh);
            if (!collider) Object.Destroy(obj.GetComponent<Collider>());
            return obj;
        }

        /// <summary>
        /// The unit cube with UV = metres across each face (z = 1 marks real UVs for the shader) and matching tangents.
        /// U follows the longer side of the face; a small offset from the size keeps equal boxes from looking stamped.
        /// </summary>
        private Mesh MetricBox(Mesh source, Vector3 scale)
        {
            var key = Vector3Int.RoundToInt(scale * 1000);
            if (boxes.TryGetValue(key, out var mesh)) return mesh;
            var vertices = source.vertices;
            var normals = source.normals;
            var uv = new List<Vector3>(vertices.Length);
            var tangents = new Vector4[vertices.Length];
            var shift = new Vector2(Mathf.Repeat(scale.x * 7.13f + scale.y * 3.71f, 1), Mathf.Repeat(scale.z * 5.29f + scale.x * 1.93f, 1)) * 4;
            for (int i = 0; i < vertices.Length; i++)
            {
                var n = normals[i];
                var a = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
                int face = a.x > a.y && a.x > a.z ? 0 : a.y > a.z ? 1 : 2;
                int b = face == 0 ? 2 : 0, c = face == 1 ? 2 : 1;
                int along = scale[b] >= scale[c] ? b : c, across = along == b ? c : b;
                uv.Add(new Vector3(vertices[i][along] * scale[along] + shift.x, vertices[i][across] * scale[across] + shift.y, 1));
                var t = Vector3.zero; t[along] = 1;
                var bt = Vector3.zero; bt[across] = 1;
                tangents[i] = new Vector4(t.x, t.y, t.z, Vector3.Dot(Vector3.Cross(n, t), bt) >= 0 ? 1 : -1);
            }
            mesh = new Mesh { name = "Metric box" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.SetUVs(0, uv);
            mesh.tangents = tangents;
            mesh.triangles = source.triangles;
            mesh.RecalculateBounds();
            boxes[key] = mesh;
            return mesh;
        }

        /// <summary>The sphere with UV z = 0: the shader maps it by position instead (no seams, no poles).</summary>
        private Mesh FlaggedBall(Mesh source)
        {
            if (ball != null) return ball;
            var uv = new List<Vector3>();
            foreach (var p in source.uv) uv.Add(new Vector3(p.x, p.y, 0));
            ball = Object.Instantiate(source);
            ball.name = "Mapped ball";
            ball.SetUVs(0, uv);
            return ball;
        }

        public void Dispose()
        {
            foreach (var material in materials.Values) Object.Destroy(material);
            materials.Clear();
            foreach (var mesh in boxes.Values) Object.Destroy(mesh);
            boxes.Clear();
            if (ball != null) Object.Destroy(ball);
        }

        // Surface texture layers (layer = surface - 1), as generated by Tools/make_surfaces.py.
        public const int Wood = 1, Masonry = 2, Metal = 3, Cloth = 4, Crystal = 5, Skin = 6, Plaster = 7, Roof = 8,
            Planks = 9, Painted = 10, Rubber = 11, Wallpaper = 12, Rug = 13, Leather = 14, Bark = 15, Brick = 16;

        // First match wins: exceptions first, then glow and effects without texture, then materials.
        private static readonly (string[] words, int surface)[] Rules =
        {
            (new[] { "garage door", "umbrella pole" }, Painted),
            (new[] { "above door" }, Planks),
            (new[] { "mailbox post", "fence post" }, Wood),
            (new[] { "timber cap", "crate ore", "safe door" }, Metal),
            (new[] { "counter top", "bench top" }, Wood),
            (new[] { "meteor crust" }, Leather),
            (new[] { "eye", "pupil", "lamp", "light", "warning", "trail", "wax", "label", "smile", "socket", "rust", "keyhole",
                "waymark", "rune", "puff", "hill", "grass", "flower", "lump", "debris", "mushroom", "face", "nose", "lawn" }, 0),
            (new[] { "cthulhu" }, Skin),
            (new[] { "crystal", "glass", "seal key", "door seal", "jar", "seal pedestal" }, Crystal),
            (new[] { "rug" }, Rug),
            (new[] { "plaster" }, Wallpaper),
            (new[] { "roof" }, Roof),
            (new[] { "chimney", "garage", "patio" }, Brick),
            (new[] { "trunk", "root" }, Bark),
            (new[] { "grip", "hose", "tire" }, Rubber),
            (new[] { "glove", "belt", "boot", "leather", "backpack" }, Leather),
            (new[] { "blanket", "pillow", "cuff", "umbrella", "doormat", "coat", "gnome hat", "beard", "cloth", "map", "paper" }, Cloth),
            (new[] { "crown", "hedge" }, Skin),
            (new[] { "ceiling" }, Plaster),
            (new[] { "sealed", "door frame", "lintel", "stone", "slab", "carved", "sanctum", "rubble", "pillar", "pedestal", "plinth",
                "fossil", "shell", "rib", "pebble" }, Masonry),
            (new[] { "pump", "jetpack", "motor", "stock", "mailbox", "pole", "tank", "shutter", "window frame", "mullion", "transom",
                "stake tip", "capsule cap", "sign", "helmet", "brim", "meter" }, Painted),
            (new[] { "metal", "iron", "rail", "cart", "band", "lock", "blade", "drill", "spiral", "barrel", "vise", "nozzle", "collar",
                "prong", "gear", "axle", "scale", "lantern", "hook", "strap", "safe", "capsule", "nugget", "golden", "tool", "key" }, Metal),
            (new[] { "wood", "timber", "support", "beam", "brace", "leg", "chair", "bed", "table", "shelf", "lid", "shaft", "frame",
                "pegboard", "stake", "door", "post" }, Wood),
            (new[] { "floor", "fence", "walkway", "crate", "supplies", "sleeper", "counter", "workbench", "chest", "house" }, Planks),
        };

        public static int Surface(string name)
        {
            string n = name.ToLowerInvariant();
            foreach (var (words, surface) in Rules)
                foreach (var word in words)
                    if (n.Contains(word)) return surface;
            return 0;
        }
    }
}
