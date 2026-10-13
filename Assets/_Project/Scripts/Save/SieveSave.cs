using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cinderbird.Save
{
    [Serializable]
    public sealed class SieveSave
    {
        [SerializeField] private float _ashVolume;
        [SerializeField] private float _loadVolume;
        [SerializeField] private Color _ashColor = Color.grey;
        [SerializeField] private Color _dustColor = Color.grey;
        [SerializeField] private List<EmberRecord> _embers = new List<EmberRecord>();

        public float AshVolume { get => _ashVolume; set => _ashVolume = value; }
        public float LoadVolume { get => _loadVolume; set => _loadVolume = value; }
        public Color AshColor { get => _ashColor; set => _ashColor = value; }
        public Color DustColor { get => _dustColor; set => _dustColor = value; }
        public List<EmberRecord> Embers => _embers;
    }
}
