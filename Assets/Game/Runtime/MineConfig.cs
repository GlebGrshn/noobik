using System;
using UnityEngine;

namespace Nubik
{
    [Serializable]
    public sealed class RockDef
    {
        public string nameRu, nameEn;
        [Tooltip("Hits of a damage-1 tool needed to clear one voxel.")] public int hardness = 2;
        [Tooltip("Coins per cleared voxel, in hundredths. Zero: money comes from selling ore.")] public int value;
        public Color color = Color.gray;
    }

    /// <summary>Anything that rides in the backpack and sells in the house: ore pieces and chests.</summary>
    [Serializable]
    public sealed class OreDef
    {
        public string nameRu, nameEn;
        public int value = 5;
        [Tooltip("Backpack slots one piece takes.")] public int slots = 1;
        public bool chest;
        public Color color = Color.gray;
    }

    [Serializable]
    public sealed class OreChance
    {
        public int ore;
        public int weight = 1;
    }

    [Serializable]
    public sealed class ZoneDef
    {
        public string nameRu, nameEn;
        [Tooltip("One line shown when the player first reaches the zone.")] public string noteRu, noteEn;
        [Tooltip("Depth in meters where the zone begins.")] public int startDepth;
        public RockDef rock = new RockDef(), vein = new RockDef();
        [Range(0, 1)] public float veinShare = 0.18f;
        [Tooltip("Cubic meters of ground per hidden ore piece.")] public float volumePerFind = 10;
        public OreChance[] ores = new OreChance[0];
        [Tooltip("Ore entry used for the chests of this zone.")] public int chestOre;
        public int[] chestDepths = new int[0];
        public Color fog = Color.black, ambient = Color.gray, sun = Color.white;
        public float sunIntensity = 1f, lamp, fogEnd = 40;
    }

    [Serializable]
    public sealed class ToolDef
    {
        public string nameRu, nameEn;
        public int damage = 1, price;
        [Tooltip("Dig sphere radius in meters.")] public float radius = 1f;
        [Tooltip("Seconds between hits.")] public float interval = 0.42f;
        public Color color = Color.gray;
    }

    /// <summary>One step of an upgrade track; the meaning of value depends on the track.</summary>
    [Serializable]
    public sealed class LevelDef
    {
        public int price;
        public float value;
    }

    [Serializable]
    public sealed class ItemDef
    {
        public string nameRu, nameEn;
        public int price, slots = 1;
        [Tooltip("Scanner: range in meters. Medkit: health restored.")] public float power;
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
        [Tooltip("Width and length of the diggable yard patch, meters.")] public int width = 14;
        [Tooltip("Natural caves start this deep (part of the world shape: changing it moves caves under old saves).")] public float cavernsFrom = 73;
        [Tooltip("Health lost per second standing in lava.")] public float lavaDamage = 28;
        [Range(0, 1)] public float oreDensity = 2f / 3f;
        public float voxel = 0.5f;
        [Tooltip("Voxels per chunk edge.")] public int chunk = 12;
        public float reach = 3.2f;
        public float moveSpeed = 4.5f;
        [Tooltip("About 0.9 m high: enough to climb dug steps.")] public float jumpSpeed = 6f;
        public float gravity = 20f;
        [Tooltip("Landing faster than this (m/s) hurts.")] public float safeFallSpeed = 10f;
        [Tooltip("Health lost per m/s above the safe speed.")] public float fallDamage = 7f;
        [Tooltip("Health restored per second on the lawn; the house heals fully.")] public float surfaceRegen = 2f;
        [Tooltip("Upward acceleration of the jetpack, m/s².")] public float jetThrust = 32f;
        public float jetMaxRise = 5.5f;
        [Tooltip("Share of the tank refilled per second while standing.")] public float jetRecharge = 0.35f;
        public float scanDuration = 6f, scanCooldown = 10f;
        public int firstDiscoveryCoins = 50;
        public float refillRate = 12f, drillFuelPerHit = .55f;
        public LevelDef[] fuelTank =
        {
            new LevelDef { price = 0, value = 15 }, new LevelDef { price = 100, value = 28 },
            new LevelDef { price = 280, value = 50 }, new LevelDef { price = 600, value = 85 },
            new LevelDef { price = 1000, value = 140 }, new LevelDef { price = 1600, value = 220 }
        };

