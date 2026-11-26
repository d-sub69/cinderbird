using System;
using System.Collections.Generic;
using Cinderbird.Core;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Turns the terrain's changed chunks into meshes and colliders: one <see cref="VoxelChunkView"/> per chunk.
    /// <list type="bullet">
    /// <item>Meshes are made on worker threads. A dig's are uploaded at once (<see cref="RebuildDirtyChunks"/>);
    /// sliding ash is meshed a few chunks per frame (<see cref="ScheduleDirtyChunks"/>), so even a huge slide costs
    /// each frame about the same.</item>
    /// <item>Colliders are baked in the background from their own copy of the shape, never waited for: right after
    /// a dig, and at most every <see cref="SlideColliderInterval"/> seconds per chunk while ash slides. Baking collision
    /// data is the slowest part of a re-mesh, and nobody can tell a collider a fraction of a second behind.</item>
    /// </list>
    /// Owned by <see cref="VoxelTerrain"/>; call <see cref="Update"/> every frame and <see cref="Dispose"/> when done.
    /// </summary>
    public sealed class ChunkMeshUpdater : IDisposable
    {
        // Shows up by name in Unity's Profiler, so slow frames can be traced to meshing.
        private static readonly ProfilerMarker MeshMarker = new ProfilerMarker("Cinderbird.Mesh");
        private static readonly ProfilerMarker ColliderMarker = new ProfilerMarker("Cinderbird.Colliders");

        // Enough to keep all worker threads busy; a dig touches at most 8 chunks, so only start-up is split.
        private const int MeshBatchSize = 32;
        // Sliding ash re-meshes at most this many chunks per frame; the rest wait their turn (a frame or two).
        private const int MaxSlideChunksPerFrame = 6;
        // While ash slides, a chunk's collider is re-baked at most this often.
        private const float SlideColliderInterval = 0.25f;
        // Colliders copied and baked per round while ash slides; each copy costs the main thread a little.
        private const int MaxSlideCollidersPerBake = 8;

        private readonly VoxelWorld _world;
        private readonly MarchingCubes _mesher = new MarchingCubes();
        private readonly VoxelChunkView[] _views;
        private readonly List<int> _dirtyChunks = new List<int>();
        private readonly List<int> _batchChunks = new List<int>();
        // A batch started by ScheduleDirtyChunks, uploaded by Update (or before any other re-mesh).
        private readonly List<int> _scheduledChunks = new List<int>();
        private bool _meshScheduled;

        // Colliders: whose shape changed since its last bake, when that was, and the bake in flight.
        private readonly bool[] _colliderStale;
        private readonly bool[] _colliderUrgent;
        private readonly float[] _colliderBakedAt;
        private readonly List<int> _bakingChunks = new List<int>();
        private readonly List<Vector3> _vertexBuffer = new List<Vector3>();
        private readonly List<int> _indexBuffer = new List<int>();
        private JobHandle _bakeHandle;
        private NativeArray<EntityId> _bakeIds;
        private bool _bakePending;

        /// <param name="parent">The chunk objects go under it, on its layer; vertices are in its local space.</param>
        /// <param name="layerTints">Vertex color per ash layer; null leaves the mesh white.</param>
        public ChunkMeshUpdater(VoxelWorld world, Transform parent, Material material, Color[] layerTints)
        {
            _world = world;
            if (layerTints != null)
            {
                _mesher.SetLayerTints(layerTints);
            }

            _views = new VoxelChunkView[world.ChunkCount];
            _colliderStale = new bool[world.ChunkCount];
            _colliderUrgent = new bool[world.ChunkCount];
            _colliderBakedAt = new float[world.ChunkCount];
            for (int i = 0; i < _views.Length; i++)
            {
                Vector3Int coord = world.ChunkCoord(i);
                GameObject chunkObject = new GameObject($"Chunk {coord.x},{coord.y},{coord.z}",
                    typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider), typeof(VoxelChunkView));
                chunkObject.layer = parent.gameObject.layer;
                chunkObject.transform.SetParent(parent, false);
                VoxelChunkView view = chunkObject.GetComponent<VoxelChunkView>();
                view.Initialize(material);
                _views[i] = view;
            }
        }

        /// <summary>
        /// Main-thread milliseconds of the last re-mesh: copying the samples and meshing (waiting for the workers) plus the
        /// upload; for a scheduled re-mesh only the copying, the upload is <see cref="LastUploadMilliseconds"/>.
        /// </summary>
        public float LastMeshMilliseconds { get; private set; }

        /// <summary>Main-thread milliseconds of uploading the last scheduled re-mesh (<see cref="ScheduleDirtyChunks"/>).</summary>
        public float LastUploadMilliseconds { get; private set; }

        /// <summary>Counts uploads of scheduled re-meshes, so the performance overlay can tell a new one happened.</summary>
        public int UploadCount { get; private set; }

        public int LastMeshedChunkCount { get; private set; }

        /// <summary>Main-thread milliseconds of the last collider round: copying shapes and starting their bake.</summary>
        public float LastColliderMilliseconds { get; private set; }

        /// <summary>Counts collider rounds, so the performance overlay can tell a new one happened.</summary>
        public int ColliderRoundCount { get; private set; }

        /// <summary>
        /// Meshes every changed chunk and uploads the meshes this frame, so a hole appears the moment it's dug; their
        /// colliders bake in the background as soon as the worker threads are free of the previous bake.
        /// </summary>
        /// <param name="waitForColliders">Finish the colliders before returning (start-up, loading a save).</param>
        public void RebuildDirtyChunks(bool waitForColliders = false)
        {
            // An older scheduled batch goes up first, so it can never overwrite these newer meshes.
            FinishScheduled();
            _world.TakeDirtyChunks(_dirtyChunks);
            if (_dirtyChunks.Count == 0)
            {
                return;
            }

            using (MeshMarker.Auto())
            {
                long started = TimeMeasure.Now();
                // In batches: meshing all chunks at once (start-up) would hold tens of MB of job memory at the same time.
                for (int first = 0; first < _dirtyChunks.Count; first += MeshBatchSize)
                {
                    _batchChunks.Clear();
                    int end = Math.Min(first + MeshBatchSize, _dirtyChunks.Count);
                    for (int i = first; i < end; i++)
                    {
                        _batchChunks.Add(_dirtyChunks[i]);
                    }

                    _mesher.PolygonizeChunks(_world, _batchChunks);
                    Upload(_batchChunks, true);
                    _mesher.ReleaseResults();
                }

                LastMeshMilliseconds = TimeMeasure.MillisecondsSince(started);
                LastMeshedChunkCount = _dirtyChunks.Count;
            }

            if (waitForColliders)
            {
                BakeAllCollidersNow();
            }
            else
            {
                UpdateColliders();
            }
        }

        /// <summary>
        /// For sliding ash: starts meshing a few changed chunks on worker threads and returns at once. They are uploaded
        /// by the next <see cref="Update"/>, which keeps going through the rest, a few per frame.
        /// </summary>
        public void ScheduleDirtyChunks()
        {
            if (_meshScheduled)
            {
                // The batch in flight goes up next frame; Update starts the next one after it.
                return;
            }

            _world.TakeDirtyChunks(_scheduledChunks, MaxSlideChunksPerFrame);
            if (_scheduledChunks.Count == 0)
            {
                return;
            }

            using (MeshMarker.Auto())
            {
                long started = TimeMeasure.Now();
                _mesher.ScheduleChunks(_world, _scheduledChunks);
                _meshScheduled = true;
                LastMeshMilliseconds = TimeMeasure.MillisecondsSince(started);
                LastMeshedChunkCount = _scheduledChunks.Count;
            }
        }

        /// <summary>
        /// Uploads a scheduled batch once its workers are done, starts the next one if chunks are
        /// still waiting, and keeps the colliders coming.
        /// </summary>
        public void Update()
        {
            // Never waited for: the workers may be busy baking colliders, and waiting for them would stall the frame.
            if (_meshScheduled && _mesher.IsBatchDone)
            {
                FinishScheduled();
                if (_world.HasDirtyChunks)
                {
                    ScheduleDirtyChunks();
                }
            }

            UpdateColliders();
        }

        /// <summary>The object showing a chunk (its mesh, on the terrain's transform).</summary>
        public VoxelChunkView ViewOf(int chunk)
        {
            return _views[chunk];
        }

        public void Dispose()
        {
            if (_bakePending)
            {
                _bakeHandle.Complete();
                _bakeIds.Dispose();
                _bakePending = false;
            }

            _mesher.Dispose();
        }

        private void FinishScheduled()
        {
            if (!_meshScheduled)
            {
                return;
            }

            _meshScheduled = false;
            using (MeshMarker.Auto())
            {
                long started = TimeMeasure.Now();
                _mesher.CompleteChunks();
                Upload(_scheduledChunks, false);
                _mesher.ReleaseResults();
                LastUploadMilliseconds = TimeMeasure.MillisecondsSince(started);
                UploadCount++;
            }
        }

        // The mesher's results for these chunks, in order, go up for drawing; their colliders are now out of date.
        private void Upload(List<int> chunks, bool urgent)
        {
            for (int i = 0; i < chunks.Count; i++)
            {
                int chunk = chunks[i];
                _views[chunk].Apply(_mesher.ResultVertices(i), _mesher.ResultNormals(i), _mesher.ResultColors(i), _mesher.ResultIndices(i));
                _colliderStale[chunk] = true;
                _colliderUrgent[chunk] |= urgent;
            }
        }

        // Swaps in a finished bake, then starts the next one for colliders that are due. Never waits for the workers.
        private void UpdateColliders()
        {
            if (_bakePending)
            {
                if (!_bakeHandle.IsCompleted)
                {
                    return;
                }

                FinishBake();
            }

            StartBake(false);
        }

        private void BakeAllCollidersNow()
        {
            if (_bakePending)
            {
                _bakeHandle.Complete();
                FinishBake();
            }

            StartBake(true);
            if (_bakePending)
            {
                _bakeHandle.Complete();
                FinishBake();
            }
        }

        private void StartBake(bool everything)
        {
            float now = Time.unscaledTime;
            _bakingChunks.Clear();
            for (int chunk = 0; chunk < _colliderStale.Length; chunk++)
            {
                if (!_colliderStale[chunk])
                {
                    continue;
                }

                // A dig's colliders go first and all at once; sliding ash waits its interval and takes turns.
                bool due = everything || _colliderUrgent[chunk] || now - _colliderBakedAt[chunk] >= SlideColliderInterval;
                if (due && (everything || _colliderUrgent[chunk] || _bakingChunks.Count < MaxSlideCollidersPerBake))
                {
                    _bakingChunks.Add(chunk);
                }
            }

            if (_bakingChunks.Count == 0)
            {
                return;
            }

            using (ColliderMarker.Auto())
            {
                long started = TimeMeasure.Now();
                int withTriangles = 0;
                foreach (int chunk in _bakingChunks)
                {
                    _views[chunk].CopyShapeToCollider(_vertexBuffer, _indexBuffer);
                    _colliderStale[chunk] = false;
                    _colliderUrgent[chunk] = false;
                    if (_views[chunk].ColliderHasTriangles)
                    {
                        withTriangles++;
                    }
                }

                _bakeIds = new NativeArray<EntityId>(withTriangles, Allocator.Persistent);
                int next = 0;
                foreach (int chunk in _bakingChunks)
                {
                    if (_views[chunk].ColliderHasTriangles)
                    {
                        _bakeIds[next++] = _views[chunk].ColliderMeshId;
                    }
                }

                _bakeHandle = new BakeMeshJob { MeshIds = _bakeIds }.Schedule(_bakeIds.Length, 1);
                // Start the workers now rather than whenever Unity next flushes its job queue.
                JobHandle.ScheduleBatchedJobs();
                _bakePending = true;
                LastColliderMilliseconds = TimeMeasure.MillisecondsSince(started);
                ColliderRoundCount++;
            }
        }

        private void FinishBake()
        {
            _bakeHandle.Complete();
            _bakeIds.Dispose();
            _bakePending = false;
            float now = Time.unscaledTime;
            foreach (int chunk in _bakingChunks)
            {
                _views[chunk].RefreshCollider();
                _colliderBakedAt[chunk] = now;
            }

            _bakingChunks.Clear();
        }
    }
}
