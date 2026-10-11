using System.Collections.Generic;
using Cinderbird.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class MarchingCubesTests
    {
        private const int Cells = 16;
        private const float CellSize = 0.25f;
        // Not a multiple of the cell size, so no sample lands exactly on the surface (avoids degenerate triangles).
        private const float BallRadius = 1.37f;

        private static MeshBuffers PolygonizeBall(out Vector3 center)
        {
            VoxelGrid grid = TerrainTestGrids.Ball(Cells, CellSize, BallRadius, out center);
            MeshBuffers buffers = new MeshBuffers();
            new MarchingCubes().Polygonize(grid, buffers);
            return buffers;
        }

        [Test]
        public void Tables_EveryCaseHasValidTriangles()
        {
            int[][] rows = MarchingCubesTables.TriangleRows();

            Assert.That(rows.Length, Is.EqualTo(256));
            Assert.That(rows[0], Is.Empty);
            Assert.That(rows[255], Is.Empty);

            for (int i = 0; i < rows.Length; i++)
            {
                Assert.That(rows[i].Length % 3, Is.Zero, $"Case {i} is not whole triangles");
                Assert.That(rows[i].Length, Is.LessThanOrEqualTo(MarchingCubesTables.MaxTriangleIndicesPerCase));
                Assert.That(rows[i], Has.All.InRange(0, 11), $"Case {i} has an invalid edge");
            }
        }

        [Test]
        public void Polygonize_AllAir_ProducesNoTriangles()
        {
            VoxelGrid grid = new VoxelGrid(4, 4, 4, 1f, Vector3.zero);
            MeshBuffers buffers = new MeshBuffers();

            new MarchingCubes().Polygonize(grid, buffers);

            Assert.That(buffers.Indices, Is.Empty);
            Assert.That(buffers.Vertices, Is.Empty);
        }

        [Test]
        public void Polygonize_AllAsh_ProducesNoTriangles()
        {
            VoxelGrid grid = new VoxelGrid(4, 4, 4, 1f, Vector3.zero);
            System.Array.Fill(grid.Densities, VoxelGrid.Ash);
            MeshBuffers buffers = new MeshBuffers();

            new MarchingCubes().Polygonize(grid, buffers);

            Assert.That(buffers.Indices, Is.Empty);
        }

        [Test]
        public void Polygonize_Ball_ProducesConsistentBuffers()
        {
            MeshBuffers buffers = PolygonizeBall(out _);

            Assert.That(buffers.Indices.Count, Is.GreaterThan(0));
            Assert.That(buffers.Indices.Count % 3, Is.Zero);
            Assert.That(buffers.Normals.Count, Is.EqualTo(buffers.Vertices.Count));
            Assert.That(buffers.Indices, Has.All.InRange(0, buffers.Vertices.Count - 1));
        }

        [Test]
        public void Polygonize_Ball_VerticesLieOnSphere()
        {
            MeshBuffers buffers = PolygonizeBall(out Vector3 center);

            foreach (Vector3 vertex in buffers.Vertices)
            {
                Assert.That((vertex - center).magnitude, Is.EqualTo(BallRadius).Within(CellSize * 0.25f));
            }
        }

        [Test]
        public void Polygonize_Ball_TrianglesFaceOutward()
        {
            MeshBuffers buffers = PolygonizeBall(out Vector3 center);
            List<Vector3> v = buffers.Vertices;
            List<int> indices = buffers.Indices;

            for (int i = 0; i < indices.Count; i += 3)
            {
                Vector3 a = v[indices[i]];
                Vector3 b = v[indices[i + 1]];
                Vector3 c = v[indices[i + 2]];
                // Unity treats clockwise triangles as front-facing; for those, this cross product points at the viewer.
                Vector3 faceNormal = Vector3.Cross(b - a, c - a);
                if (faceNormal.sqrMagnitude < 1e-12f)
                {
                    continue;
                }

                Vector3 outward = (a + b + c) / 3f - center;
                Assert.That(Vector3.Dot(faceNormal, outward), Is.GreaterThan(0f), $"Triangle {i / 3} faces inward");
            }
        }

        [Test]
        public void Polygonize_Ball_NormalsPointOutward()
        {
            MeshBuffers buffers = PolygonizeBall(out Vector3 center);

            for (int i = 0; i < buffers.Vertices.Count; i++)
            {
                Vector3 outward = (buffers.Vertices[i] - center).normalized;
                Assert.That(Vector3.Dot(buffers.Normals[i], outward), Is.GreaterThan(0.9f));
            }
        }

        [Test]
        public void Polygonize_Ball_IsClosedAndConsistentlyWound()
        {
            MeshBuffers buffers = PolygonizeBall(out _);
            List<int> indices = buffers.Indices;
            HashSet<(int, int)> directedEdges = new HashSet<(int, int)>();

            for (int i = 0; i < indices.Count; i += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    (int, int) edge = (indices[i + k], indices[i + (k + 1) % 3]);
                    Assert.That(directedEdges.Add(edge), Is.True, $"Edge {edge} used twice in the same direction");
                }
            }

            // In a closed, consistently wound mesh every edge is walked once in each direction.
            foreach ((int from, int to) in directedEdges)
            {
                Assert.That(directedEdges.Contains((to, from)), Is.True, $"Edge ({from},{to}) has no partner: hole in mesh");
            }
        }

        [Test]
        public void Polygonize_CalledTwice_GivesSameResult()
        {
            VoxelGrid grid = TerrainTestGrids.Ball(Cells, CellSize, BallRadius, out _);
            MarchingCubes mesher = new MarchingCubes();
            MeshBuffers buffers = new MeshBuffers();

            mesher.Polygonize(grid, buffers);
            int firstCount = buffers.Indices.Count;
            mesher.Polygonize(grid, buffers);

            Assert.That(buffers.Indices.Count, Is.EqualTo(firstCount));
        }
    }
}