        public ToolDef[] tools =
        {
            new ToolDef { nameRu = "Обычная лопата", nameEn = "Basic shovel", damage = 1, price = 0, radius = 1f, interval = 0.42f, color = new Color(0.62f, 0.64f, 0.66f) },
            new ToolDef { nameRu = "Медная лопата", nameEn = "Copper shovel", damage = 2, price = 60, radius = 1.05f, interval = 0.41f, color = new Color(0.90f, 0.52f, 0.25f) },
            new ToolDef { nameRu = "Бронзовая лопата", nameEn = "Bronze shovel", damage = 3, price = 160, radius = 1.08f, interval = 0.39f, color = new Color(0.80f, 0.60f, 0.30f) },
            new ToolDef { nameRu = "Стальная лопата", nameEn = "Steel shovel", damage = 4, price = 350, radius = 1.12f, interval = 0.37f, color = new Color(0.80f, 0.88f, 0.94f) },
            new ToolDef { nameRu = "Закалённая лопата", nameEn = "Tempered shovel", damage = 6, price = 650, radius = 1.16f, interval = 0.35f, color = new Color(0.45f, 0.55f, 0.75f) },
            new ToolDef { nameRu = "Титановая лопата", nameEn = "Titanium shovel", damage = 8, price = 1100, radius = 1.2f, interval = 0.33f, color = new Color(0.72f, 0.74f, 0.80f) },
            new ToolDef { nameRu = "Кристальная лопата", nameEn = "Crystal shovel", damage = 11, price = 1800, radius = 1.26f, interval = 0.31f, color = new Color(0.55f, 0.95f, 1f) },
            new ToolDef { nameRu = "Бензобур", nameEn = "Petrol drill", damage = 20, price = 3000, radius = 1.4f, interval = 0.16f, color = new Color(0.95f, 0.67f, 0.24f) },
        };

        [Tooltip("Backpack slots per level.")]
        public LevelDef[] backpack =
        {
            new LevelDef { price = 0, value = 8 }, new LevelDef { price = 80, value = 12 }, new LevelDef { price = 220, value = 18 },
            new LevelDef { price = 450, value = 26 }, new LevelDef { price = 800, value = 36 }, new LevelDef { price = 1400, value = 50 },
        };

        [Tooltip("Jetpack petrol consumption in litres per second. Basic jetpack is available from the start.")]
        public LevelDef[] jetpack =
        {
            new LevelDef { price = 0, value = 1.6f }, new LevelDef { price = 250, value = 1.4f }, new LevelDef { price = 450, value = 1.2f },
            new LevelDef { price = 800, value = 1f }, new LevelDef { price = 1300, value = .85f }, new LevelDef { price = 2000, value = .7f },
        };

        [Tooltip("Maximum health per level.")]
        public LevelDef[] health =
        {
            new LevelDef { price = 0, value = 100 }, new LevelDef { price = 150, value = 130 }, new LevelDef { price = 350, value = 170 },
            new LevelDef { price = 700, value = 220 }, new LevelDef { price = 1200, value = 280 },
        };

        public ItemDef scanner = new ItemDef { nameRu = "Сканер руды", nameEn = "Ore scanner", price = 180, slots = 2, power = 14 };
        public ItemDef medkit = new ItemDef { nameRu = "Аптечка", nameEn = "Medkit", price = 25, slots = 1, power = 50 };
        [Tooltip("Power: blast radius in metres.")]
        public ItemDef dynamite = new ItemDef { nameRu = "Динамит", nameEn = "Dynamite", price = 35, slots = 1, power = 2.3f };
        [Tooltip("Seconds from the throw to the blast.")] public float dynamiteFuse = 2.2f;
        [Tooltip("Digging damage of the blast core: enough for any rock above the bedrock.")] public int dynamiteDamage = 50;
        [Tooltip("Health lost right next to the blast; less further away.")] public float dynamiteHurt = 55;

