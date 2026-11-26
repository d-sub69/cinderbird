using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>A cluster as it was buried: where, how big, in which layer, and how many embers it got.</summary>
    public readonly struct EmberCluster
    {
        public EmberCluster(ushort id, Vector3 center, float radius, byte layer, int embers)
        {
            Id = id;
            Center = center;
            Radius = radius;
            Layer = layer;
            Embers = embers;
        }

        /// <summary>1 and up; embers carry it (0 means sprinkled on its own).</summary>
        public ushort Id { get; }

        /// <summary>Terrain-local.</summary>
        public Vector3 Center { get; }

        public float Radius { get; }

        public byte Layer { get; }

        public int Embers { get; }
    }
}
