using System;
using System.Collections.Generic;
using Cinderbird.Embers;
using UnityEngine;

namespace Cinderbird.Save
{
    /// <summary>
    /// One carried ember in a save. <see cref="Ember"/> itself is a readonly struct with properties, which
    /// JsonUtility can't write, so carried embers are copied into these.
    /// </summary>
    [Serializable]
    public sealed class EmberRecord
    {
        [SerializeField] private Vector3 _position;
        [SerializeField] private int _tier;
        [SerializeField] private int _layer;

        public EmberRecord()
        {
        }

        public EmberRecord(Ember ember)
        {
            _position = ember.Position;
            _tier = ember.TierIndex;
            _layer = ember.Layer;
        }

        public Ember ToEmber()
        {
            return new Ember(_position, _tier, (byte)Mathf.Clamp(_layer, 0, byte.MaxValue));
        }

        public static void Fill(List<EmberRecord> records, IReadOnlyList<Ember> embers)
        {
            records.Clear();
            for (int i = 0; i < embers.Count; i++)
            {
                records.Add(new EmberRecord(embers[i]));
            }
        }

        public static List<Ember> ToEmbers(List<EmberRecord> records)
        {
            List<Ember> embers = new List<Ember>(records.Count);
            foreach (EmberRecord record in records)
            {
                if (record != null)
                {
                    embers.Add(record.ToEmber());
                }
            }

            return embers;
        }
    }
}
