using System.Collections.Generic;
using UnityEngine;

namespace Nubik
{
    /// <summary>Temporary backyard and the sealed door, built from primitives until Blender art arrives.</summary>
    public sealed class Yard
    {
        public static readonly Vector3 FirstSpawn = new Vector3(0, 0.05f, -4.6f);
        public static readonly Vector3 SurfaceSpawn = new Vector3(-6.6f, 0.05f, -9.2f);
        public const float SurfaceYaw = 35;
        /// <summary>Where a fainted or rescued player wakes up, facing the workbench.</summary>
        public static readonly Vector3 HomeSpawn = new Vector3(-3, 0.15f, 19.2f);
        public const float HomeYaw = 0;
        public static readonly Vector3 FuelPumpPoint = new Vector3(8.5f, 0, 14.8f);
        public static bool AtFuelPump(Vector3 p) => p.y > -.3f && p.y < 1.2f && Vector2.Distance(new Vector2(p.x, p.z), new Vector2(FuelPumpPoint.x, FuelPumpPoint.z)) < 2.4f;
        /// <summary>Standing spots in front of the ore buyer's counter and the workbench.</summary>
        public static readonly Vector3 CounterPoint = new Vector3(-7.6f, 0, 22.4f);
        public static readonly Vector3 WorkbenchPoint = new Vector3(1.8f, 0, 22.6f);
        public static bool InsideHouse(Vector3 p) => p.x > -10.3f && p.x < 4.3f && p.z > 17.3f && p.z < 24.7f && p.y > -0.5f && p.y < 3;
        public Vector3 DoorPoint { get; private set; }
        public readonly List<Renderer> Runes = new List<Renderer>();
        public Light DoorLight { get; private set; }

        // Same as the terrain shader's grass so the diggable patch blends into the lawn.
        private static readonly Color Lawn = new Color(0.42f, 0.55f, 0.30f);
        private static readonly Color Wood = new Color(0.60f, 0.40f, 0.25f);
        private static readonly Color DarkWood = new Color(0.40f, 0.26f, 0.17f);
        private static readonly Color Hedge = new Color(0.27f, 0.50f, 0.20f);
        private readonly List<Mesh> combinedMeshes = new List<Mesh>();
        private Transform clouds;
        private int shownSeals = -1;

