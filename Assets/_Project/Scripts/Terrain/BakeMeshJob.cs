using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Pre-cooks collision data for many meshes on worker threads, so assigning them to MeshColliders afterwards
    /// is nearly free. Not Burst-compiled: it calls into Unity's physics.
    /// </summary>
    public struct BakeMeshJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<EntityId> MeshIds;

        public void Execute(int index)
        {
            // Must match the colliders' cooking options; both use the defaults (non-convex).
            Physics.BakeMesh(MeshIds[index], false);
        }
    }
}