        public OreDef[] ores =
        {
            new OreDef { nameRu = "Уголь", nameEn = "Coal", value = 4, color = new Color(0.20f, 0.20f, 0.23f) },
            new OreDef { nameRu = "Медь", nameEn = "Copper", value = 9, color = new Color(0.90f, 0.52f, 0.28f) },
            new OreDef { nameRu = "Железо", nameEn = "Iron", value = 15, color = new Color(0.72f, 0.58f, 0.52f) },
            new OreDef { nameRu = "Серебро", nameEn = "Silver", value = 28, color = new Color(0.86f, 0.92f, 0.98f) },
            new OreDef { nameRu = "Золото", nameEn = "Gold", value = 55, color = new Color(1f, 0.78f, 0.20f) },
            new OreDef { nameRu = "Аметист", nameEn = "Amethyst", value = 90, color = new Color(0.70f, 0.45f, 0.95f) },
            new OreDef { nameRu = "Изумруд", nameEn = "Emerald", value = 140, color = new Color(0.30f, 0.92f, 0.52f) },
            new OreDef { nameRu = "Алмаз", nameEn = "Diamond", value = 240, color = new Color(0.72f, 0.96f, 1f) },
            new OreDef { nameRu = "Старый сундук", nameEn = "Old chest", value = 45, slots = 2, chest = true, color = new Color(1f, 0.72f, 0.20f) },
            new OreDef { nameRu = "Сундук с серебром", nameEn = "Silver chest", value = 90, slots = 2, chest = true, color = new Color(0.86f, 0.92f, 0.98f) },
            new OreDef { nameRu = "Кристальный ларец", nameEn = "Crystal casket", value = 180, slots = 2, chest = true, color = new Color(0.55f, 0.95f, 1f) },
            // Appended entries keep the saved backpack indices of the ones above.
            new OreDef { nameRu = "Огненный опал", nameEn = "Fire opal", value = 320, color = new Color(1f, 0.45f, 0.18f) },
            new OreDef { nameRu = "Звёздный металл", nameEn = "Star metal", value = 450, slots = 2, color = new Color(0.55f, 0.78f, 1f) },
        };

        public ZoneDef[] zones =
        {
            new ZoneDef
            {
                nameRu = "Корни", nameEn = "Rootlands", noteRu = "Мягкая земля и переплетённые корни. На 18 м остался лагерь.", noteEn = "Soft soil and tangled roots. A camp remains at 18 m.", startDepth = 0,
                rock = new RockDef { nameRu = "Земля", nameEn = "Dirt", hardness = 2, color = new Color(0.55f, 0.34f, 0.20f) },
                vein = new RockDef { nameRu = "Глина", nameEn = "Clay", hardness = 2, color = new Color(0.72f, 0.45f, 0.28f) },
                veinShare = 0.2f, volumePerFind = 12, chestDepths = new[] { 12, 24 }, chestOre = 8,
                ores = new[] { new OreChance { ore = 0, weight = 45 }, new OreChance { ore = 1, weight = 40 }, new OreChance { ore = 2, weight = 15 } },
                fog = new Color(0.20f, 0.13f, 0.09f), ambient = new Color(0.62f, 0.66f, 0.72f), sun = new Color(1f, 0.95f, 0.86f), sunIntensity = 0.55f, lamp = 1.4f, fogEnd = 34,
            },
            new ZoneDef
            {
                nameRu = "Сланец", nameEn = "Slate", noteRu = "Холодный камень, серебро и старые рельсы. Ищи штольню на 48 м.", noteEn = "Cold stone, silver and old rails. Look for the gallery at 48 m.", startDepth = 30,
                rock = new RockDef { nameRu = "Камень", nameEn = "Stone", hardness = 6, color = new Color(0.37f, 0.45f, 0.47f) },
                vein = new RockDef { nameRu = "Прочная жила", nameEn = "Hard vein", hardness = 10, color = new Color(0.34f, 0.40f, 0.55f) },
                veinShare = 0.16f, volumePerFind = 12, chestDepths = new[] { 38, 56 }, chestOre = 9,
                ores = new[] { new OreChance { ore = 2, weight = 40 }, new OreChance { ore = 3, weight = 40 }, new OreChance { ore = 4, weight = 20 } },
                fog = new Color(0.07f, 0.08f, 0.11f), ambient = new Color(0.36f, 0.39f, 0.46f), sun = new Color(0.8f, 0.88f, 1f), sunIntensity = 0.15f, lamp = 2.2f, fogEnd = 26,
            },
            new ZoneDef
            {
                nameRu = "Кристаллы", nameEn = "Crystals", noteRu = "Светящиеся кристаллы и природные пустоты. Грот ждёт на 88 м.", noteEn = "Glowing crystals and natural hollows. A grotto waits at 88 m.", startDepth = 70,
                rock = new RockDef { nameRu = "Плотная порода", nameEn = "Dense rock", hardness = 24, color = new Color(0.29f, 0.25f, 0.37f) },
                vein = new RockDef { nameRu = "Кристаллическая жила", nameEn = "Crystal vein", hardness = 32, color = new Color(0.45f, 0.33f, 0.72f) },
                veinShare = 0.15f, volumePerFind = 11, chestDepths = new[] { 84, 106 }, chestOre = 10,
                ores = new[] { new OreChance { ore = 4, weight = 40 }, new OreChance { ore = 5, weight = 30 }, new OreChance { ore = 6, weight = 20 }, new OreChance { ore = 7, weight = 10 } },
                fog = new Color(0.05f, 0.03f, 0.09f), ambient = new Color(0.30f, 0.26f, 0.42f), sun = new Color(0.7f, 0.6f, 1f), sunIntensity = 0f, lamp = 2.8f, fogEnd = 22,
            },
            new ZoneDef
            {
                nameRu = "Магма", nameEn = "Magma", noteRu = "Раскалённый базальт и лавовые озёра. Где-то здесь упал метеорит.", noteEn = "Glowing basalt and lava pools. A meteorite fell somewhere here.", startDepth = 100,
                rock = new RockDef { nameRu = "Базальт", nameEn = "Basalt", hardness = 28, color = new Color(0.26f, 0.18f, 0.16f) },
                vein = new RockDef { nameRu = "Обсидиан", nameEn = "Obsidian", hardness = 40, color = new Color(0.13f, 0.11f, 0.15f) },
                veinShare = 0.14f, volumePerFind = 11, chestOre = 10,
                ores = new[] { new OreChance { ore = 6, weight = 25 }, new OreChance { ore = 7, weight = 40 }, new OreChance { ore = 11, weight = 35 } },
                fog = new Color(0.16f, 0.05f, 0.02f), ambient = new Color(0.52f, 0.32f, 0.24f), sun = new Color(1f, 0.5f, 0.3f), sunIntensity = 0f, lamp = 2.4f, fogEnd = 24,
            },
        };

