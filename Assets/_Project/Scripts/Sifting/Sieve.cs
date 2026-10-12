using Cinderbird.Embers;
using UnityEngine;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Owns the sieve's <see cref="SieveContents"/> so other components can reach it through the Inspector.
    /// </summary>
    public sealed class Sieve : MonoBehaviour, IEmberHolder
    {
        public SieveContents Contents { get; } = new SieveContents();

        public IEmberSource Source => Contents;
    }
}
