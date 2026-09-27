using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nubik
{
    public struct DigResult
    {
        public bool Changed;
        public int Cleared;
        /// <summary>Coins earned, in hundredths.</summary>
        public int Value;
        public RockDef Rock;
    }

    /// <summary>
    /// Density field of the diggable yard patch. Points hold 0 (air) .. 255 (solid);
    /// the surface is where density crosses <see cref="Iso"/>. Digging only lowers density,
    /// so every voxel pays out once. Saves store only chunks that differ from generation.
    /// </summary>
    public sealed class VoxelTerrain
    {
        public const int Iso = 128;
        public readonly MineConfig Config;
        public readonly int SizeX, SizeY, SizeZ, Chunk, ChunksX, ChunksY, ChunksZ;
        private readonly byte[] density;
        private readonly bool[] meshDirty, edited;
        private readonly Dictionary<int, string> encoded = new Dictionary<int, string>();
        private readonly List<int> dirtyList = new List<int>();

        public VoxelTerrain(MineConfig config)
        {
            Config = config;
            SizeX = config.SizeX; SizeY = config.SizeY; SizeZ = config.SizeZ; Chunk = config.chunk;
            ChunksX = config.ChunksX; ChunksY = config.ChunksY; ChunksZ = config.ChunksZ;
            density = new byte[(SizeX + 1) * (SizeY + 1) * (SizeZ + 1)];
            meshDirty = new bool[config.ChunkCount];
            edited = new bool[config.ChunkCount];
            for (int y = 0; y <= SizeY; y++)
                for (int z = 0; z <= SizeZ; z++)
                    for (int x = 0; x <= SizeX; x++)
                        density[Index(x, y, z)] = Initial(x, y, z);
        }

        public int Index(int x, int y, int z) => (y * (SizeZ + 1) + z) * (SizeX + 1) + x;
        public byte this[int x, int y, int z] => density[Index(x, y, z)];
        public int ChunkIndex(int cx, int cy, int cz) => (cy * ChunksZ + cz) * ChunksX + cx;
        public Vector3Int ChunkCoords(int chunk) => new Vector3Int(chunk % ChunksX, chunk / (ChunksX * ChunksZ), chunk / ChunksX % ChunksZ);

        /// <summary>The outer ring and the bedrock floor never change.</summary>
        public bool Fixed(int x, int y, int z) =>
            x == 0 || z == 0 || x == SizeX || z == SizeZ || y == SizeY || Config.PointPosition(x, y, z).y <= Config.FloorY;

        public byte Initial(int x, int y, int z)
        {
            var world = Config.PointPosition(x, y, z);
            float solid = Mathf.Clamp01(0.5f - world.y / Config.voxel);
            if (Fixed(x, y, z)) return (byte)Mathf.RoundToInt(solid * 255);
            // Natural caves in the deepest zone and the chamber of the sealed door.
            float cavernTop = Config.zones[Config.zones.Length - 1].startDepth + 3;
            if (-world.y > cavernTop && world.y > Config.FloorY + 2)
                solid = Mathf.Min(solid, Mathf.Clamp01((0.68f - Config.Noise(world * 0.22f, 13)) / 0.06f + 0.5f));
            float chamber = Vector3.Distance(world, new Vector3(0, Config.FloorY + 0.6f, 0));
            solid = Mathf.Min(solid, Mathf.Clamp01((chamber - 2.6f) / Config.voxel + 0.5f));
            return (byte)Mathf.RoundToInt(solid * 255);
        }

        public bool Solid(int x, int y, int z) => density[Index(x, y, z)] >= Iso;

        public Vector3 ToGrid(Vector3 world) => (world - Config.Origin) / Config.voxel;

        /// <summary>Trilinear density at a world position, 0..255; outside the grid counts as solid below ground.</summary>
        public float Sample(Vector3 world)
        {
            var g = ToGrid(world);
            if (g.x < 0 || g.z < 0 || g.x > SizeX || g.z > SizeZ || g.y < 0) return world.y <= 0 ? 255 : 0;
            if (g.y > SizeY) return 0;
            int x = Mathf.Min((int)g.x, SizeX - 1), y = Mathf.Min((int)g.y, SizeY - 1), z = Mathf.Min((int)g.z, SizeZ - 1);
            float fx = g.x - x, fy = g.y - y, fz = g.z - z;
            float c00 = Mathf.Lerp(this[x, y, z], this[x + 1, y, z], fx), c10 = Mathf.Lerp(this[x, y + 1, z], this[x + 1, y + 1, z], fx);
            float c01 = Mathf.Lerp(this[x, y, z + 1], this[x + 1, y, z + 1], fx), c11 = Mathf.Lerp(this[x, y + 1, z + 1], this[x + 1, y + 1, z + 1], fx);
            return Mathf.Lerp(Mathf.Lerp(c00, c10, fy), Mathf.Lerp(c01, c11, fy), fz);
        }

        public bool IsAir(Vector3 world) => Sample(world) < Iso;

        /// <summary>True if any point within the radius is air: a buried find becomes visible.</summary>
        public bool IsExposed(Vector3 world, float radius)
        {
            var g = ToGrid(world);
            float r = radius / Config.voxel;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(g.x - r)), x1 = Mathf.Min(SizeX, Mathf.CeilToInt(g.x + r));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(g.y - r)), y1 = Mathf.Min(SizeY, Mathf.CeilToInt(g.y + r));
            int z0 = Mathf.Max(0, Mathf.FloorToInt(g.z - r)), z1 = Mathf.Min(SizeZ, Mathf.CeilToInt(g.z + r));
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                        if (!Solid(x, y, z) && (new Vector3(x, y, z) - g).sqrMagnitude <= r * r) return true;
            return false;
        }

        /// <summary>Density removed per hit in the core of the dig sphere.</summary>
        public static int Cut(int damage, int hardness) => Mathf.CeilToInt(Iso * Mathf.Max(1, damage) / (float)Mathf.Max(1, hardness));

        public DigResult Dig(Vector3 center, float radius, int damage)
        {
            var result = new DigResult { Rock = Config.Rock(center) };
            var g = ToGrid(center);
            float r = radius / Config.voxel, core = r * 0.7f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(g.x - r)), x1 = Mathf.Min(SizeX, Mathf.CeilToInt(g.x + r));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(g.y - r)), y1 = Mathf.Min(SizeY, Mathf.CeilToInt(g.y + r));
            int z0 = Mathf.Max(0, Mathf.FloorToInt(g.z - r)), z1 = Mathf.Min(SizeZ, Mathf.CeilToInt(g.z + r));
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float distance = (new Vector3(x, y, z) - g).magnitude;
                        if (distance > r || Fixed(x, y, z)) continue;
                        int index = Index(x, y, z);
                        int before = density[index];
                        if (before == 0) continue;
                        var rock = Config.Rock(Config.PointPosition(x, y, z));
                        float falloff = distance <= core ? 1 : 1 - (distance - core) / (r - core);
                        int after = Mathf.Max(0, before - Mathf.CeilToInt(Cut(damage, rock.hardness) * falloff));
                        if (after == before) continue;
                        density[index] = (byte)after;
                        result.Changed = true;
                        if (before >= Iso && after < Iso) { result.Cleared++; result.Value += rock.value; }
                        MarkChanged(x, y, z);
                    }
            return result;
        }

        private void MarkChanged(int x, int y, int z)
        {
            int owner = ChunkIndex(Mathf.Min(x / Chunk, ChunksX - 1), Mathf.Min(y / Chunk, ChunksY - 1), Mathf.Min(z / Chunk, ChunksZ - 1));
            edited[owner] = true;
            encoded.Remove(owner);
            // Cells on both sides of the point may belong to neighbouring chunk meshes.
            for (int cy = Mathf.Max(0, y - 1) / Chunk; cy <= Mathf.Min(ChunksY - 1, (y + 1) / Chunk); cy++)
                for (int cz = Mathf.Max(0, z - 1) / Chunk; cz <= Mathf.Min(ChunksZ - 1, (z + 1) / Chunk); cz++)
                    for (int cx = Mathf.Max(0, x - 1) / Chunk; cx <= Mathf.Min(ChunksX - 1, (x + 1) / Chunk); cx++)
                    {
                        int chunk = ChunkIndex(cx, cy, cz);
                        if (meshDirty[chunk]) continue;
                        meshDirty[chunk] = true;
                        dirtyList.Add(chunk);
                    }
        }

        /// <summary>Chunks whose meshes must be rebuilt; the list is cleared by the caller.</summary>
        public List<int> DirtyChunks => dirtyList;
        public void ClearDirty() { foreach (int chunk in dirtyList) meshDirty[chunk] = false; dirtyList.Clear(); }

        // Save format: run-length pairs (count, density) over the points a chunk owns, base64.
        public IEnumerable<int> EditedChunks()
        {
            for (int i = 0; i < edited.Length; i++) if (edited[i]) yield return i;
        }

        public string Encode(int chunk)
        {
            if (encoded.TryGetValue(chunk, out var cached)) return cached;
            var bytes = new List<byte>();
            int run = 0, value = -1;
            foreach (int index in OwnedPoints(chunk))
            {
                int current = density[index];
                if (current == value && run < 255) { run++; continue; }
                if (run > 0) { bytes.Add((byte)run); bytes.Add((byte)value); }
                value = current; run = 1;
            }
            if (run > 0) { bytes.Add((byte)run); bytes.Add((byte)value); }
            return encoded[chunk] = Convert.ToBase64String(bytes.ToArray());
        }

        /// <summary>Applies a saved chunk. Rejects data that would add ground or move fixed points.</summary>
        public bool Decode(int chunk, string data)
        {
            if (chunk < 0 || chunk >= edited.Length || string.IsNullOrEmpty(data)) return false;
            byte[] bytes;
            try { bytes = Convert.FromBase64String(data); }
            catch (FormatException) { return false; }
            if (bytes.Length % 2 != 0) return false;
            var points = new List<int>(OwnedPoints(chunk));
            var values = new byte[points.Count];
            int cursor = 0;
            for (int i = 0; i < bytes.Length; i += 2)
                for (int n = 0; n < bytes[i]; n++)
                {
                    if (cursor >= values.Length) return false;
                    values[cursor++] = bytes[i + 1];
                }
            if (cursor != values.Length) return false;
            for (int i = 0; i < points.Count; i++)
            {
                int index = points[i];
                int x = index % (SizeX + 1), z = index / (SizeX + 1) % (SizeZ + 1), y = index / ((SizeX + 1) * (SizeZ + 1));
                byte initial = Initial(x, y, z);
                if (values[i] > initial || Fixed(x, y, z) && values[i] != initial) return false;
            }
            for (int i = 0; i < points.Count; i++)
            {
                int index = points[i];
                if (density[index] == values[i]) continue;
                density[index] = values[i];
                MarkChanged(index % (SizeX + 1), index / ((SizeX + 1) * (SizeZ + 1)), index / (SizeX + 1) % (SizeZ + 1));
            }
            edited[chunk] = true;
            encoded[chunk] = data;
            return true;
        }

        private IEnumerable<int> OwnedPoints(int chunk)
        {
            var c = ChunkCoords(chunk);
            int x1 = Mathf.Min(SizeX, (c.x + 1) * Chunk), y1 = Mathf.Min(SizeY, (c.y + 1) * Chunk), z1 = Mathf.Min(SizeZ, (c.z + 1) * Chunk);
            for (int y = c.y * Chunk; y < y1; y++)
                for (int z = c.z * Chunk; z < z1; z++)
                    for (int x = c.x * Chunk; x < x1; x++)
                        yield return Index(x, y, z);
        }
    }
}