        public CollectibleDef[] collection =
        {
            new CollectibleDef { nameRu = "Старая каска", nameEn = "Old helmet", depth = 3, spot = new Vector2(0.4f, -2.4f), spread = 0, color = new Color(0.08f, 0.61f, 0.59f) },
            new CollectibleDef { nameRu = "Окаменелая ракушка", nameEn = "Fossil shell", depth = 44, color = new Color(0.95f, 0.86f, 0.68f) },
            new CollectibleDef { nameRu = "Светящийся кристалл", nameEn = "Glowing crystal", depth = 78, color = new Color(0.45f, 1f, 0.92f) },
            new CollectibleDef { nameRu = "Фрагмент механизма", nameEn = "Mechanism fragment", depth = 97, color = new Color(0.80f, 0.58f, 0.28f) },
            new CollectibleDef { nameRu = "Осколок печати", nameEn = "Seal fragment", depth = 114, color = new Color(1f, 0.84f, 0.30f) },
        };

        /// <summary>Picks an ore of the zone from a hash, by weight.</summary>
        public int PickOre(ZoneDef zone, uint hash)
        {
            int total = 0;
            foreach (var chance in zone.ores) total += Mathf.Max(0, chance.weight);
            if (total <= 0) return 0;
            int roll = (int)(hash % (uint)total);
            foreach (var chance in zone.ores)
            {
                roll -= Mathf.Max(0, chance.weight);
                if (roll < 0) return chance.ore;
            }
            return zone.ores[zone.ores.Length - 1].ore;
        }

        // Voxel grid: x/z across the yard patch, y from the bedrock floor up to a few meters of air.
        public int SizeX => Mathf.RoundToInt(width / voxel);
        public int SizeZ => SizeX;
        public int ChunksX => Mathf.CeilToInt(SizeX / (float)chunk);
        public int ChunksZ => ChunksX;
        public int ChunksY => Mathf.CeilToInt((depth + 6) / voxel / chunk);
        public int SizeY => ChunksY * chunk;
        public int ChunkCount => ChunksX * ChunksY * ChunksZ;
        // World-aligned 6 m loot cells keep old central finds stable when the terrain grid grows.
        public int LootChunkCount => 16 * ChunksY;
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
