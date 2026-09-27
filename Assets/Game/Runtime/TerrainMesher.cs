using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nubik
{
    /// <summary>
    /// Naive surface nets: one vertex per cell that crosses the surface, one quad per crossing edge.
    /// Gives a smooth, welded mesh with gradient normals; a chunk owns the edges whose base point it contains.
    /// </summary>
    public sealed class TerrainMesher
    {
        private static readonly Color Topsoil = new Color(0.36f, 0.22f, 0.13f);
        private static readonly Color Bedrock = new Color(0.15f, 0.14f, 0.19f);
        private readonly VoxelTerrain terrain;
        private readonly MineConfig config;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        private readonly int[] cells;
        private readonly int span;

        public TerrainMesher(VoxelTerrain terrain)
        {
            this.terrain = terrain;
            config = terrain.Config;
            span = terrain.Chunk + 1;
            cells = new int[span * span * span];
        }

        public int TriangleCount => triangles.Count / 3;

        /// <summary>Fills the mesh; returns false when the chunk has no surface.</summary>
        public bool Build(int chunk, Mesh mesh)
        {
            vertices.Clear(); normals.Clear(); colors.Clear(); triangles.Clear();
            var c = terrain.ChunkCoords(chunk);
            var lo = new Vector3Int(c.x * terrain.Chunk, c.y * terrain.Chunk, c.z * terrain.Chunk);
            var hi = new Vector3Int(Mathf.Min(terrain.SizeX, lo.x + terrain.Chunk), Mathf.Min(terrain.SizeY, lo.y + terrain.Chunk), Mathf.Min(terrain.SizeZ, lo.z + terrain.Chunk));
            var size = new Vector3Int(terrain.SizeX, terrain.SizeY, terrain.SizeZ);

            for (int i = 0; i < cells.Length; i++) cells[i] = -1;
            for (int y = Mathf.Max(0, lo.y - 1); y < hi.y; y++)
                for (int z = Mathf.Max(0, lo.z - 1); z < hi.z; z++)
                    for (int x = Mathf.Max(0, lo.x - 1); x < hi.x; x++)
                        cells[Slot(x - lo.x + 1, y - lo.y + 1, z - lo.z + 1)] = CellVertex(x, y, z);

            for (int y = lo.y; y < hi.y; y++)
                for (int z = lo.z; z < hi.z; z++)
                    for (int x = lo.x; x < hi.x; x++)
                    {
                        var p = new Vector3Int(x, y, z);
                        bool solid = terrain.Solid(x, y, z);
                        for (int axis = 0; axis < 3; axis++)
                        {
                            int u = (axis + 1) % 3, v = (axis + 2) % 3;
                            if (p[u] < 1 || p[v] < 1 || p[axis] + 1 > size[axis]) continue;
                            var q = p; q[axis]++;
                            if (terrain.Solid(q.x, q.y, q.z) == solid) continue;
                            var du = Vector3Int.zero; du[u] = 1;
                            var dv = Vector3Int.zero; dv[v] = 1;
                            int a = Cell(p - lo), b = Cell(p - du - lo), d = Cell(p - du - dv - lo), e = Cell(p - dv - lo);
                            if (a < 0 || b < 0 || d < 0 || e < 0) continue;
                            // Solid below the edge's end means the surface faces +axis (clockwise front faces).
                            if (solid) Quad(a, b, d, e); else Quad(a, e, d, b);
                        }
                    }

            mesh.Clear();
            if (triangles.Count == 0) return false;
            mesh.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return true;
        }

        private int Slot(int x, int y, int z) => (y * span + z) * span + x;
        private int Cell(Vector3Int local) => cells[Slot(local.x + 1, local.y + 1, local.z + 1)];

        private void Quad(int a, int b, int c, int d)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a); triangles.Add(c); triangles.Add(d);
        }

        private int CellVertex(int x, int y, int z)
        {
            int mask = 0;
            for (int corner = 0; corner < 8; corner++)
                if (terrain.Solid(x + (corner & 1), y + (corner >> 1 & 1), z + (corner >> 2 & 1))) mask |= 1 << corner;
            if (mask == 0 || mask == 255) return -1;
            var sum = Vector3.zero;
            int count = 0;
            for (int corner = 0; corner < 8; corner++)
                for (int bit = 1; bit < 8; bit <<= 1)
                {
                    if ((corner & bit) != 0) continue;
                    int other = corner | bit;
                    if ((mask >> corner & 1) == (mask >> other & 1)) continue;
                    var p0 = new Vector3(corner & 1, corner >> 1 & 1, corner >> 2 & 1);
                    var p1 = new Vector3(other & 1, other >> 1 & 1, other >> 2 & 1);
                    float d0 = terrain[x + (int)p0.x, y + (int)p0.y, z + (int)p0.z];
                    float d1 = terrain[x + (int)p1.x, y + (int)p1.y, z + (int)p1.z];
                    sum += Vector3.Lerp(p0, p1, Mathf.Clamp01((VoxelTerrain.Iso - 0.5f - d0) / (d1 - d0)));
                    count++;
                }
            var grid = new Vector3(x, y, z) + sum / count;
            var world = config.Origin + grid * config.voxel;
            var normal = Normal(grid);
            vertices.Add(world);
            normals.Add(normal);
            colors.Add(Tint(world, normal));
            return vertices.Count - 1;
        }

        private Vector3 Normal(Vector3 grid)
        {
            const float h = 0.5f;
            var gradient = new Vector3(
                Density(grid + new Vector3(h, 0, 0)) - Density(grid - new Vector3(h, 0, 0)),
                Density(grid + new Vector3(0, h, 0)) - Density(grid - new Vector3(0, h, 0)),
                Density(grid + new Vector3(0, 0, h)) - Density(grid - new Vector3(0, 0, h)));
            return gradient.sqrMagnitude < 1e-6f ? Vector3.up : -gradient.normalized;
        }

        private float Density(Vector3 grid)
        {
            grid.x = Mathf.Clamp(grid.x, 0, terrain.SizeX); grid.y = Mathf.Clamp(grid.y, 0, terrain.SizeY); grid.z = Mathf.Clamp(grid.z, 0, terrain.SizeZ);
            return terrain.Sample(config.Origin + grid * config.voxel);
        }

        /// <summary>Linear-space vertex colour; the shader paints grass on flat ground itself.</summary>
        private Color Tint(Vector3 world, Vector3 normal)
        {
            if (world.y < config.FloorY + 0.7f) return Bedrock.linear;
            float depth = -world.y;
            Color color = config.Rock(world).color;
            if (depth < 1.4f) color = Color.Lerp(Topsoil, color, depth / 1.4f);
            // Soft strata so walls read as layers while digging down.
            color *= 0.88f + 0.12f * Mathf.Sin(world.y * 2.3f + config.Noise(world * 0.35f, 5) * 5f);
            color.a = 1;
            return color.linear;
        }
    }
}
