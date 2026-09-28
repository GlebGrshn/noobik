using System.Collections.Generic;
using UnityEngine;

namespace Nubik
{
    /// <summary>The chamber beyond the five-seal door. Built once and enabled only while inside.</summary>
    public sealed class BossEncounter
    {
        public static readonly Vector3 Origin = new Vector3(55, -118, 0);
        public static Vector3 Spawn => Origin + new Vector3(0, .1f, -8.8f);
        public static Vector3 WeaponPoint => Origin + new Vector3(0, .8f, -6.7f);
        public BossBattle Battle { get; private set; } = new BossBattle();
        public bool Inside { get; private set; }
        private readonly Transform root, monster, weapon, ring, wave, beam;
        private readonly List<Transform> tendrils = new List<Transform>();
        private readonly List<Vector3> rests = new List<Vector3>();
        private readonly HashSet<Collider> bodyParts = new HashSet<Collider>();
        private readonly HashSet<Collider> headParts = new HashSet<Collider>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly Renderer[] eyes;
        private float age, beamTime;
        private readonly Shapes shapes;

        public BossEncounter(Shapes shapes)
        {
            this.shapes = shapes;
            root = new GameObject("Cthulhu sanctum").transform; root.position = Origin;
            var stone = new Color(.19f, .27f, .29f);
            shapes.Box("Carved sanctum floor", new Vector3(0, -.22f, 0), new Vector3(21, .44f, 25), stone, root, true);
            foreach (float x in new[] { -10.5f, 10.5f })
                shapes.Box("Sanctum wall", new Vector3(x, 4, 0), new Vector3(.6f, 8, 25), stone * .72f, root, true);
            foreach (float z in new[] { -12.5f, 12.5f })
                shapes.Box("Sanctum wall", new Vector3(0, 4, z), new Vector3(21, 8, .6f), stone * .72f, root, true);
            shapes.Box("Sanctum ceiling", new Vector3(0, 8.1f, 0), new Vector3(21, .5f, 25), stone * .55f, root, true);
            for (int i = 0; i < 6; i++)
                foreach (float x in new[] { -9.5f, 9.5f })
                {
                    float z = -10 + i * 4;
                    shapes.Box("Carved pillar", new Vector3(x, 3, z), new Vector3(.8f, 6, .8f), stone, root);
                    shapes.Box("Rune slit", new Vector3(x * .985f, 2.5f, z - .43f), new Vector3(.22f, 2.5f, .04f), new Color(.21f, .8f, .71f), root, false, .7f);
                }
            for (int i = 0; i < 5; i++)
                shapes.Box("Door seal", new Vector3((i - 2) * .7f, 2.2f, -12.12f), new Vector3(.3f, .5f, .05f), Expedition.Keys[i].Color, root, false, .6f);
            shapes.Box("Weapon pedestal", new Vector3(0, .32f, -6.7f), new Vector3(1.3f, .64f, .8f), stone * 1.5f, root);
            weapon = new GameObject("Ancient harpoon").transform; weapon.SetParent(root, false); weapon.localPosition = new Vector3(0, .9f, -6.7f);
            shapes.Box("Harpoon stock", Vector3.zero, new Vector3(.95f, .17f, .24f), new Color(.34f, .49f, .46f), weapon);
            shapes.Box("Harpoon rail", new Vector3(.15f, .13f, 0), new Vector3(1.25f, .05f, .07f), new Color(.62f, .95f, .9f), weapon, false, .6f);
            monster = new GameObject("Cthulhu").transform; monster.SetParent(root, false);
            var skin = new Color(.24f, .53f, .40f);
            Body(shapes.Ball("Cthulhu torso", new Vector3(0, 2.8f, 7.1f), new Vector3(4.7f, 5.2f, 3.5f), skin, monster, true), false);
            Body(shapes.Ball("Cthulhu head", new Vector3(0, 5, 6.1f), new Vector3(3.7f, 3.5f, 3.1f), skin * 1.15f, monster, true), true);
            var eyeList = new List<Renderer>();
            foreach (float x in new[] { -.84f, .84f })
            {
                eyeList.Add(shapes.Ball("Cthulhu eye", new Vector3(x, 5.18f, 4.73f), new Vector3(.65f, .32f, .28f), new Color(1, .7f, .24f), monster, false, .9f).GetComponent<Renderer>());
                shapes.Box("Cthulhu brow", new Vector3(x, 5.47f, 4.73f), new Vector3(.95f, .18f, .35f), Quaternion.Euler(0, 0, x > 0 ? 15 : -15), skin * .7f, monster);
            }
            eyes = eyeList.ToArray();
            for (int side = -1; side <= 1; side += 2)
            {
                shapes.Ball("Cthulhu arm", new Vector3(side * 2.5f, 2.3f, 6.3f), new Vector3(1.5f, 3.3f, 1.6f), skin, monster);
                shapes.Ball("Cthulhu claw", new Vector3(side * 2.7f, .7f, 5.7f), new Vector3(1.8f, .9f, 1.9f), skin * .75f, monster);
                for (int finger = 0; finger < 3; finger++)
                    shapes.Box("Cthulhu claw tip", new Vector3(side * (2.2f + finger * .42f), .36f, 4.8f), new Vector3(.18f, .22f, .7f), new Color(.56f, .64f, .48f), monster);
                Wing(side, skin * .65f);
            }
            for (int n = 0; n < 7; n++)
                for (int segment = 0; segment < 7; segment++)
                {
                    float t = segment / 6f, x = (n - 3) * .4f;
                    var p = new Vector3(x + Mathf.Sin(n * 2 + t * 3) * t * .65f, 4.4f - t * (2.4f + n % 2 * .5f), 4.8f - Mathf.Sin(t * 2) * .95f);
                    var obj = shapes.Ball("Cthulhu tentacle", p, Vector3.one * Mathf.Lerp(.52f, .17f, t), skin * (1 - n % 2 * .13f), monster);
                    tendrils.Add(obj.transform); rests.Add(p);
                }
            ring = MakeRing("Tentacle warning", new Color(1, .28f, .13f));
            wave = shapes.Box("Wave warning", Vector3.zero, new Vector3(9.7f, .025f, 24), new Color(.84f, .23f, .15f), root, false, .6f).transform;
            beam = shapes.Box("Harpoon trail", Vector3.zero, new Vector3(.035f, .035f, 1), new Color(.47f, 1, .95f), root, false, .9f).transform;
            foreach (var p in new[] { new Vector3(-5, 5, -3), new Vector3(5, 5, 5) })
            {
                var obj = new GameObject("Sanctum light", typeof(Light)); obj.transform.SetParent(root, false); obj.transform.localPosition = p;
                var light = obj.GetComponent<Light>(); light.type = LightType.Point; light.range = 19; light.intensity = 2.7f; light.color = new Color(.38f, .76f, .73f);
            }
            root.gameObject.SetActive(false);
        }

