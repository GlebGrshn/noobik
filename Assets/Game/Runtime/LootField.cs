using System.Collections.Generic;
using UnityEngine;

namespace Nubik
{
    public enum LootKind { Find, Chest, Collectible }

    public sealed class LootItem
    {
        public LootKind Kind;
        /// <summary>Finds: save slot inside their chunk. Specials and collectibles use their own indices.</summary>
        public int Chunk = -1, Slot = -1, Special = -1, Collectible = -1;
        public int Value;
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
            var firstZone = config.zones[0];
            Add(new LootItem { Kind = LootKind.Find, Special = 0, Value = (firstZone.findMin + firstZone.findMax) / 2, Position = FirstFindPosition, Size = 0.3f, Color = firstZone.findColor });
            int special = 1;
            foreach (var zone in config.zones)
                foreach (int depth in zone.chestDepths)
                {
                    var spot = Spread(depth, 307, 2.5f);
                    Add(new LootItem { Kind = LootKind.Chest, Special = special, Value = Range(zone.chestMin, zone.chestMax, config.Hash(depth, special, 0, 311)), Position = new Vector3(spot.x, -depth - 0.5f, spot.y), Size = 0.45f, Color = zone.findColor });
                    special++;
                }
            for (int i = 0; i < config.collection.Length; i++)
            {
                var item = config.collection[i];
                var spot = item.spot + Spread(item.depth, 401 + i, item.spread);
                Add(new LootItem { Kind = LootKind.Collectible, Collectible = i, Position = new Vector3(spot.x, -item.depth - 0.5f, spot.y), Size = 0.4f, Color = item.color });
            }

            float edge = config.width / 2f - 0.8f, extent = config.chunk * config.voxel;
            for (int chunk = 0; chunk < config.ChunkCount; chunk++)
            {
                var c = terrain.ChunkCoords(chunk);
                var min = config.PointPosition(c.x * config.chunk, c.y * config.chunk, c.z * config.chunk);
                var zone = config.Zone(-(min.y + extent / 2));
                int count = Mathf.Min(48, Mathf.RoundToInt(extent * extent * extent / Mathf.Max(1, zone.volumePerFind)));
                uint state = config.Hash(chunk, 0, 0, 97) | 1;
                for (int slot = 0; slot < count; slot++)
                {
                    var position = min + new Vector3(Next(ref state), Next(ref state), Next(ref state)) * extent;
                    float size = 0.2f + Next(ref state) * 0.12f;
                    if (position.y > -0.6f || position.y < config.FloorY + 0.8f || Mathf.Abs(position.x) > edge || Mathf.Abs(position.z) > edge) continue;
                    var local = config.Zone(-position.y);
                    Add(new LootItem { Kind = LootKind.Find, Chunk = chunk, Slot = slot, Value = Range(local.findMin, local.findMax, state), Position = position, Size = size, Color = local.findColor });
                }
            }
        }

        public static readonly Vector3 FirstFindPosition = new Vector3(0, -0.05f, -2.5f);

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

        public bool IsExposed(LootItem item) => terrain.IsExposed(item.Position, item.Size + 0.35f);

        public void MarkTaken(GameProgress progress)
        {
            foreach (var item in Items)
                item.Taken = item.Collectible >= 0 ? progress.HasCollectible(item.Collectible)
                    : item.Special >= 0 ? progress.HasSpecial(item.Special)
                    : progress.IsFound(item.Chunk, item.Slot);
        }

        private Vector2 Spread(int depth, int salt, float spread) =>
            new Vector2(config.Hash(depth, salt, 1, 509) % 1000 / 1000f - 0.5f, config.Hash(depth, salt, 2, 509) % 1000 / 1000f - 0.5f) * 2 * spread;

        private static int Range(int min, int max, uint hash) => max <= min ? min : min + (int)(hash % (uint)(max - min + 1));

        private static float Next(ref uint state)
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return (state & 0xffffff) / 16777216f;
        }
    }
}