        public Yard(Shapes s, MineConfig config)
        {
            var root = new GameObject("Yard").transform;
            float half = config.width / 2f, lip = 0.35f, top = -0.005f;
            // Lawn around the diggable patch. Terrain starts at the centre of the outer cells (0.25 m in),
            // so the lawn reaches a little further to hide the seam.
            void Lawn(float x0, float x1, float z0, float z1) =>
                s.Box("Lawn", new Vector3((x0 + x1) / 2, top - 0.15f, (z0 + z1) / 2), new Vector3(x1 - x0, 0.3f, z1 - z0), s.Mat(Yard.Lawn, 0, true, true), root, true);
            Lawn(-18, 18, -16, -half + lip);
            Lawn(-18, 18, half - lip, 17.3f);
            Lawn(-18, -half + lip, -half + lip, half - lip);
            Lawn(half - lip, 18, -half + lip, half - lip);
            foreach (var corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            {
                var p = new Vector3(corner.x * (half + 0.1f), 0.3f, corner.y * (half + 0.1f));
                s.Box("Dig stake", p, new Vector3(0.08f, 0.6f, 0.08f), Wood, root);
                s.Box("Stake tip", p + Vector3.up * 0.33f, new Vector3(0.1f, 0.1f, 0.1f), new Color(1f, 0.45f, 0.15f), root);
            }

            // Brick patio and the house to the north.
            s.Box("Patio", new Vector3(-1, 0.04f, 15.5f), new Vector3(26, 0.1f, 3.2f), new Color(0.74f, 0.48f, 0.36f), root, true);
            var wall = new Color(0.60f, 0.55f, 0.48f);
            BuildHouse(s, root, wall);
            var pump = FuelPumpPoint;
            s.Box("Fuel pump base", pump + Vector3.up * .15f, new Vector3(.95f, .3f, .85f), new Color(.28f, .33f, .32f), root, true);
            s.Box("Fuel pump housing", pump + Vector3.up * .95f, new Vector3(.65f, 1.4f, .55f), new Color(.75f, .30f, .16f), root, true);
            s.Box("Fuel pump meter", pump + new Vector3(0, 1.35f, -.29f), new Vector3(.48f, .34f, .04f), new Color(.1f, .2f, .2f), root);
            s.Box("Fuel hose", pump + new Vector3(.49f, .8f, 0), new Vector3(.08f, 1.25f, .08f), new Color(.13f, .17f, .17f), root);
            s.Box("Fuel nozzle", pump + new Vector3(.43f, 1.36f, -.12f), new Vector3(.2f, .15f, .3f), new Color(.32f, .4f, .4f), root);
            WorldSign(root, pump + new Vector3(0, 1.35f, -.32f), 0, "БЕНЗИН", new Color(1, .8f, .32f), .45f, .24f);
            WorldSign(root, pump + new Vector3(0, 2.2f, -.05f), 0, "ЗАПРАВКА", new Color(1, .8f, .32f), 1.8f, .4f);
            s.Box("Roof south", new Vector3(-3, 6.25f, 19.1f), new Vector3(16, 0.35f, 4.9f), Quaternion.Euler(-32, 0, 0), new Color(0.86f, 0.46f, 0.24f), root);
            s.Box("Roof north", new Vector3(-3, 6.25f, 22.9f), new Vector3(16, 0.35f, 4.9f), Quaternion.Euler(32, 0, 0), new Color(0.80f, 0.42f, 0.22f), root);
            var glass = new Color(0.26f, 0.36f, 0.46f);
            foreach (var w in new[] { new Vector2(-8, 1.6f), new Vector2(2, 1.6f), new Vector2(-8, 4f), new Vector2(-3, 4f), new Vector2(2, 4f) })
            {
                s.Box("Window frame", new Vector3(w.x, w.y, 17.06f), new Vector3(1.7f, 1.4f, 0.1f), new Color(0.93f, 0.91f, 0.86f), root);
                s.Box("Window", new Vector3(w.x, w.y, 17.02f), new Vector3(1.4f, 1.1f, 0.1f), glass, root);
            }
            s.Box("Garage", new Vector3(9.5f, 2f, 20.5f), new Vector3(7, 4, 7), new Color(0.66f, 0.50f, 0.42f), root, true);
            s.Box("Garage roof", new Vector3(9.5f, 4.1f, 20.3f), new Vector3(7.6f, 0.25f, 7.8f), new Color(0.42f, 0.30f, 0.26f), root);
            s.Box("Garage door", new Vector3(9.5f, 1.2f, 16.97f), new Vector3(3.6f, 2.4f, 0.1f), new Color(0.72f, 0.56f, 0.42f), root);

            // Patio table with a striped umbrella.
            s.Box("Table", new Vector3(-9, 0.75f, 15.3f), new Vector3(1.4f, 0.08f, 1.4f), new Color(0.85f, 0.83f, 0.78f), root, true);
            s.Box("Umbrella pole", new Vector3(-9, 1.3f, 15.3f), new Vector3(0.06f, 2.6f, 0.06f), Color.white, root);
            s.Ball("Umbrella", new Vector3(-9, 2.5f, 15.3f), new Vector3(3, 0.5f, 3), new Color(0.95f, 0.55f, 0.25f), root);
            for (int i = 0; i < 4; i++)
                s.Box("Chair", new Vector3(-9 + (i % 2 == 0 ? (i - 1) * 1.1f : 0), 0.45f, 15.3f + (i % 2 == 1 ? (i - 2) * 1.1f : 0)), new Vector3(0.5f, 0.9f, 0.5f), Wood, root);

            // Fence, hedges and trees keep the player inside the yard.
            for (int side = -1; side <= 1; side += 2)
            {
                s.Box("Fence", new Vector3(side * 18, 0.8f, 0.65f), new Vector3(0.14f, 1.6f, 33.3f), Wood, root, true);
                s.Box("Hedge", new Vector3(side * 17.2f, 0.45f, 2), new Vector3(1, 0.9f, 20), Hedge, root);
            }
            s.Box("Fence south", new Vector3(0, 0.8f, -16), new Vector3(36, 1.6f, 0.14f), Wood, root, true);
            // Invisible fence behind the patio, with the house and garage closing the rest.
            foreach (var span in new[] { new Vector2(-18, -10.5f), new Vector2(4.5f, 6), new Vector2(13, 18) })
                s.Box("Fence north", new Vector3((span.x + span.y) / 2, 0.8f, 17.3f), new Vector3(span.y - span.x, 1.6f, 0.14f), Wood, root, true)
                    .GetComponent<Renderer>().enabled = false;
            for (int i = -17; i <= 17; i += 2)
                s.Box("Fence post", new Vector3(i, 0.9f, -15.9f), new Vector3(0.18f, 1.8f, 0.18f), DarkWood, root);
            Tree(s, root, new Vector3(-14, 0, 10), new Color(0.93f, 0.48f, 0.18f));
            Tree(s, root, new Vector3(14.5f, 0, -12), new Color(0.88f, 0.36f, 0.16f));
            Tree(s, root, new Vector3(-14.5f, 0, -12.5f), new Color(0.40f, 0.62f, 0.24f));
            Tree(s, root, new Vector3(15, 0, 9), new Color(0.95f, 0.66f, 0.22f));

            // The sealed door waits in a chamber on the bedrock floor.
            var door = new Vector3(0, config.FloorY + 1.6f, 1.9f);
            DoorPoint = door;
            var stone = new Color(0.22f, 0.20f, 0.28f);
            s.Box("Door frame left", door + new Vector3(-1.35f, 0, 0.1f), new Vector3(0.5f, 3.4f, 0.6f), stone, root, true);
            s.Box("Door frame right", door + new Vector3(1.35f, 0, 0.1f), new Vector3(0.5f, 3.4f, 0.6f), stone, root, true);
            s.Box("Door lintel", door + new Vector3(0, 1.85f, 0.1f), new Vector3(3.2f, 0.5f, 0.6f), stone, root, true);
            s.Box("Sealed door", door, new Vector3(2.2f, 3.2f, 0.3f), new Color(0.30f, 0.27f, 0.36f), root, true);
            s.Box("Keyhole", door + new Vector3(0, -0.2f, -0.17f), new Vector3(0.14f, 0.34f, 0.05f), new Color(0.05f, 0.04f, 0.07f), root);
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 2 / 5;
                var rune = s.Box("Rune", door + new Vector3(Mathf.Cos(angle) * 0.75f, Mathf.Sin(angle) * 0.75f + 0.3f, -0.17f), new Vector3(0.24f, 0.24f, 0.04f), Quaternion.Euler(0, 0, i * 72), Expedition.Keys[i].Color, root, false, 0.9f);
                Runes.Add(rune.GetComponent<Renderer>());
            }
            DoorLight = new GameObject("Door glow", typeof(Light)).GetComponent<Light>();
            DoorLight.transform.position = door + new Vector3(0, 0.4f, -1.2f);
            DoorLight.type = LightType.Point;
            DoorLight.color = new Color(0.4f, 1f, 0.9f);
            DoorLight.range = 7;
            DoorLight.intensity = 1.6f;
            DressYard(s, root, half);
            CombineScenery(root);
            BuildClouds(s);
        }

