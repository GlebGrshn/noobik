using System.Collections.Generic;
using UnityEngine;

namespace Nubik
{
    public sealed class MineSite
    {
        public readonly string Name, Note;
        public readonly int Depth, CacheId;
        public readonly Color Accent;
        public MineSite(string name, string note, int depth, int cacheId, Color accent)
        { Name = name; Note = note; Depth = depth; CacheId = cacheId; Accent = accent; }
        public Vector3 Origin => new Vector3(0, -Depth, 0);
        public Vector3 Cache => Origin + new Vector3(1.45f, 0.26f, 2.8f);
        public bool Contains(Vector3 feet) => Mathf.Abs(feet.x) < 2.35f && feet.z > 0.1f && feet.z < 3.8f &&
            feet.y > -Depth - 0.35f && feet.y < -Depth + 2.5f;
    }

    /// <summary>Stable landmarks and cache IDs. Existing terrain and find generation keep their original seed.</summary>
    public sealed class MineSites
    {
        public static readonly MineSite[] All =
        {
            new MineSite("Лагерь у корней", "Кто-то оставил здесь припасы. Тайник у дальней стены.", 18, 16, new Color(.72f, .84f, .40f)),
            new MineSite("Старая штольня", "Рельсы обрываются. Рядом с вагонеткой остался тайник.", 48, 17, new Color(1f, .72f, .32f)),
            new MineSite("Кристальный грот", "Свет под землёй. На старом настиле сохранился ларец.", 88, 18, new Color(.34f, .92f, .88f))
        };
        private readonly List<Transform> roots = new List<Transform>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private static readonly Color Timber = new Color(.34f, .23f, .16f), CutWood = new Color(.55f, .39f, .24f);
        private static readonly Color Iron = new Color(.19f, .25f, .28f), Brass = new Color(.69f, .47f, .23f);

        /// <summary>Keep generated ore embedded in authored walls, retaining its original chunk/slot save ID.
        /// This uses room bounds, not mutable player excavation, so later digging never moves ore.</summary>
        public static Vector3 AnchorOre(Vector3 position, VoxelTerrain terrain)
        {
            foreach (var site in All)
            {
                var p = position - site.Origin;
                if (p.y <= 0 || p.y >= 3.5f) continue;
                float wall = 0;
                if (p.z > -1.1f && p.z < 3.9f && Mathf.Abs(p.x) < 2.5f) wall = 2.47f;
                else if (p.y < 2.8f && p.z > -2.6f && p.z <= -1.1f && Mathf.Abs(p.x) < 1.1f) wall = 1.07f;
                if (wall == 0) continue;
                float sign = p.x < 0 ? -1 : 1;
                // A natural cavern may have already removed this wall. Walk out to original solid rock,
                // never sampling the player's edits, so saving and digging cannot relocate the find.
                var at = new Vector3(wall * sign, position.y, position.z);
                float edge = terrain.Config.width / 2 - .1f;
                while (terrain.Sample(at, true) < VoxelTerrain.Iso && Mathf.Abs(at.x) < edge)
                    at.x = sign * Mathf.Min(edge, Mathf.Abs(at.x) + .15f);
                return at;
            }
            return position;
        }

        public static void Carve(VoxelTerrain terrain)
        {
            foreach (var site in All)
            {
                terrain.CarveRoom(site.Origin + new Vector3(0, 1.75f, 1.4f), new Vector3(2.5f, 1.75f, 2.5f));
                terrain.CarveRoom(site.Origin + new Vector3(0, 1.4f, -1.2f), new Vector3(1.1f, 1.4f, 1.4f));
                // Recess below the flush walkway keeps its planks visible without raising the depth reading.
                terrain.CarveRoom(site.Origin + new Vector3(0, -.08f, 1.4f), new Vector3(.8f, .2f, 2.2f));
            }
        }

