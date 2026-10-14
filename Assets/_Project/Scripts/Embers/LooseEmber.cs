using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>
    /// An ember lying loose on a surface, with an id that stays the same while it lies there. Once found it is never
    /// buried again: it rides the ash under it, up or down, until it is collected.
    /// </summary>
    public readonly struct LooseEmber
    {
        /// <summary>A <see cref="Rest"/> not measured yet (just laid, falling, or loaded from a save).</summary>
        public const float UnknownRest = float.NaN;

        public LooseEmber(int id, Ember ember, Vector3 cameFrom, float rest = UnknownRest)
        {
            Id = id;
            Ember = ember;
            CameFrom = cameFrom;
            Rest = rest;
        }

        public int Id { get; }

        /// <summary>The ember; its position is in the terrain's local space.</summary>
        public Ember Ember { get; }

        /// <summary>Where it was when it started lying loose (e.g. its spot in the ash), so views can show it move out.</summary>
        public Vector3 CameFrom { get; }

        /// <summary>
        /// How high its center sits above the ash surface under it, in meters: 0 = half sunk, its radius = lying on
        /// top. Its height follows the surface plus this, so it rides ash that slides or piles up; it only ever grows,
        /// so the ember never sinks deeper. NaN until measured (<see cref="HasRest"/>).
        /// </summary>
        public float Rest { get; }

        public bool HasRest => !float.IsNaN(Rest);

        public LooseEmber MovedTo(Vector3 position)
        {
            return new LooseEmber(Id, new Ember(position, Ember.TierIndex, Ember.Layer), CameFrom, Rest);
        }

        public LooseEmber WithRest(float rest)
        {
            return new LooseEmber(Id, Ember, CameFrom, rest);
        }
    }
}