        private void DressYard(Shapes s, Transform root, float half)
        {
            var iron = new Color(.18f,.27f,.27f);
            var paleWood = new Color(.76f,.59f,.36f);
            var stone = new Color(.48f,.53f,.47f);
            // A timber gantry frames the far side of the existing digging patch.
            float z = half + .8f;
            for(int side=-1;side<=1;side+=2)
            {
                s.Box("Mine timber",new Vector3(side*2.7f,1.55f,z),new Vector3(.26f,3.1f,.3f),DarkWood,root,true);
                s.Box("Timber cap",new Vector3(side*2.7f,.32f,z),new Vector3(.34f,.42f,.38f),iron,root);
                s.Box("Diagonal brace",new Vector3(side*2.2f,2.65f,z),new Vector3(.16f,1.45f,.18f),Quaternion.Euler(0,0,side*48),Wood,root);
                s.Box("Lantern stem",new Vector3(side*2.4f,2.26f,z-.26f),new Vector3(.04f,.36f,.04f),iron,root);
                s.Box("Lantern frame",new Vector3(side*2.4f,2.02f,z-.26f),new Vector3(.27f,.36f,.27f),iron,root);
                s.Box("Lantern glass",new Vector3(side*2.4f,2.02f,z-.28f),new Vector3(.21f,.24f,.25f),new Color(1,.74f,.32f),root,false,.5f);
            }
            // Low plank frame around the patch, like a garden bed: outside the diggable ground, easy to step over.
            for (int side = -1; side <= 1; side += 2)
            {
                s.Box("Patch frame", new Vector3(side * (half + 0.22f), 0.05f, 0), new Vector3(0.26f, 0.1f, half * 2 + 0.7f), paleWood, root);
                s.Box("Patch frame", new Vector3(0, 0.05f, side * (half + 0.22f)), new Vector3(half * 2 + 0.18f, 0.1f, 0.26f), paleWood, root);
            }
            s.Box("Mine crossbeam",new Vector3(0,3.05f,z),new Vector3(6.2f,.32f,.4f),paleWood,root);
            s.Box("Mine sign",new Vector3(0,2.5f,z-.18f),new Vector3(2.9f,.65f,.13f),iron,root);
            WorldSign(root,new Vector3(0,2.5f,z-.27f),0,"ШАХТА  /  120 м",new Color(1,.81f,.45f),2.6f,.48f);
            // Stepping stones lead from the patio to the excavation without covering the patch.
            for(int i=0;i<6;i++)
                s.Box("Path slab",new Vector3((i%2==0?-.18f:.18f),.025f,half+1.1f+i*(6f-half*.15f)/6),new Vector3(1.7f,.05f,.64f),Quaternion.Euler(0,(i%3-1)*5,0),stone,root);
            // Window mullions and shutters make the existing house read as a building at a distance.
            foreach(var w in new[]{new Vector2(-8,1.6f),new Vector2(2,1.6f),new Vector2(-8,4),new Vector2(-3,4),new Vector2(2,4)})
            {
                s.Box("Window mullion",new Vector3(w.x,w.y,16.94f),new Vector3(.07f,1.12f,.08f),new Color(.95f,.88f,.70f),root);
                s.Box("Window transom",new Vector3(w.x,w.y,16.94f),new Vector3(1.4f,.07f,.08f),new Color(.95f,.88f,.70f),root);
                for(int side=-1;side<=1;side+=2)
                    s.Box("Shutter",new Vector3(w.x+side*1.03f,w.y,17.04f),new Vector3(.35f,1.5f,.12f),new Color(.23f,.39f,.35f),root);
            }
            s.Box("Chimney",new Vector3(-7,6.2f,21.5f),new Vector3(.85f,2,.8f),new Color(.48f,.30f,.24f),root);
            s.Box("Chimney cap",new Vector3(-7,7.24f,21.5f),new Vector3(1.05f,.18f,1),DarkWood,root);
            WorldSign(root, new Vector3(-3, 2.72f, 16.98f), 0, "ДОМ  ·  СКУПКА И МАСТЕРСКАЯ", new Color(1, .81f, .45f), 3.4f, .42f);
            // Deterministic foliage outside the excavation; its own random stream never touches loot or saves.
            var random = new System.Random(41);
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
            bool Free(float x, float z) =>
                !(Mathf.Abs(x) < half + 0.7f && Mathf.Abs(z) < half + 0.7f) && !(Mathf.Abs(x) < 1.3f && z > half) &&
                !(z > 13.6f && x > -14.5f && x < 12.5f) && Mathf.Abs(x) < 17.4f && z > -15.6f && z < 17;
            // Tufts gather along the fence, hedges, trees and the patch border, with a few in the open lawn.
            var anchors = new List<Vector2>();
            for (int i = 0; i < 40; i++) anchors.Add(new Vector2(Range(-17, 17), -15.2f));
            for (int i = 0; i < 24; i++) anchors.Add(new Vector2(Range(-1, 1) * 16.6f, Range(-8, 12)));
            foreach (var tree in new[] { new Vector2(-14, 10), new Vector2(14.5f, -12), new Vector2(-14.5f, -12.5f), new Vector2(15, 9) })
                for (int i = 0; i < 7; i++) anchors.Add(tree + Random2(random, 1.6f));
            for (int i = 0; i < 26; i++) anchors.Add(new Vector2(Range(-1, 1) * (half + 1.1f), Range(-half, half)));
            for (int i = 0; i < 45; i++) anchors.Add(new Vector2(Range(-16, 16), Range(-14, 13)));
            var greens = new[] { new Color(0.46f, 0.62f, 0.29f), new Color(0.53f, 0.68f, 0.32f), new Color(0.38f, 0.54f, 0.24f) };
            foreach (var anchor in anchors)
            {
                if (!Free(anchor.x, anchor.y)) continue;
                var color = greens[random.Next(greens.Length)];
                float height = Range(0.22f, 0.42f), turn = Range(0, 360);
                for (int blade = 0; blade < 3; blade++)
                {
                    float lean = Range(10, 26), yaw = turn + blade * 120;
                    var tilt = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(lean, 0, 0);
                    s.Box("Grass tuft", new Vector3(anchor.x, 0, anchor.y) + tilt * new Vector3(0, height * 0.5f, 0), new Vector3(0.035f, height, 0.012f), tilt, color, root);
                }
            }
            // Flower beds by the patio and the south fence, plus a few by the trees.
            var petals = new[] { new Color(0.93f, 0.33f, 0.30f), new Color(1f, 0.83f, 0.30f), new Color(0.96f, 0.95f, 0.90f), new Color(0.68f, 0.48f, 0.86f) };
            var flowers = new List<Vector2>();
            for (int i = 0; i < 34; i++) flowers.Add(new Vector2(Range(-13.5f, 11.5f), Range(13.0f, 13.5f)));
            for (int i = 0; i < 26; i++) flowers.Add(new Vector2(Range(-16.5f, 16.5f), Range(-15.3f, -14.6f)));
            foreach (var tree in new[] { new Vector2(-14, 10), new Vector2(15, 9) })
                for (int i = 0; i < 6; i++) flowers.Add(tree + Random2(random, 1.3f));
            foreach (var flower in flowers)
            {
                float height = Range(0.2f, 0.34f);
                s.Box("Flower stem", new Vector3(flower.x, height * 0.5f, flower.y), new Vector3(0.018f, height, 0.018f), new Color(0.32f, 0.50f, 0.22f), root);
                s.Ball("Flower", new Vector3(flower.x, height, flower.y), new Vector3(0.1f, 0.06f, 0.1f), petals[random.Next(petals.Length)], root);
            }
            for (int i = 0; i < 10; i++)
            {
                var spot = new Vector2(Range(-16, 16), Range(-14, 13));
                if (Free(spot.x, spot.y)) s.Ball("Pebble", new Vector3(spot.x, 0.04f, spot.y), new Vector3(0.28f, 0.14f, 0.22f), stone, root);
            }
            // Hills form a silhouette beyond the fence; inexpensive silhouettes instead of distant scenery.
            for(int i=0;i<9;i++)
            {
                float angle=i*Mathf.PI*2/9;
                s.Ball("Distant hill",new Vector3(Mathf.Sin(angle)*74,-7,Mathf.Cos(angle)*74),
                    new Vector3(42,27+(i%3)*5,38),new Color(.37f,.52f,.45f),root);
            }
        }

