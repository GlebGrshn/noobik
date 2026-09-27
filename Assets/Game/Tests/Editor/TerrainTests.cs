using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Nubik.Tests
{
    public class TerrainTests
    {
        private MineConfig config;
        private VoxelTerrain terrain;

        [SetUp] public void Setup() { config = ScriptableObject.CreateInstance<MineConfig>(); terrain = new VoxelTerrain(config); }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(config);

        [Test] public void GroundStartsAtZero()
        {
            Assert.IsTrue(terrain.IsAir(new Vector3(0, 0.3f, 0)));
            Assert.IsFalse(terrain.IsAir(new Vector3(0, -0.3f, 0)));
            Assert.IsFalse(terrain.IsAir(new Vector3(3, -50, 3)));
        }

        // Hits of the dig core per voxel follow the economy table: dirt 2 with the basic shovel,
        // stone 3 with copper, 2 with steel, dense rock 6 with steel and 4 with crystal.
        [TestCase(0, 2, 2)]
        [TestCase(1, 6, 3)]
        [TestCase(2, 6, 2)]
        [TestCase(2, 24, 6)]
        [TestCase(3, 24, 4)]
        [TestCase(0, 6, 6)]
        public void CoreHitsMatchTheTable(int tool, int hardness, int expected)
        {
            int cut = VoxelTerrain.Cut(config.tools[tool].damage, hardness), density = 255, hits = 0;
            while (density >= VoxelTerrain.Iso) { density -= cut; hits++; }
            Assert.AreEqual(expected, hits);
            Assert.AreEqual(expected, config.HitsToClear(new RockDef { hardness = hardness }, tool));
        }

        [Test] public void DigPaysEachVoxelOnce()
        {
            var at = new Vector3(0, -2, 0);
            int cleared = 0, value = 0;
            for (int i = 0; i < 8; i++)
            {
                var result = terrain.Dig(at, 0.9f, 7);
                cleared += result.Cleared; value += result.Value;
            }
            Assert.Greater(cleared, 0);
            var again = terrain.Dig(at, 0.9f, 7);
            Assert.AreEqual(0, again.Cleared);
            Assert.AreEqual(0, again.Value);
            Assert.IsTrue(terrain.IsAir(at));
        }

        [Test] public void EdgeAndBedrockNeverChange()
        {
            float edge = config.width / 2f;
            var wall = new Vector3(-edge, -5, 0);
            var floor = new Vector3(0, config.FloorY - 0.2f, 0);
            for (int i = 0; i < 20; i++) { terrain.Dig(wall, 1.5f, 50); terrain.Dig(floor, 1.5f, 50); }
            Assert.AreEqual(255, terrain.Sample(wall), 0.01f);
            Assert.IsFalse(terrain.IsAir(new Vector3(0, config.FloorY - 0.3f, 3)));
        }

        [Test] public void DigMarksNeighbourChunksDirty()
        {
            terrain.ClearDirty();
            float border = config.Origin.x + config.chunk * config.voxel;
            terrain.Dig(new Vector3(border, -3, 0), 0.9f, 7);
            var dirty = new HashSet<int>(terrain.DirtyChunks);
            Assert.GreaterOrEqual(dirty.Count, 2);
        }

        [Test] public void ChunkEncodingRoundTrips()
        {
            terrain.Dig(new Vector3(1, -1, 1), 1f, 3);
            terrain.Dig(new Vector3(1.5f, -1.5f, 1), 1f, 3);
            var copy = new VoxelTerrain(config);
            foreach (int chunk in terrain.EditedChunks()) Assert.IsTrue(copy.Decode(chunk, terrain.Encode(chunk)));
            for (float y = -3; y < 0; y += 0.37f)
                Assert.AreEqual(terrain.Sample(new Vector3(1.2f, y, 1.1f)), copy.Sample(new Vector3(1.2f, y, 1.1f)), 0.001f);
        }

        [Test] public void SavedChunkCannotAddGround()
        {
            int chunk = terrain.ChunkIndex(0, config.ChunksY - 1, 0);
            int points = config.chunk * config.chunk * config.chunk;
            var bytes = new List<byte>();
            for (int left = points; left > 0; left -= 255) { bytes.Add((byte)Mathf.Min(255, left)); bytes.Add(255); }
            Assert.IsFalse(terrain.Decode(chunk, System.Convert.ToBase64String(bytes.ToArray())));
            Assert.IsFalse(terrain.Decode(chunk, "!!notbase64"));
        }

        private int GroundChunk => terrain.ChunkIndex(0, (int)(terrain.ToGrid(Vector3.zero).y / config.chunk), 0);

        [Test] public void MesherBuildsGroundAndHole()
        {
            var mesher = new TerrainMesher(terrain);
            var mesh = new Mesh();
            Assert.IsTrue(mesher.Build(GroundChunk, mesh));
            int before = mesher.TriangleCount;
            terrain.Dig(new Vector3(-3, -0.2f, -3), 1f, 7);
            Assert.IsTrue(mesher.Build(GroundChunk, mesh));
            Assert.Greater(mesher.TriangleCount, before);
            foreach (var normal in mesh.normals) Assert.AreEqual(1, normal.magnitude, 0.01f);
            Object.DestroyImmediate(mesh);
        }

        [Test] public void GroundFacesUp()
        {
            var mesher = new TerrainMesher(terrain);
            var mesh = new Mesh();
            Assert.IsTrue(mesher.Build(GroundChunk, mesh));
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            // Unity front faces wind clockwise; seen from above that makes cross(b - a, c - a) point up.
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]]; var b = vertices[triangles[i + 1]]; var c = vertices[triangles[i + 2]];
                Assert.Greater(Vector3.Cross(b - a, c - a).y, 0);
            }
            Object.DestroyImmediate(mesh);
        }

        [Test] public void DoorChamberIsOpenAtTheBottom()
        {
            Assert.IsTrue(terrain.IsAir(new Vector3(0, config.FloorY + 1, 0)));
            Assert.IsFalse(terrain.IsAir(new Vector3(0, config.FloorY + 6, 0)));
        }
    }
}
