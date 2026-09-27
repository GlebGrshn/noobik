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
        public Vector3 ShopPoint { get; private set; }
        public Vector3 DoorPoint { get; private set; }
        public readonly List<Renderer> Runes = new List<Renderer>();
        public Light DoorLight { get; private set; }

        // Same as the terrain shader's grass so the diggable patch blends into the lawn.
        private static readonly Color Lawn = new Color(0.34f, 0.60f, 0.22f);
        private static readonly Color Wood = new Color(0.60f, 0.40f, 0.25f);
        private static readonly Color DarkWood = new Color(0.40f, 0.26f, 0.17f);
        private static readonly Color Hedge = new Color(0.27f, 0.50f, 0.20f);

        public Yard(Shapes s, MineConfig config)
        {
            var root = new GameObject("Yard").transform;
            float half = config.width / 2f, lip = 0.35f, top = -0.005f;
            // Lawn around the diggable patch. Terrain starts at the centre of the outer cells (0.25 m in),
            // so the lawn reaches a little further to hide the seam.
            void Lawn(float x0, float x1, float z0, float z1) =>
                s.Box("Lawn", new Vector3((x0 + x1) / 2, top - 0.15f, (z0 + z1) / 2), new Vector3(x1 - x0, 0.3f, z1 - z0), Yard.Lawn, root, true);
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
            s.Box("House", new Vector3(-3, 2.6f, 21), new Vector3(15, 5.2f, 7.8f), wall, root, true);
            s.Box("Roof south", new Vector3(-3, 6.25f, 19.1f), new Vector3(16, 0.35f, 4.9f), Quaternion.Euler(-32, 0, 0), new Color(0.86f, 0.46f, 0.24f), root);
            s.Box("Roof north", new Vector3(-3, 6.25f, 22.9f), new Vector3(16, 0.35f, 4.9f), Quaternion.Euler(32, 0, 0), new Color(0.80f, 0.42f, 0.22f), root);
            s.Box("Door", new Vector3(-3, 1.15f, 17.08f), new Vector3(1.3f, 2.3f, 0.12f), new Color(0.55f, 0.37f, 0.27f), root);
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
            s.Box("Fence north", new Vector3(0, 0.8f, 17.3f), new Vector3(36, 1.6f, 0.14f), Wood, root, true).GetComponent<Renderer>().enabled = false;
            for (int i = -17; i <= 17; i += 2)
                s.Box("Fence post", new Vector3(i, 0.9f, -15.9f), new Vector3(0.18f, 1.8f, 0.18f), DarkWood, root);
            Tree(s, root, new Vector3(-14, 0, 10), new Color(0.93f, 0.48f, 0.18f));
            Tree(s, root, new Vector3(14.5f, 0, -12), new Color(0.88f, 0.36f, 0.16f));
            Tree(s, root, new Vector3(-14.5f, 0, -12.5f), new Color(0.40f, 0.62f, 0.24f));
            Tree(s, root, new Vector3(15, 0, 9), new Color(0.95f, 0.66f, 0.22f));

            // Shop stall and the sell crate by the south-west corner.
            var shop = new Vector3(-10.5f, 0, -9.5f);
            ShopPoint = shop + new Vector3(1.6f, 0, 0.3f);
            s.Box("Counter", shop + new Vector3(0, 0.55f, 0), new Vector3(1.1f, 1.1f, 2.6f), Wood, root, true);
            s.Box("Counter top", shop + new Vector3(0, 1.12f, 0), new Vector3(1.3f, 0.08f, 2.8f), DarkWood, root);
            for (int z = -1; z <= 1; z += 2)
                s.Box("Stall post", shop + new Vector3(-0.4f, 1.4f, z * 1.3f), new Vector3(0.12f, 2.8f, 0.12f), DarkWood, root);
            for (int i = 0; i < 6; i++)
                s.Box("Awning", shop + new Vector3(0.1f, 2.75f, -1.45f + i * 0.58f), new Vector3(1.8f, 0.08f, 0.58f), Quaternion.Euler(0, 0, -14), i % 2 == 0 ? new Color(0.95f, 0.50f, 0.20f) : new Color(0.98f, 0.95f, 0.88f), root);
            var crate = shop + new Vector3(1.4f, 0.4f, 2.1f);
            s.Box("Sell crate", crate, new Vector3(0.9f, 0.8f, 0.9f), Wood, root, true);
            for (int i = 0; i < 5; i++)
                s.Box("Coins", crate + new Vector3((i % 3 - 1) * 0.2f, 0.42f, (i / 3 - 0.5f) * 0.25f), new Vector3(0.16f, 0.06f, 0.16f), new Color(1f, 0.76f, 0.2f), root, false, 0.2f);

            // The sealed door waits in a chamber on the bedrock floor.
            var door = new Vector3(0, config.FloorY + 1.6f, 1.9f);
            DoorPoint = door;
            var stone = new Color(0.22f, 0.20f, 0.28f);
            s.Box("Door frame left", door + new Vector3(-1.35f, 0, 0.1f), new Vector3(0.5f, 3.4f, 0.6f), stone, root, true);
            s.Box("Door frame right", door + new Vector3(1.35f, 0, 0.1f), new Vector3(0.5f, 3.4f, 0.6f), stone, root, true);
            s.Box("Door lintel", door + new Vector3(0, 1.85f, 0.1f), new Vector3(3.2f, 0.5f, 0.6f), stone, root, true);
            s.Box("Sealed door", door, new Vector3(2.2f, 3.2f, 0.3f), new Color(0.30f, 0.27f, 0.36f), root, true);
            s.Box("Keyhole", door + new Vector3(0, -0.2f, -0.17f), new Vector3(0.14f, 0.34f, 0.05f), new Color(0.05f, 0.04f, 0.07f), root);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                var rune = s.Box("Rune", door + new Vector3(Mathf.Cos(angle) * 0.75f, Mathf.Sin(angle) * 0.75f + 0.3f, -0.17f), new Vector3(0.16f, 0.16f, 0.04f), Quaternion.Euler(0, 0, i * 45), new Color(0.3f, 1f, 0.85f), root, false, 0.9f);
                Runes.Add(rune.GetComponent<Renderer>());
            }
            DoorLight = new GameObject("Door glow", typeof(Light)).GetComponent<Light>();
            DoorLight.transform.position = door + new Vector3(0, 0.4f, -1.2f);
            DoorLight.type = LightType.Point;
            DoorLight.color = new Color(0.4f, 1f, 0.9f);
            DoorLight.range = 7;
            DoorLight.intensity = 1.6f;
        }

        private static void Tree(Shapes s, Transform root, Vector3 at, Color leaves)
        {
            s.Box("Trunk", at + new Vector3(0, 1.6f, 0), new Vector3(0.45f, 3.2f, 0.45f), new Color(0.42f, 0.28f, 0.18f), root, true);
            s.Ball("Crown", at + new Vector3(0, 3.8f, 0), new Vector3(3.6f, 3f, 3.6f), leaves, root);
            s.Ball("Crown", at + new Vector3(0.9f, 3.2f, 0.6f), new Vector3(2.4f, 2.1f, 2.4f), leaves * 0.92f, root);
            s.Ball("Crown", at + new Vector3(-0.8f, 3.4f, -0.7f), new Vector3(2.2f, 2f, 2.2f), leaves * 1.05f, root);
        }
    }
}
