using System.Collections.Generic;
using Cinderbird.Embers;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// What one dig grazed or reached without taking it: embers to lay loose and finds (offerings) to show, held until
    /// that dig's hole is open. Each dig has its own, since several holes can be draining or settling at once.
    /// </summary>
    public sealed class DigExposure
    {
        public List<LooseEmber> Embers { get; } = new List<LooseEmber>();

        public List<BuriedFind> Finds { get; } = new List<BuriedFind>();

        public void Clear()
        {
            Embers.Clear();
            Finds.Clear();
        }
    }
}