        public MineSites(Shapes s)
        {
            for (int i = 0; i < All.Length; i++)
            {
                var site = All[i];
                var root = new GameObject(site.Name).transform;
                root.position = site.Origin;
                roots.Add(root);
                // The central walkway leaves both sides open for digging and returning to the shaft.
                for (int n = 0; n < 12; n++)
                    s.Box("Walkway", new Vector3(0, -.07f, -.6f + n * .36f), new Vector3(1.55f, .14f, .32f), n % 3 == 0 ? Timber : CutWood, root, true);
                foreach (float z in new[] { -.65f, 1.35f, 3.4f })
                {
                    foreach (float x in new[] { -1.98f, 1.98f })
                    {
                        s.Box("Support", new Vector3(x, 1.55f, z), new Vector3(.22f, 3.1f, .26f), Timber, root);
                        s.Box("Iron strap", new Vector3(x, 2.6f, z), new Vector3(.25f, .17f, .29f), Iron, root);
                        s.Box("Brace", new Vector3(x * .8f, 2.84f, z), new Vector3(.16f, .85f, .18f), Quaternion.Euler(0, 0, x < 0 ? -42 : 42), CutWood, root);
                    }
                    s.Box("Crossbeam", new Vector3(0, 3.07f, z), new Vector3(4.3f, .24f, .30f), CutWood, root);
                }
                Lantern(s, root, new Vector3(-1.6f, 2.2f, .2f), i == 2 ? site.Accent : new Color(1f, .7f, .3f));
                Lantern(s, root, new Vector3(1.6f, 2.2f, 3.05f), site.Accent);
                // Broad, glowing entrance marker: visible from the shaft without adding another UI panel.
                s.Box("Waymark", new Vector3(0, 2.87f, -.83f), new Vector3(.72f, .18f, .06f), site.Accent, root, false, .55f);
                if (i == 0) Camp(s, root);
                else if (i == 1) Railway(s, root);
                else Crystals(s, root);
                Combine(root);
                root.gameObject.SetActive(false);
            }
        }

        private static void Lantern(Shapes s, Transform root, Vector3 p, Color color)
        {
            s.Box("Lantern hook", p + Vector3.up * .33f, new Vector3(.05f, .42f, .05f), Iron, root);
            s.Box("Lantern glass", p, new Vector3(.18f, .28f, .18f), color, root, false, .8f);
            foreach (float y in new[] { -.18f, .18f }) s.Box("Lantern cap", p + Vector3.up * y, new Vector3(.28f, .07f, .28f), Iron, root);
            var lamp = new GameObject("Lantern light", typeof(Light));
            lamp.transform.SetParent(root, false); lamp.transform.localPosition = p;
            var light = lamp.GetComponent<Light>(); light.type = LightType.Point; light.range = 5;
            light.color = color; light.intensity = 1.8f; light.shadows = LightShadows.None;
        }

        private static void Camp(Shapes s, Transform root)
        {
            // A low bunk, supply crates and roots distinguish the warm topsoil camp.
            s.Box("Bed frame", new Vector3(-1.55f, .33f, 2.1f), new Vector3(.8f, .22f, 1.55f), Timber, root);
            s.Box("Folded blanket", new Vector3(-1.55f, .51f, 2.3f), new Vector3(.73f, .15f, 1.1f), new Color(.35f, .43f, .28f), root);
            s.Box("Pillow", new Vector3(-1.55f, .54f, 1.5f), new Vector3(.6f, .17f, .32f), new Color(.78f, .72f, .53f), root);
            foreach (var p in new[] { new Vector3(1.65f, .32f, .5f), new Vector3(1.9f, .26f, 1.25f) })
            {
                s.Box("Supplies", p, new Vector3(.57f, .55f, .55f), CutWood, root);
                foreach (float x in new[] { -.2f, .2f }) s.Box("Crate band", p + new Vector3(x, 0, -.29f), new Vector3(.055f, .58f, .04f), Timber, root);
            }
            for (int n = 0; n < 8; n++)
                s.Box("Root", new Vector3((n % 2 == 0 ? -1 : 1) * (1.7f + n % 3 * .12f), 3.15f - n % 3 * .15f, -.25f + n * .48f),
                    new Vector3(.09f, .65f + n % 3 * .18f, .12f), Quaternion.Euler(12, n * 29, n % 2 == 0 ? 24 : -30), Timber, root);
        }

        private static void Railway(Shapes s, Transform root)
        {
            for (int n = 0; n < 9; n++)
                s.Box("Sleeper", new Vector3(-1.35f, .1f, -.35f + n * .42f), new Vector3(1.15f, .12f, .14f), Timber, root);
            foreach (float x in new[] { -1.72f, -.98f })
                s.Box("Rail", new Vector3(x, .22f, 1.35f), new Vector3(.065f, .09f, 3.8f), Iron, root);
            var at = new Vector3(-1.35f, .66f, 2.55f);
            s.Box("Cart base", at, new Vector3(.95f, .18f, 1.15f), Iron, root);
            foreach (float x in new[] { -.46f, .46f }) s.Box("Cart side", at + new Vector3(x, .34f, 0), new Vector3(.08f, .55f, 1.16f), Brass, root);
            foreach (float z in new[] { -.54f, .54f }) s.Box("Cart end", at + new Vector3(0, .34f, z), new Vector3(.9f, .55f, .07f), Brass, root);
            foreach (float x in new[] { -.46f, .46f })
                foreach (float z in new[] { -.36f, .36f })
                    s.Ball("Cart wheel", at + new Vector3(x, -.27f, z), new Vector3(.14f, .32f, .32f), Iron, root);
            for (int n = 0; n < 5; n++) s.Ball("Cart rubble", at + new Vector3((n % 2 - .5f) * .4f, .22f, (n - 2) * .18f), Vector3.one * .32f, new Color(.41f, .44f, .47f), root);
        }

