using System;
using UnityEngine;

namespace Nubik
{
    [Serializable]
    public sealed class RockDef
    {
        public string nameRu, nameEn;
        [Tooltip("Hits of a damage-1 tool needed to clear one voxel.")] public int hardness = 2;
        [Tooltip("Coins per cleared voxel, in hundredths.")] public int value = 25;
        public Color color = Color.gray;
    }

    [Serializable]
    public sealed class ZoneDef
    {
        public string nameRu, nameEn;
        [Tooltip("Depth in meters where the zone begins.")] public int startDepth;
        public RockDef rock = new RockDef(), vein = new RockDef();
        [Range(0, 1)] public float veinShare = 0.18f;
        [Tooltip("Cubic meters of ground per hidden find.")] public float volumePerFind = 10;
        public int findMin = 12, findMax = 25;
        public int chestMin = 35, chestMax = 50;
        public int[] chestDepths = new int[0];
        public Color findColor = Color.yellow, fog = Color.black, ambient = Color.gray, sun = Color.white;
        public float sunIntensity = 1f, lamp, fogEnd = 40;
    }

    [Serializable]
    public sealed class ToolDef
    {
        public string nameRu, nameEn;
        public int damage = 1, price;
        [Tooltip("Dig sphere radius in meters.")] public float radius = 1f;
        public Color color = Color.gray;
    }

    [Serializable]
    public sealed class CollectibleDef
    {
        public string nameRu, nameEn;
        public int depth;
        [Tooltip("Center of the placement area on the yard patch, meters.")] public Vector2 spot;
        [Tooltip("Seeded offset from the spot, meters; 0 keeps it exact.")] public float spread = 2f;
        public Color color = Color.cyan;
    }

    [CreateAssetMenu(menuName = "Nubik/Mine balance")]
    public sealed class MineConfig : ScriptableObject
    {
        public int seed = 28092026;
        [Tooltip("Depth of the sealed door, meters.")] public int depth = 120;
        [Tooltip("Width and length of the diggable yard patch, meters.")] public int width = 12;
        public float voxel = 0.5f;
        [Tooltip("Voxels per chunk edge.")] public int chunk = 12;
        public float hitInterval = 0.42f;
        public float reach = 3.2f;
        public float moveSpeed = 4.5f;
        public float jumpSpeed = 5.2f;
        public int firstDiscoveryCoins = 30;

        public ToolDef[] tools =
        {
            new ToolDef { nameRu = "Обычная лопата", nameEn = "Basic shovel", damage = 1, price = 0, radius = 1f, color = new Color(0.62f, 0.64f, 0.66f) },
            new ToolDef { nameRu = "Медная лопата", nameEn = "Copper shovel", damage = 2, price = 60, radius = 1.05f, color = new Color(0.90f, 0.52f, 0.25f) },
            new ToolDef { nameRu = "Стальная лопата", nameEn = "Steel shovel", damage = 4, price = 220, radius = 1.1f, color = new Color(0.80f, 0.88f, 0.94f) },
            new ToolDef { nameRu = "Кристальная лопата", nameEn = "Crystal shovel", damage = 7, price = 700, radius = 1.2f, color = new Color(0.55f, 0.95f, 1f) },
        };

        public ZoneDef[] zones =
        {
            new ZoneDef
            {
                nameRu = "Земля", nameEn = "Topsoil", startDepth = 0,
                rock = new RockDef { nameRu = "Земля", nameEn = "Dirt", hardness = 2, value = 25, color = new Color(0.55f, 0.34f, 0.20f) },
                vein = new RockDef { nameRu = "Глина", nameEn = "Clay", hardness = 2, value = 35, color = new Color(0.72f, 0.45f, 0.28f) },
                veinShare = 0.2f, volumePerFind = 16, findMin = 12, findMax = 25, chestMin = 35, chestMax = 50, chestDepths = new[] { 12, 24 },
                findColor = new Color(1f, 0.72f, 0.20f), fog = new Color(0.20f, 0.13f, 0.09f),
                ambient = new Color(0.62f, 0.66f, 0.72f), sun = new Color(1f, 0.95f, 0.86f), sunIntensity = 0.55f, lamp = 1.4f, fogEnd = 34,
            },
            new ZoneDef
            {
                nameRu = "Камень", nameEn = "Stone", startDepth = 30,
                rock = new RockDef { nameRu = "Камень", nameEn = "Stone", hardness = 6, value = 50, color = new Color(0.46f, 0.47f, 0.50f) },
                vein = new RockDef { nameRu = "Прочная жила", nameEn = "Hard vein", hardness = 10, value = 90, color = new Color(0.34f, 0.40f, 0.55f) },
                veinShare = 0.16f, volumePerFind = 16, findMin = 20, findMax = 40, chestMin = 45, chestMax = 70, chestDepths = new[] { 38, 56 },
                findColor = new Color(0.86f, 0.92f, 0.98f), fog = new Color(0.07f, 0.08f, 0.11f),
                ambient = new Color(0.36f, 0.39f, 0.46f), sun = new Color(0.8f, 0.88f, 1f), sunIntensity = 0.15f, lamp = 2.2f, fogEnd = 26,
            },
            new ZoneDef
            {
                nameRu = "Пещера", nameEn = "Cavern", startDepth = 70,
                rock = new RockDef { nameRu = "Плотная порода", nameEn = "Dense rock", hardness = 24, value = 90, color = new Color(0.29f, 0.25f, 0.37f) },
                vein = new RockDef { nameRu = "Кристаллическая жила", nameEn = "Crystal vein", hardness = 32, value = 160, color = new Color(0.45f, 0.33f, 0.72f) },
                veinShare = 0.15f, volumePerFind = 14, findMin = 35, findMax = 60, chestMin = 70, chestMax = 100, chestDepths = new[] { 84, 106 },
                findColor = new Color(0.55f, 0.95f, 1f), fog = new Color(0.05f, 0.03f, 0.09f),
                ambient = new Color(0.30f, 0.26f, 0.42f), sun = new Color(0.7f, 0.6f, 1f), sunIntensity = 0f, lamp = 2.8f, fogEnd = 22,
            },
        };

