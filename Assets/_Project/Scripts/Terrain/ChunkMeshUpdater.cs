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
    /// Turns the terrain's changed chunks into meshes and colliders: one <see cref="VoxelChunkView"/> per chunk,
    /// meshed in parallel on worker threads and uploaded at once, colliders baked in the background and swapped in
    /// within <see cref="MaxColliderLagFrames"/> frames. Owned by <see cref="VoxelTerrain"/>; call
    /// <see cref="Update"/> every frame and <see cref="Dispose"/> when done.
    /// </summary>
    public sealed class ChunkMeshUpdater : IDisposable
    {
        // Shows up by name in Unity's Profiler, so slow frames can be traced to meshing.
        private static readonly ProfilerMarker MeshMarker = new ProfilerMarker("Cinderbird.Mesh");

        // Enough to keep all worker threads busy; a dig touches at most 8 chunks, so only start-up is split.
        private const int MeshBatchSize = 32;

        // If the bake hasn't reported done by then, wait for it, so a collider is never more than this many frames late.
        private const int MaxColliderLagFrames = 2;

        private readonly VoxelWorld _world;
        private readonly MarchingCubes _mesher = new MarchingCubes();
        private readonly VoxelChunkView[] _views;
        private readonly List<int> _dirtyChunks = new List<int>();
        private readonly List<int> _batchChunks = new List<int>();
        private readonly List<int> _bakedChunks = new List<int>();
        private readonly List<int> _pendingColliderChunks = new List<int>();
        // A batch started by ScheduleDirtyChunks, uploaded by Update (or before any other re-mesh).
        private readonly List<int> _scheduledChunks = new List<int>();
        private bool _meshScheduled;
        private int _scheduledFrame;
        private JobHandle _bakeHandle;
        private NativeArray<EntityId> _bakeIds;
        private bool _bakePending;
        private int _bakeStartFrame;

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

        /// <summary>
        /// Meshes every changed chunk and uploads the meshes this frame, so a hole appears the moment it's dug; then
        /// starts baking their colliders in parallel in the background.
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
                CompleteColliderBake();
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
                    for (int i = 0; i < _batchChunks.Count; i++)
                    {
                        _views[_batchChunks[i]].Apply(_mesher.ResultVertices(i), _mesher.ResultNormals(i), _mesher.ResultColors(i), _mesher.ResultIndices(i));
                    }

                    _mesher.ReleaseResults();
                }

                StartColliderBake(_dirtyChunks);
                if (waitForColliders)
                {
                    CompleteColliderBake();
                }

                LastMeshMilliseconds = TimeMeasure.MillisecondsSince(started);
                LastMeshedChunkCount = _dirtyChunks.Count;
            }
        }

        /// <summary>
        /// Like <see cref="RebuildDirtyChunks"/>, but only starts the meshing on worker threads: the meshes are uploaded on
        /// the next <see cref="Update"/>, a frame later at most, so the main thread doesn't wait for the workers. For
        /// frequent small changes (sliding ash), where a frame's delay can't be seen.
        /// </summary>
        public void ScheduleDirtyChunks()
        {
            FinishScheduled();
            _world.TakeDirtyChunks(_scheduledChunks);
            if (_scheduledChunks.Count == 0)
            {
                return;
            }

            if (_scheduledChunks.Count > MeshBatchSize)
            {
                // A big change: mark them dirty again and mesh them in batches the usual way.
                for (int i = 0; i < _scheduledChunks.Count; i++)
                {
                    _world.MarkDirty(_scheduledChunks[i]);
                }

                _scheduledChunks.Clear();
                RebuildDirtyChunks();
                return;
            }

            using (MeshMarker.Auto())
            {
                long started = TimeMeasure.Now();
                _mesher.ScheduleChunks(_world, _scheduledChunks);
                _meshScheduled = true;
                _scheduledFrame = Time.frameCount;
                LastMeshMilliseconds = TimeMeasure.MillisecondsSince(started);
                LastMeshedChunkCount = _scheduledChunks.Count;
            }
        }

        /// <summary>
        /// Uploads a scheduled re-mesh once its workers are done (or a frame has passed) and swaps in baked colliders. Baking collision data is the slowest part of re-meshing, and nothing needs it this
        /// very frame: the hole is visible at once and the collider catches up a frame later, which nobody can notice.
        /// </summary>
        public void Update()
        {
            if (_meshScheduled && (_mesher.IsBatchDone || Time.frameCount > _scheduledFrame))
            {
                FinishScheduled();
            }

            if (_bakePending && (_bakeHandle.IsCompleted || Time.frameCount - _bakeStartFrame >= MaxColliderLagFrames))
            {
                CompleteColliderBake();
            }
        }

        public void Dispose()
        {
            CompleteColliderBake();
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
                CompleteColliderBake();
                _mesher.CompleteChunks();
                for (int i = 0; i < _scheduledChunks.Count; i++)
                {
                    _views[_scheduledChunks[i]].Apply(_mesher.ResultVertices(i), _mesher.ResultNormals(i), _mesher.ResultColors(i), _mesher.ResultIndices(i));
                }

                _mesher.ReleaseResults();
                StartColliderBake(_scheduledChunks);
                LastUploadMilliseconds = TimeMeasure.MillisecondsSince(started);
                UploadCount++;
            }
        }

        private void StartColliderBake(List<int> chunks)
        {
            _bakedChunks.Clear();
            _pendingColliderChunks.Clear();
            foreach (int chunk in chunks)
            {
                _pendingColliderChunks.Add(chunk);
                if (_views[chunk].HasTriangles)
                {
                    _bakedChunks.Add(chunk);
                }
            }

            _bakeIds = new NativeArray<EntityId>(_bakedChunks.Count, Allocator.Persistent);
            for (int i = 0; i < _bakedChunks.Count; i++)
            {
                _bakeIds[i] = _views[_bakedChunks[i]].MeshId;
            }

            _bakeHandle = new BakeMeshJob { MeshIds = _bakeIds }.Schedule(_bakeIds.Length, 1);
            // Start the workers now rather than whenever Unity next flushes its job queue.
            JobHandle.ScheduleBatchedJobs();
            _bakeStartFrame = Time.frameCount;
            _bakePending = true;
        }

        // Also called before the next re-mesh: a mesh must not change while its collision data is being baked.
        private void CompleteColliderBake()
        {
            if (!_bakePending)
            {
                return;
            }

            _bakeHandle.Complete();
            _bakeIds.Dispose();
            _bakePending = false;
            foreach (int chunk in _pendingColliderChunks)
            {
                _views[chunk].RefreshCollider();
            }
        }
    }
}
