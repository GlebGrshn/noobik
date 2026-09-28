using System.Collections.Generic;
using UnityEngine;

namespace Nubik
{
    /// <summary>Builds the temporary primitive art and shares one material per colour.</summary>
    public sealed class Shapes
    {
        private readonly GameObject cube, sphere;
        private readonly Material template;
        private readonly Dictionary<long, Material> materials = new Dictionary<long, Material>();

        public Shapes(GameObject cube, GameObject sphere, Material template)
        {
            this.cube = cube; this.sphere = sphere; this.template = template;
        }

        /// <summary>Material with a base colour, optional glow, immunity to the headlamp, and the lawn pattern.</summary>
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
            if (!collider) Object.Destroy(obj.GetComponent<Collider>());
            return obj;
        }

        public void Dispose()
        {
            foreach (var material in materials.Values) Object.Destroy(material);
            materials.Clear();
        }

        private static int Surface(string name)
        {
            string n = name.ToLowerInvariant();
            if (n.Contains("cthulhu") && !n.Contains("eye")) return 6;
            if (n.Contains("crystal") || n.Contains("glass") || n.Contains("seal key")) return 5;
            if (n.Contains("roof")) return 8;
            if (n.Contains("blanket") || n.Contains("pillow") || n.Contains("glove") || n.Contains("cuff") || n.Contains("umbrella")) return 4;
            if (n.Contains("wall") || n.Contains("plaster")) return 7;
            if (n.Contains("stone") || n.Contains("slab") || n.Contains("patio") || n.Contains("carved") || n.Contains("sanctum") || n.Contains("chimney") || n.Contains("rubble")) return 2;
            if (n.Contains("metal") || n.Contains("iron") || n.Contains("rail") || n.Contains("cart") || n.Contains("band") || n.Contains("lock") || n.Contains("blade") || n.Contains("drill") || n.Contains("motor") || n.Contains("fuel pump") || n.Contains("nugget")) return 3;
            if (n.Contains("wood") || n.Contains("timber") || n.Contains("support") || n.Contains("beam") || n.Contains("brace") || n.Contains("walkway") || n.Contains("sleeper") || n.Contains("crate") || n.Contains("supplies") || n.Contains("bed") || n.Contains("table") || n.Contains("leg") || n.Contains("chest") || n.Contains("lid") || n.Contains("fence") || n.Contains("trunk") || n.Contains("shaft") || n.Contains("shutter") || n.Contains("frame") || n.Contains("chair") || n.Contains("root")) return 1;
            return 0;
        }
    }
}