        public CollectibleDef[] collection =
        {
            new CollectibleDef { nameRu = "Старая каска", nameEn = "Old helmet", depth = 3, spot = new Vector2(0.4f, -2.4f), spread = 0, color = new Color(0.08f, 0.61f, 0.59f) },
            new CollectibleDef { nameRu = "Окаменелая ракушка", nameEn = "Fossil shell", depth = 44, color = new Color(0.95f, 0.86f, 0.68f) },
            new CollectibleDef { nameRu = "Светящийся кристалл", nameEn = "Glowing crystal", depth = 78, color = new Color(0.45f, 1f, 0.92f) },
            new CollectibleDef { nameRu = "Фрагмент механизма", nameEn = "Mechanism fragment", depth = 97, color = new Color(0.80f, 0.58f, 0.28f) },
            new CollectibleDef { nameRu = "Загадочный ключ", nameEn = "Mysterious key", depth = 114, color = new Color(1f, 0.84f, 0.30f) },
        };

        // Voxel grid: x/z across the yard patch, y from the bedrock floor up to a few meters of air.
        public int SizeX => Mathf.RoundToInt(width / voxel);
        public int SizeZ => SizeX;
        public int ChunksX => Mathf.CeilToInt(SizeX / (float)chunk);
        public int ChunksZ => ChunksX;
        public int ChunksY => Mathf.CeilToInt((depth + 6) / voxel / chunk);
        public int SizeY => ChunksY * chunk;
        public int ChunkCount => ChunksX * ChunksY * ChunksZ;
        /// <summary>World Y of grid layer 0; ground level is y = 0.</summary>
        public float Bottom => -(depth + 2f);
        public float FloorY => -depth - 0.5f;
        public Vector3 Origin => new Vector3(-width / 2f, Bottom, -width / 2f);
        public Vector3 PointPosition(int x, int y, int z) => Origin + new Vector3(x, y, z) * voxel;

        public int ZoneIndex(float depthMeters)
        {
            int result = 0;
            for (int i = 1; i < zones.Length; i++)
                if (depthMeters >= zones[i].startDepth) result = i;
            return result;
        }
        public ZoneDef Zone(float depthMeters) => zones[ZoneIndex(depthMeters)];

        /// <summary>Deterministic hardness pockets: veins are harder and pay more.</summary>
        public bool IsVein(Vector3 world)
        {
            var zone = Zone(-world.y);
            return zone.veinShare > 0 && Noise(world * 0.45f, 7) < zone.veinShare;
        }
        public RockDef Rock(Vector3 world) => IsVein(world) ? Zone(-world.y).vein : Zone(-world.y).rock;
        public int HitsToClear(RockDef rock, int tier) => Mathf.CeilToInt(rock.hardness / (float)Mathf.Max(1, tools[tier].damage));

        /// <summary>Smooth 3D value noise in [0, 1).</summary>
        public float Noise(Vector3 p, int salt)
        {
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y), z = Mathf.FloorToInt(p.z);
            float fx = Smooth(p.x - x), fy = Smooth(p.y - y), fz = Smooth(p.z - z);
            float Lattice(int dx, int dy, int dz) => (Hash(x + dx, y + dy, z + dz, salt) & 0xffff) / 65536f;
            float a = Mathf.Lerp(Lattice(0, 0, 0), Lattice(1, 0, 0), fx), b = Mathf.Lerp(Lattice(0, 1, 0), Lattice(1, 1, 0), fx);
            float c = Mathf.Lerp(Lattice(0, 0, 1), Lattice(1, 0, 1), fx), d = Mathf.Lerp(Lattice(0, 1, 1), Lattice(1, 1, 1), fx);
            return Mathf.Lerp(Mathf.Lerp(a, b, fy), Mathf.Lerp(c, d, fy), fz);
        }
        private static float Smooth(float t) => t * t * (3 - 2 * t);

        public uint Hash(int x, int y, int z, int salt)
        {
            unchecked
            {
                uint h = (uint)x * 0x9E3779B1u ^ (uint)y * 0x85EBCA77u ^ (uint)z * 0xC2B2AE3Du ^ (uint)salt * 0x27D4EB2Fu ^ (uint)seed;
                h = (h ^ (h >> 16)) * 0x45d9f3b;
                h = (h ^ (h >> 16)) * 0x45d9f3b;
                return h ^ (h >> 16);
            }
        }

        private void OnValidate()
        {
            depth = Mathf.Max(5, depth);
            width = Mathf.Max(4, width);
            chunk = Mathf.Max(4, chunk);
            voxel = Mathf.Max(0.25f, voxel);
        }
    }
}
