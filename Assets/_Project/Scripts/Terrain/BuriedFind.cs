using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Something other than an ember buried in the ash (an offering), as data like embers: an id the game knows it by,
    /// where it lies (terrain-local) and the ash layer it's in (it can't be dug out while that layer is locked).
    /// </summary>
    public readonly struct BuriedFind
    {
        public BuriedFind(int id, Vector3 position, byte layer)
        {
            Id = id;
            Position = position;
            Layer = layer;
        }

        public int Id { get; }
        public Vector3 Position { get; }
        public byte Layer { get; }
    }
}
