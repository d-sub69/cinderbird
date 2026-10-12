using System.Collections.Generic;
using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>
    /// A component the player can look at and pour embers into (the nest; a placed basket in step C2).
    /// Found by the interaction raycast on its collider or a parent.
    /// </summary>
    public interface IPourTarget
    {
        /// <summary>For prompts, e.g. "the nest".</summary>
        string DisplayName { get; }

        IEmberSink Sink { get; }

        /// <summary>Called after a pour with the embers that went in and where they came from, for visuals.</summary>
        void OnPoured(IReadOnlyList<Ember> embers, Vector3 fromPosition);
    }
}
