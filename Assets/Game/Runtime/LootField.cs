using System.Collections.Generic;
using UnityEngine;

namespace Nubik
{
    public enum LootKind { Find, Chest, Collectible, Key, Secret }

    public sealed class LootItem
    {
        public LootKind Kind;
        /// <summary>Finds: save slot inside their chunk. Specials and collectibles use their own indices.</summary>
        public int Chunk = -1, Slot = -1, Special = -1, Collectible = -1, Key = -1, Secret = -1;
        /// <summary>Ore entry for finds and chests; -1 for collectibles, keys and secrets.</summary>
        public int Ore = -1;
        public int Value, Slots;
        public Vector3 Position;
        public float Size;
        public Color Color;
        public GameObject View;
        public bool Taken, Exposed;
    }

    /// <summary>Hidden finds generated from the seed. Only positions and values live here; views are spawned when exposed.</summary>
    public sealed class LootField
    {
        public const int SlotsPerChunk = 64;
        public readonly List<LootItem> Items = new List<LootItem>();
        private readonly List<LootItem>[] byChunk;
        private readonly VoxelTerrain terrain;
        private readonly MineConfig config;

        public LootField(VoxelTerrain terrain)
        {
            this.terrain = terrain;
            config = terrain.Config;
            byChunk = new List<LootItem>[config.ChunkCount];
            for (int i = 0; i < byChunk.Length; i++) byChunk[i] = new List<LootItem>();

            // Special index order is part of the save format: first find, then chests zone by zone.
            var firstOres = config.zones[0].ores;
            Add(WithOre(new LootItem { Special = 0, Position = FirstFindPosition, Size = 0.3f }, firstOres.Length > 1 ? firstOres[1].ore : 0));
            int special = 1;
            foreach (var zone in config.zones)
                foreach (int depth in zone.chestDepths)
                {
                    var spot = Spread(depth, 307, 2.5f);
                    Add(WithOre(new LootItem { Special = special, Position = new Vector3(spot.x, -depth - 0.5f, spot.y), Size = 0.45f }, zone.chestOre));
                    special++;
                }
            for (int i = 0; i < config.collection.Length; i++)
            {
                var item = config.collection[i];
                var spot = item.spot + Spread(item.depth, 401 + i, item.spread);
                Add(new LootItem { Kind = LootKind.Collectible, Collectible = i, Position = new Vector3(spot.x, -item.depth - 0.5f, spot.y), Size = 0.4f, Color = item.color });
            }
            // Reserved IDs do not shift the original seven special finds or random chunk slots.
            foreach (var site in MineSites.All)
                Add(WithOre(new LootItem { Special = site.CacheId, Position = site.Cache, Size = 0.45f }, config.Zone(site.Depth).chestOre));

            for (int i = 0; i < Expedition.Keys.Length; i++)
                Add(new LootItem { Kind = LootKind.Key, Key = i, Position = Expedition.Keys[i].Position, Size = .34f, Color = Expedition.Keys[i].Color });
            for (int i = 0; i < Secrets.All.Length; i++)
                Add(new LootItem { Kind = LootKind.Secret, Secret = i, Position = Secrets.All[i].Position, Size = .36f, Color = Secrets.All[i].Color });

            float edge = config.width / 2f - 0.8f, extent = config.chunk * config.voxel;
            for (int cy = 0; cy < config.ChunksY; cy++)
                for (int iz = 0; iz < 4; iz++)
                    for (int ix = 0; ix < 4; ix++)
                    {
                        int chunk = (cy * 4 + iz) * 4 + ix;
                        var min = new Vector3((ix - 2) * extent, config.Origin.y + cy * extent, (iz - 2) * extent);
                        var zone = config.Zone(-(min.y + extent / 2));
                        int count = Mathf.Min(48, Mathf.RoundToInt(extent * extent * extent / Mathf.Max(1, zone.volumePerFind)));
                        bool original = ix >= 1 && ix <= 2 && iz >= 1 && iz <= 2;
                        int seedCell = original ? (cy * 2 + iz - 1) * 2 + ix - 1 : 10000 + chunk;
                        uint state = config.Hash(seedCell, 0, 0, 97) | 1;
                        for (int slot = 0; slot < count; slot++)
                        {
                            var position = min + new Vector3(Next(ref state), Next(ref state), Next(ref state)) * extent;
                            float size = .2f + Next(ref state) * .12f;
                            // Thin the existing sequence, never reroll positions or collected slot IDs.
                            if (Mathf.FloorToInt((slot + 1) * config.oreDensity + .0001f) == Mathf.FloorToInt(slot * config.oreDensity + .0001f)) continue;
                            if (position.y > -.6f || position.y < config.FloorY + .8f || Mathf.Abs(position.x) > edge || Mathf.Abs(position.z) > edge) continue;
                            int ore = config.PickOre(config.Zone(-position.y), state);
                            Add(WithOre(new LootItem { Chunk = chunk, Slot = slot, Position = Secrets.Embed(MineSites.AnchorOre(position, terrain)), Size = size }, ore));
                        }
                    }
        }