        /// <summary>Hollow house you can walk into: the ore buyer on the left, the workbench on the right.</summary>
        private void BuildHouse(Shapes s, Transform root, Color wall)
        {
            var inner = new Color(0.86f, 0.80f, 0.68f);
            var floor = new Color(0.62f, 0.44f, 0.30f);
            s.Box("House floor", new Vector3(-3, 0.05f, 21), new Vector3(14.6f, 0.1f, 7.4f), floor, root, true);
            s.Box("House ceiling", new Vector3(-3, 3.25f, 21), new Vector3(14.6f, 0.1f, 7.4f), inner, root, true);
            s.Box("House back", new Vector3(-3, 2.6f, 24.8f), new Vector3(15, 5.2f, 0.2f), wall, root, true);
            s.Box("House left", new Vector3(-10.4f, 2.6f, 21), new Vector3(0.2f, 5.2f, 7.8f), wall, root, true);
            s.Box("House right", new Vector3(4.4f, 2.6f, 21), new Vector3(0.2f, 5.2f, 7.8f), wall, root, true);
            // Front wall with a doorway 1.4 m wide and 2.4 m tall.
            s.Box("House front", new Vector3(-7.1f, 2.6f, 17.2f), new Vector3(6.8f, 5.2f, 0.2f), wall, root, true);
            s.Box("House front", new Vector3(1.1f, 2.6f, 17.2f), new Vector3(6.8f, 5.2f, 0.2f), wall, root, true);
            s.Box("Above door", new Vector3(-3, 3.8f, 17.2f), new Vector3(1.4f, 2.8f, 0.2f), wall, root, true);
            s.Box("Upper floor", new Vector3(-3, 4.25f, 21), new Vector3(15, 1.9f, 7.8f), wall, root);
            // Inner wall faces in a lighter plaster, and the open door leaning inside.
            s.Box("Plaster back", new Vector3(-3, 1.65f, 24.68f), new Vector3(14.6f, 3.1f, 0.04f), inner, root);
            s.Box("Plaster left", new Vector3(-10.28f, 1.65f, 21), new Vector3(0.04f, 3.1f, 7.4f), inner, root);
            s.Box("Plaster right", new Vector3(4.28f, 1.65f, 21), new Vector3(0.04f, 3.1f, 7.4f), inner, root);
            s.Box("Door", new Vector3(-3.62f, 1.2f, 17.95f), new Vector3(0.08f, 2.3f, 1.3f), new Color(0.55f, 0.37f, 0.27f), root);
            s.Box("Doormat", new Vector3(-3, 0.1f, 16.6f), new Vector3(1.4f, 0.03f, 0.8f), new Color(0.45f, 0.30f, 0.20f), root);
            foreach (float x in new[] { -8f, 2f })
                s.Box("Window light", new Vector3(x, 1.6f, 17.33f), new Vector3(1.4f, 1.1f, 0.04f), new Color(0.78f, 0.90f, 1f), root, false, 0.6f);
            s.Box("Rug", new Vector3(-3, 0.11f, 20.6f), new Vector3(4.2f, 0.02f, 2.6f), new Color(0.62f, 0.26f, 0.22f), root);
            s.Box("Rug border", new Vector3(-3, 0.105f, 20.6f), new Vector3(4.5f, 0.02f, 2.9f), new Color(0.90f, 0.72f, 0.40f), root);
            // Skirting boards, beams under the ceiling, window frames seen from inside and a picture over the rug.
            s.Box("Baseboard wood", new Vector3(-3, 0.17f, 24.64f), new Vector3(14.6f, 0.14f, 0.04f), DarkWood, root);
            foreach (float x in new[] { -10.24f, 4.24f })
                s.Box("Baseboard wood", new Vector3(x, 0.17f, 21), new Vector3(0.04f, 0.14f, 7.4f), DarkWood, root);
            foreach (float x in new[] { -7.1f, 1.1f })
                s.Box("Baseboard wood", new Vector3(x, 0.17f, 17.34f), new Vector3(6.8f, 0.14f, 0.04f), DarkWood, root);
            for (int i = 0; i < 4; i++)
                s.Box("Overhead beam", new Vector3(-8.6f + i * 3.8f, 3.1f, 21), new Vector3(0.22f, 0.2f, 7.4f), DarkWood, root);
            foreach (float x in new[] { -8f, 2f })
            {
                var frame = new Color(0.93f, 0.91f, 0.86f);
                foreach (float y in new[] { 1.02f, 2.18f })
                    s.Box("Window frame", new Vector3(x, y, 17.3f), new Vector3(1.6f, 0.1f, 0.08f), frame, root);
                foreach (float dx in new[] { -0.75f, 0.75f })
                    s.Box("Window frame", new Vector3(x + dx, 1.6f, 17.3f), new Vector3(0.1f, 1.26f, 0.08f), frame, root);
                s.Box("Window sill wood", new Vector3(x, 0.99f, 17.4f), new Vector3(1.75f, 0.05f, 0.22f), DarkWood, root);
            }
            var picture = new Vector3(-3, 2.05f, 24.62f);
            s.Box("Picture frame wood", picture, new Vector3(1.5f, 0.95f, 0.05f), DarkWood, root);
            s.Box("Picture sky", picture + new Vector3(0, 0.12f, -0.03f), new Vector3(1.3f, 0.55f, 0.02f), new Color(0.55f, 0.75f, 0.9f), root);
            s.Box("Picture field", picture + new Vector3(0, -0.24f, -0.03f), new Vector3(1.3f, 0.3f, 0.02f), new Color(0.38f, 0.6f, 0.3f), root);
            s.Box("Picture hole", picture + new Vector3(0.25f, -0.24f, -0.04f), new Vector3(0.3f, 0.1f, 0.02f), new Color(0.35f, 0.22f, 0.14f), root);
            s.Box("Picture sun", picture + new Vector3(-0.4f, 0.25f, -0.04f), new Vector3(0.14f, 0.14f, 0.02f), new Color(1f, 0.85f, 0.35f), root, false, 0.4f);

            // Ore buyer: counter, scales, crates of ore and a sign.
            var wood = new Color(0.55f, 0.36f, 0.22f);
            s.Box("Buyer counter", new Vector3(-7.6f, 0.6f, 23.6f), new Vector3(3.2f, 1f, 0.9f), wood, root, true);
            s.Box("Counter top", new Vector3(-7.6f, 1.13f, 23.6f), new Vector3(3.4f, 0.07f, 1f), DarkWood, root);
            s.Box("Scales", new Vector3(-7f, 1.26f, 23.5f), new Vector3(0.5f, 0.2f, 0.35f), new Color(0.75f, 0.70f, 0.55f), root);
            s.Box("Scale pan", new Vector3(-7f, 1.4f, 23.5f), new Vector3(0.42f, 0.03f, 0.42f), new Color(0.95f, 0.80f, 0.40f), root, false, 0.15f);
            var crateOre = new[] { new Color(0.90f, 0.52f, 0.28f), new Color(0.86f, 0.92f, 0.98f), new Color(1f, 0.78f, 0.20f) };
            for (int i = 0; i < 3; i++)
            {
                var crate = new Vector3(-9.4f + i * 0.95f, 0.35f, 24.25f);
                s.Box("Ore crate", crate, new Vector3(0.85f, 0.7f, 0.6f), DarkWood, root);
                for (int k = 0; k < 4; k++)
                    s.Box("Crate ore", crate + new Vector3((k % 2 - 0.5f) * 0.35f, 0.38f, (k / 2 - 0.5f) * 0.25f), Vector3.one * 0.2f,
                        Quaternion.Euler(k * 20, k * 35, 10), crateOre[i], root, false, 0.2f);
            }
            WorldSign(root, new Vector3(-7.6f, 2.35f, 24.6f), 0, "СКУПКА РУДЫ", new Color(1, .81f, .45f), 3f, .5f);

            // Workbench with a pegboard of tools and a jetpack on display.
            s.Box("Workbench", new Vector3(1.8f, 0.5f, 23.7f), new Vector3(3f, 0.9f, 1f), wood, root, true);
            s.Box("Bench top", new Vector3(1.8f, 0.98f, 23.7f), new Vector3(3.2f, 0.08f, 1.1f), new Color(0.70f, 0.52f, 0.32f), root);
            s.Box("Vise", new Vector3(0.6f, 1.12f, 23.5f), new Vector3(0.25f, 0.2f, 0.3f), new Color(0.30f, 0.34f, 0.38f), root);
            s.Box("Pegboard", new Vector3(1.8f, 1.9f, 24.64f), new Vector3(2.8f, 1.2f, 0.04f), new Color(0.78f, 0.66f, 0.48f), root);
            for (int i = 0; i < 4; i++)
                s.Box("Hanging tool", new Vector3(0.8f + i * 0.65f, 1.9f, 24.6f), new Vector3(0.08f, 0.8f, 0.04f), Quaternion.Euler(0, 0, (i - 1.5f) * 8), new Color(0.40f, 0.44f, 0.48f), root);
            var pack = new Vector3(2.6f, 1.45f, 23.8f);
            for (int side = -1; side <= 1; side += 2)
            {
                s.Box("Jetpack tank", pack + new Vector3(side * 0.15f, 0, 0), new Vector3(0.22f, 0.7f, 0.22f), new Color(0.85f, 0.35f, 0.25f), root);
                s.Box("Jetpack nozzle", pack + new Vector3(side * 0.15f, -0.42f, 0), new Vector3(0.14f, 0.14f, 0.14f), new Color(0.30f, 0.30f, 0.34f), root);
            }
            s.Box("Jetpack frame", pack + new Vector3(0, 0.1f, 0.14f), new Vector3(0.5f, 0.5f, 0.06f), new Color(0.30f, 0.30f, 0.34f), root);
            foreach (float y in new[] { -0.15f, 0.25f })
                s.Box("Jetpack belt", pack + new Vector3(0, y, -0.1f), new Vector3(0.56f, 0.06f, 0.05f), new Color(0.36f, 0.24f, 0.16f), root);
            WorldSign(root, new Vector3(1.8f, 2.75f, 24.6f), 0, "МАСТЕРСКАЯ", new Color(.43f, .86f, .72f), 3f, .45f);

            // Bed, shelves and lamps make it a home to come back to.
            s.Box("Bed", new Vector3(-9.3f, 0.35f, 19.3f), new Vector3(1.5f, 0.5f, 2.3f), wood, root, true);
            s.Box("Bed headboard wood", new Vector3(-9.3f, 0.75f, 20.47f), new Vector3(1.6f, 1.1f, 0.08f), DarkWood, root);
            s.Box("Blanket", new Vector3(-9.3f, 0.64f, 19.0f), new Vector3(1.45f, 0.1f, 1.7f), new Color(0.25f, 0.45f, 0.42f), root);
            s.Box("Pillow", new Vector3(-9.3f, 0.68f, 20.1f), new Vector3(1.1f, 0.14f, 0.45f), new Color(0.95f, 0.93f, 0.86f), root);
            var jars = new[] { new Color(0.75f, 0.85f, 0.95f), new Color(0.90f, 0.70f, 0.40f), new Color(0.60f, 0.80f, 0.55f) };
            for (int i = 0; i < 3; i++)
            {
                s.Box("Shelf", new Vector3(4.1f, 0.8f + i * 0.6f, 19.8f), new Vector3(0.35f, 0.05f, 2f), DarkWood, root);
                for (int k = 0; k < 4; k++)
                    s.Box("Jar", new Vector3(4.1f, 0.93f + i * 0.6f, 19.1f + k * 0.45f), new Vector3(0.14f, 0.22f, 0.14f), jars[(i + k) % 3], root);
            }
            s.Box("Ceiling lamp", new Vector3(-3, 3.12f, 21), new Vector3(0.6f, 0.12f, 0.6f), new Color(1f, 0.90f, 0.65f), root, false, 0.9f);
            foreach (var at in new[] { new Vector3(-3, 2.75f, 21), new Vector3(-7.6f, 2.4f, 22.8f), new Vector3(1.8f, 2.4f, 22.8f) })
            {
                var light = new GameObject("House light", typeof(Light)).GetComponent<Light>();
                light.transform.position = at;
                light.type = LightType.Point;
                light.color = new Color(1f, 0.86f, 0.62f);
                light.range = at.x == -3 ? 10 : 5;
                light.intensity = at.x == -3 ? 2.2f : 1.3f;
                light.shadows = LightShadows.None;
            }
        }

