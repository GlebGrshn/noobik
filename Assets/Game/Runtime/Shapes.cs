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
        public Material Mat(Color color, float glow = 0, bool extraLights = true, bool lawn = false)
        {
            Color32 c = color;
            long key = (long)c.r << 24 | (long)c.g << 16 | (long)c.b << 8 | (long)Mathf.RoundToInt(Mathf.Clamp01(glow) * 100) << 32 |
                (extraLights ? 0L : 1L << 40) | (lawn ? 1L << 41 : 0L);
            if (materials.TryGetValue(key, out var material)) return material;
            material = new Material(template) { name = "Nubik " + ColorUtility.ToHtmlStringRGB(color) };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Emission", color * glow);
            material.SetFloat("_ExtraLights", extraLights ? 1 : 0);
            material.SetFloat("_Lawn", lawn ? 1 : 0);
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
    }
}
