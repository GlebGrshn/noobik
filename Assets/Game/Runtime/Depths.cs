using UnityEngine;

namespace Nubik
{
    /// <summary>A pocket of lava: its carved hollow and the molten surface lying in it.</summary>
    public readonly struct LavaPool
    {
        public readonly Vector3 Center, Half;
        public LavaPool(Vector3 center, Vector3 half) { Center = center; Half = half; }
        public float Floor => Center.y - Half.y;
        /// <summary>Molten surface height; the player burns with feet below it.</summary>
        public float Surface => Floor + 0.4f;
        public bool Burns(Vector3 feet) =>
            Mathf.Abs(feet.x - Center.x) < Half.x - 0.1f && Mathf.Abs(feet.z - Center.z) < Half.z - 0.1f && feet.y < Surface + 0.12f && feet.y > Floor - 0.6f;
    }

    /// <summary>
    /// Fixed features of the magma zone near the bottom: lava pools and the meteorite. Their hollows are carved after
    /// loading like the other rooms, so old saves get them too; generated ore never floats inside them.
    /// </summary>
    public static class Depths
    {
        public static readonly LavaPool[] Lava =
        {
            new LavaPool(new Vector3(-3.8f, -103.2f, -3.6f), new Vector3(1.7f, 1f, 1.5f)),
            new LavaPool(new Vector3(3.9f, -110.5f, -3.2f), new Vector3(1.6f, 1f, 1.7f)),
            new LavaPool(new Vector3(-3.4f, -117.1f, -1f), new Vector3(1.8f, 0.9f, 1.4f)),
        };

        /// <summary>The meteorite rests half-sunk in the floor of its crater.</summary>
        public static readonly Vector3 Meteor = new Vector3(3.8f, -115.3f, -1f);
        public const float MeteorRadius = 1.1f;
        public static readonly Vector3 CraterCenter = new Vector3(3.8f, -114.6f, -1f), CraterHalf = new Vector3(1.8f, 1.5f, 1.8f);
        /// <summary>Star metal pieces inside the meteorite: special find IDs, hidden until it is blown apart.</summary>
        public const int FirstFragment = 19, Fragments = 3, StarMetal = 12;

        public static Vector3 Fragment(int i) => Meteor + new Vector3(Mathf.Cos(i * 2.1f) * .35f, .05f + i * .12f, Mathf.Sin(i * 2.1f) * .35f);

        public static void Prepare(VoxelTerrain terrain)
        {
            // Clearance above the liquid for the capsule, a jump and the jetpack; the floor stays in place.
            foreach (var pool in Lava) terrain.CarveRoom(pool.Center + Vector3.up * .6f, pool.Half + Vector3.up * .6f);
            terrain.CarveRoom(CraterCenter, CraterHalf);
        }

        /// <summary>Moves a generated find out of a lava pool or the crater into the nearest side wall.</summary>
        public static Vector3 Embed(Vector3 position)
        {
            foreach (var pool in Lava) position = Push(position, pool.Center, pool.Half);
            return Push(position, CraterCenter, CraterHalf);
        }

        private static Vector3 Push(Vector3 position, Vector3 center, Vector3 half)
        {
            var p = position - center;
            var reach = half + Vector3.one * 0.3f;
            if (Mathf.Abs(p.x) > reach.x || Mathf.Abs(p.y) > reach.y || Mathf.Abs(p.z) > reach.z) return position;
            position.x = center.x + (p.x < 0 ? -1 : 1) * (half.x + 0.3f);
            return position;
        }
    }
}