        private static Vector2 Random2(System.Random random, float radius)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2, distance = Mathf.Sqrt((float)random.NextDouble()) * radius;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        }

        private void BuildClouds(Shapes s)
        {
            // Slow puffy clouds; kept out of the combined scenery because they move.
            var random = new System.Random(7);
            clouds = new GameObject("Clouds").transform;
            for (int i = 0; i < 8; i++)
            {
                var cloud = new GameObject("Cloud").transform;
                cloud.SetParent(clouds, false);
                float angle = i * Mathf.PI * 2 / 8 + (float)random.NextDouble() * 0.5f;
                cloud.localPosition = new Vector3(Mathf.Sin(angle) * (70 + i % 3 * 14), 38 + (float)random.NextDouble() * 12, Mathf.Cos(angle) * (70 + i % 3 * 14));
                int puffs = 3 + random.Next(3);
                for (int p = 0; p < puffs; p++)
                {
                    float size = 7 + (float)random.NextDouble() * 6;
                    var at = new Vector3((p - puffs / 2f) * 5.5f, (float)random.NextDouble() * 2.5f, (float)random.NextDouble() * 4 - 2);
                    s.Ball("Puff", at, new Vector3(size * 1.3f, size * 0.62f, size), new Color(0.97f, 0.97f, 0.95f), cloud, false, 0.45f)
                        .GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }

        /// <summary>Drifts the clouds across the sky and wraps them around.</summary>
        public void Animate(float deltaTime)
        {
            foreach (Transform cloud in clouds)
            {
                var position = cloud.localPosition + Vector3.right * 0.8f * deltaTime;
                if (position.x > 110) position.x -= 220;
                cloud.localPosition = position;
            }
        }

        private static void WorldSign(Transform parent, Vector3 at, float yaw, string text, Color color, float width, float height)
        {
            var obj=new GameObject("Sign "+text,typeof(RectTransform),typeof(Canvas));
            obj.transform.SetParent(parent,false);obj.transform.position=at;obj.transform.rotation=Quaternion.Euler(0,yaw,0);
            obj.transform.localScale=Vector3.one*.005f;
            var rect=(RectTransform)obj.transform;rect.sizeDelta=new Vector2(width/.005f,height/.005f);
            obj.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            var labelObj=new GameObject("Text",typeof(RectTransform),typeof(UnityEngine.UI.Text));
            labelObj.transform.SetParent(obj.transform,false);
            var label=labelObj.GetComponent<UnityEngine.UI.Text>();
            var lr=label.rectTransform;lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;lr.offsetMin=lr.offsetMax=Vector2.zero;
            label.font=Resources.Load<Font>("Fonts/NotoSans");label.fontSize=60;label.fontStyle=FontStyle.Bold;
            label.text=text;label.color=color;label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;
            label.resizeTextForBestFit=true;label.resizeTextMinSize=12;label.resizeTextMaxSize=60;
        }

        private void CombineScenery(Transform root)
        {
            // Static decorative primitives share one mesh per material. Keep collider objects and animated runes.
            var groups=new Dictionary<Material,List<CombineInstance>>();
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                if(!renderer.enabled||Runes.Contains(renderer))continue;
                var filter=renderer.GetComponent<MeshFilter>();
                if(filter==null)continue;
                var material=renderer.sharedMaterial;
                if(!groups.TryGetValue(material,out var list)){list=new List<CombineInstance>();groups.Add(material,list);}
                list.Add(new CombineInstance{mesh=filter.sharedMesh,transform=root.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                renderer.enabled=false;
            }
            foreach(var group in groups)
            {
                var mesh=new Mesh{name="Yard scenery",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
                mesh.CombineMeshes(group.Value.ToArray(),true,true);combinedMeshes.Add(mesh);
                var obj=new GameObject("Scenery "+group.Key.name,typeof(MeshFilter),typeof(MeshRenderer));
                obj.transform.SetParent(root,false);obj.GetComponent<MeshFilter>().sharedMesh=mesh;
                obj.GetComponent<MeshRenderer>().sharedMaterial=group.Key;
            }
        }

        public void SetSeals(int keys)
        {
            if (shownSeals == keys) return;
            shownSeals = keys;
            for (int i = 0; i < Runes.Count; i++)
            {
                bool found = (keys & 1 << i) != 0;
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", found ? Expedition.Keys[i].Color : new Color(.16f, .19f, .2f));
                block.SetColor("_Emission", found ? Expedition.Keys[i].Color * .9f : Color.black);
                Runes[i].SetPropertyBlock(block);
            }
        }

        public void Dispose(){foreach(var mesh in combinedMeshes)Object.Destroy(mesh);}

        private static void Tree(Shapes s, Transform root, Vector3 at, Color leaves)
        {
            s.Box("Trunk", at + new Vector3(0, 1.6f, 0), new Vector3(0.45f, 3.2f, 0.45f), new Color(0.42f, 0.28f, 0.18f), root, true);
            s.Ball("Crown", at + new Vector3(0, 3.8f, 0), new Vector3(3.6f, 3f, 3.6f), leaves, root);
            s.Ball("Crown", at + new Vector3(0.9f, 3.2f, 0.6f), new Vector3(2.4f, 2.1f, 2.4f), leaves * 0.92f, root);
            s.Ball("Crown", at + new Vector3(-0.8f, 3.4f, -0.7f), new Vector3(2.2f, 2f, 2.2f), leaves * 1.05f, root);
        }
    }
}