        public static readonly Vector3 FirstFindPosition = new Vector3(0, -0.05f, -2.5f);

        private LootItem WithOre(LootItem item, int ore)
        {
            ore = Mathf.Clamp(ore, 0, config.ores.Length - 1);
            var def = config.ores[ore];
            item.Ore = ore;
            item.Kind = def.chest ? LootKind.Chest : LootKind.Find;
            item.Value = def.value;
            item.Slots = Mathf.Max(1, def.slots);
            item.Color = def.color;
            return item;
        }

        private void Add(LootItem item)
        {
            Items.Add(item);
            byChunk[ChunkOf(item.Position)].Add(item);
        }

        public int ChunkOf(Vector3 world)
        {
            var g = terrain.ToGrid(world);
            int cx = Mathf.Clamp((int)(g.x / config.chunk), 0, config.ChunksX - 1);
            int cy = Mathf.Clamp((int)(g.y / config.chunk), 0, config.ChunksY - 1);
            int cz = Mathf.Clamp((int)(g.z / config.chunk), 0, config.ChunksZ - 1);
            return terrain.ChunkIndex(cx, cy, cz);
        }

        /// <summary>Items whose centers lie within the radius.</summary>
        public void Near(Vector3 center, float radius, List<LootItem> result)
        {
            result.Clear();
            var lo = terrain.ToGrid(center - Vector3.one * radius) / config.chunk;
            var hi = terrain.ToGrid(center + Vector3.one * radius) / config.chunk;
            for (int cy = Mathf.Max(0, (int)lo.y); cy <= Mathf.Min(config.ChunksY - 1, (int)hi.y); cy++)
                for (int cz = Mathf.Max(0, (int)lo.z); cz <= Mathf.Min(config.ChunksZ - 1, (int)hi.z); cz++)
                    for (int cx = Mathf.Max(0, (int)lo.x); cx <= Mathf.Min(config.ChunksX - 1, (int)hi.x); cx++)
                        foreach (var item in byChunk[terrain.ChunkIndex(cx, cy, cz)])
                            if (!item.Taken && (item.Position - center).sqrMagnitude <= radius * radius) result.Add(item);
        }

        public bool IsExposed(LootItem item) => item.Key == 0 || item.Secret >= 0 && Secrets.All[item.Secret].OnSurface || terrain.IsExposed(item.Position, item.Size + 0.35f);

        public void MarkTaken(GameProgress progress)
        {
            foreach (var item in Items)
                item.Taken = item.Key >= 0 ? progress.HasKey(item.Key) : item.Secret >= 0 ? progress.HasSecret(item.Secret) : item.Collectible >= 0 ? progress.HasCollectible(item.Collectible)
                    : item.Special >= 0 ? progress.HasSpecial(item.Special)
                    : progress.IsFound(item.Chunk, item.Slot);
        }

        private Vector2 Spread(int depth, int salt, float spread) =>
            new Vector2(config.Hash(depth, salt, 1, 509) % 1000 / 1000f - 0.5f, config.Hash(depth, salt, 2, 509) % 1000 / 1000f - 0.5f) * 2 * spread;

        private static float Next(ref uint state)
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return (state & 0xffffff) / 16777216f;
        }
    }
}