        private void Crystals(Shapes s, Transform root)
        {
            for (int n = 0; n < 9; n++)
            {
                var at = new Vector3(n % 2 == 0 ? -1.8f : 2.15f, .35f, -.3f + n * .42f);
                float size = .6f + n % 3 * .3f;
                Crystal(s, root, at - Vector3.up * .25f, size * 1.5f, Quaternion.Euler(8, n * 37, n % 2 == 0 ? -16 : 16),
                    n % 3 == 0 ? new Color(.54f, .42f, .82f) : new Color(.22f, .65f, .66f));
                s.Ball("Crystal bed", at - Vector3.up * .25f, new Vector3(.65f, .35f, .6f), new Color(.24f, .25f, .36f), root);
            }
            s.Box("Old survey table", new Vector3(-1.45f, .85f, 2.85f), new Vector3(1.1f, .16f, .65f), CutWood, root);
            foreach (float x in new[] { -1.88f, -1.02f }) s.Box("Table leg", new Vector3(x, .43f, 2.85f), new Vector3(.12f, .8f, .46f), Timber, root);
            s.Box("Survey map", new Vector3(-1.45f, .94f, 2.85f), new Vector3(.75f, .02f, .45f), new Color(.65f, .71f, .58f), root);
        }

        private void Crystal(Shapes s, Transform root, Vector3 position, float height, Quaternion rotation, Color color)
        {
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                int index = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c);
                indices.Add(index); indices.Add(index + 1); indices.Add(index + 2);
            }
            for (int n = 0; n < 6; n++)
            {
                float a = n * Mathf.PI / 3, b = (n + 1) * Mathf.PI / 3;
                var loA = new Vector3(Mathf.Cos(a) * .19f, 0, Mathf.Sin(a) * .19f);
                var loB = new Vector3(Mathf.Cos(b) * .19f, 0, Mathf.Sin(b) * .19f);
                var hiA = loA + Vector3.up * height * .67f;
                var hiB = loB + Vector3.up * height * .67f;
                Triangle(loA, hiA, loB); Triangle(loB, hiA, hiB);
                Triangle(hiA, Vector3.up * height, hiB);
                Triangle(Vector3.zero, loA, loB);
            }
            var mesh = new Mesh { name = "Faceted crystal" };
            mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); meshes.Add(mesh);
            var obj = new GameObject("Crystal", typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(root, false); obj.transform.localPosition = position; obj.transform.localRotation = rotation;
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            obj.GetComponent<MeshRenderer>().sharedMaterial = s.Mat(color, .45f);
        }

        private void Combine(Transform root)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (!groups.TryGetValue(renderer.sharedMaterial, out var group))
                    groups.Add(renderer.sharedMaterial, group = new List<CombineInstance>());
                group.Add(new CombineInstance { mesh = renderer.GetComponent<MeshFilter>().sharedMesh,
                    transform = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix });
                renderer.enabled = false;
            }
            foreach (var group in groups)
            {
                var mesh = new Mesh { name = "Mine site scenery" };
                mesh.CombineMeshes(group.Value.ToArray()); meshes.Add(mesh);
                var obj = new GameObject("Scenery", typeof(MeshFilter), typeof(MeshRenderer)); obj.transform.SetParent(root, false);
                obj.GetComponent<MeshFilter>().sharedMesh = mesh; obj.GetComponent<MeshRenderer>().sharedMaterial = group.Key;
            }
        }

        public void UpdateVisibility(float depth)
        {
            for (int i = 0; i < roots.Count; i++)
            {
                bool visible = Mathf.Abs(depth - All[i].Depth) < 16;
                if (roots[i].gameObject.activeSelf != visible) roots[i].gameObject.SetActive(visible);
            }
        }
        public void Dispose() { foreach (var mesh in meshes) Object.Destroy(mesh); }
    }
}