        private void Body(GameObject obj, bool head)
        {
            var collider = obj.GetComponent<Collider>(); collider.isTrigger = true;
            bodyParts.Add(collider); if (head) headParts.Add(collider);
        }
        private void Wing(int side, Color color)
        {
            var mesh = new Mesh { name = "Cthulhu wing" };
            mesh.vertices = new[] { new Vector3(side * 1.4f, 4, 7.5f), new Vector3(side * 6, 6.8f, 9), new Vector3(side * 5.3f, 2.3f, 8.4f), new Vector3(side * 3.8f, 3, 8), new Vector3(side * 2.2f, 1.8f, 7.8f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4, 2, 1, 0, 3, 2, 0, 4, 3, 0 }; mesh.RecalculateNormals(); meshes.Add(mesh);
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            var obj = new GameObject("Cthulhu wing", typeof(MeshFilter), typeof(MeshRenderer)); obj.transform.SetParent(monster, false);
            obj.GetComponent<MeshFilter>().sharedMesh = mesh; obj.GetComponent<MeshRenderer>().sharedMaterial = shapes.Mat(color);
            var start = new Vector3(side * 1.4f, 4, 7.45f);
            foreach (var end in new[] { new Vector3(side * 6, 6.8f, 8.95f), new Vector3(side * 5.3f, 2.3f, 8.35f) })
                shapes.Box("Cthulhu wing rib", (start + end) / 2, new Vector3(.14f, .14f, Vector3.Distance(start, end)), Quaternion.LookRotation(end - start), color * 1.6f, monster);
        }
        private Transform MakeRing(string name, Color color)
        {
            var mesh = new Mesh { name = name }; var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int i = 0; i <= 48; i++)
            {
                float a = i * Mathf.PI * 2 / 48; var p = new Vector3(Mathf.Cos(a), .01f, Mathf.Sin(a));
                vertices.Add(p * 2.6f); vertices.Add(p * 2.3f);
                if (i == 48) continue; int j = i * 2;
                triangles.AddRange(new[] { j, j + 1, j + 2, j + 1, j + 3, j + 2 });
            }
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); meshes.Add(mesh);
            var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); obj.transform.SetParent(root, false);
            obj.GetComponent<MeshFilter>().sharedMesh = mesh; obj.GetComponent<MeshRenderer>().sharedMaterial = shapes.Mat(color, .9f);
            return obj.transform;
        }
        public void Enter(bool armed)
        {
            Battle = new BossBattle(); age = 0; Inside = true; root.gameObject.SetActive(true);
            monster.gameObject.SetActive(true); monster.localScale = Vector3.one;
            weapon.gameObject.SetActive(!armed); ring.gameObject.SetActive(false); wave.gameObject.SetActive(false); beam.gameObject.SetActive(false);
            if (armed) Battle.Begin();
        }
        public void Arm() { weapon.gameObject.SetActive(false); Battle.Begin(); }
        public void Exit() { Inside = false; root.gameObject.SetActive(false); }
        public bool IsBoss(Collider collider) => bodyParts.Contains(collider);
        public float Shoot(Collider collider) => IsBoss(collider) ? Battle.Shoot(headParts.Contains(collider)) : 0;
        public void Trace(Vector3 from, Vector3 to)
        {
            beam.position = (from + to) / 2; beam.rotation = Quaternion.LookRotation(to - from);
            beam.localScale = new Vector3(.035f, .035f, Vector3.Distance(from, to)); beam.gameObject.SetActive(true); beamTime = .12f;
        }
        public float Tick(float dt, Vector3 player)
        {
            age += dt; beamTime -= dt; if (beamTime <= 0) beam.gameObject.SetActive(false);
            for (int i = 0; i < tendrils.Count; i++) tendrils[i].localPosition = rests[i] + new Vector3(Mathf.Sin(age * 2.3f + i * .35f) * .13f, Mathf.Cos(age * 1.7f + i) * .045f, 0);
            float damage = Battle.Tick(dt, player - Origin);
            bool warning = Battle.Phase == BattlePhase.Warning;
            ring.gameObject.SetActive(warning && Battle.Pattern == 0); wave.gameObject.SetActive(warning && Battle.Pattern != 0);
            ring.localPosition = new Vector3(Battle.Target.x, .04f, Battle.Target.y);
            wave.localPosition = new Vector3(Battle.Pattern == 1 ? -5.3f : 5.3f, .02f, 0);
            foreach (var eye in eyes) eye.sharedMaterial.SetColor("_Emission", new Color(1, .7f, .24f) * (Battle.Phase == BattlePhase.Recovery ? 2 : .4f));
            if (Battle.Phase == BattlePhase.Won) monster.localScale = Vector3.Lerp(monster.localScale, new Vector3(1, .14f, 1), dt * 2);
            return damage;
        }
        public void Dispose() { foreach (var mesh in meshes) Object.Destroy(mesh); }
    }
}
