using UnityEngine;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Owns the sieve's <see cref="SieveContents"/> so other components can reach it through the Inspector.
    /// </summary>
    public sealed class Sieve : MonoBehaviour
    {
        public SieveContents Contents { get; } = new SieveContents();
    }
}
