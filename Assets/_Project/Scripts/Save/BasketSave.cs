using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cinderbird.Save
{
    /// <summary>The basket's embers; where the basket is lives in <see cref="ToolsSave"/>.</summary>
    [Serializable]
    public sealed class BasketSave
    {
        [SerializeField] private List<EmberRecord> _embers = new List<EmberRecord>();

        public List<EmberRecord> Embers => _embers;
    }
}
