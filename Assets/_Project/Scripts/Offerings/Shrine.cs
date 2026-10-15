using System;
using System.Collections.Generic;
using Cinderbird.Economy;
using UnityEngine;

namespace Cinderbird.Offerings
{
    /// <summary>
    /// Owns the player's <see cref="OfferingCollection"/> and the shrine's rules around it: offerings uncovered on the
    /// pile and where they lie, picking them up into the collection, placing on the shelf, selling for Glow and buying
    /// back for the same, and which complete sets give their bonus (<see cref="ActiveSets"/>, applied to the wallet's
    /// feeding bonuses whenever the shelf changes).
    /// </summary>
    public sealed class Shrine : MonoBehaviour
    {
        [SerializeField] private OfferingCatalog _catalog;
        [SerializeField] private ShrineSettings _settings;
        [SerializeField] private Wallet _wallet;

        private OfferingCollection _collection;
        private Vector3[] _pilePositions;
        private ActiveSets _activeSets;
        private List<int>[] _setMembers;
        private bool[] _complete;
        private bool[] _wasComplete;

        /// <summary>Fired when a dig uncovers an offering (it now lies on the pile).</summary>
        public event Action<OfferingDefinition> Uncovered;

        /// <summary>Fired when the last offering of a set goes on the shelf (not when a save restores a complete set).</summary>
        public event Action<OfferingSet> SetCompleted;

        /// <summary>Fired whenever which sets are complete or active may have changed.</summary>
        public event Action SetsChanged;

        public OfferingCatalog Catalog => _catalog;

        public Wallet Wallet => _wallet;

        /// <summary>Created on first access: other objects' OnEnable can run before this Awake.</summary>
        public OfferingCollection Collection
        {
            get
            {
                if (_collection == null)
                {
                    int count = _catalog != null ? _catalog.Count : 0;
                    _collection = new OfferingCollection(count);
                    _pilePositions = new Vector3[count];
                }

                return _collection;
            }
        }

        private ActiveSets Active => _activeSets ??= new ActiveSets(_settings != null ? _settings.ActiveSetSlots : 1);

        private void Awake()
        {
            if (_catalog == null || _settings == null || _wallet == null)
            {
                Debug.LogError($"{nameof(Shrine)} on '{name}' has unassigned references.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            Collection.Changed += OnCollectionChanged;
            RefreshSets(false);
        }

        private void OnDisable()
        {
            Collection.Changed -= OnCollectionChanged;
        }

        /// <summary>Terrain-local spot where an offering lies on the pile (meaningful while it's <see cref="OfferingState.OnPile"/>).</summary>
        public Vector3 PilePositionOf(int index)
        {
            Vector3[] positions = PilePositions;
            return positions[index];
        }

        public void SetPilePosition(int index, Vector3 position)
        {
            PilePositions[index] = position;
        }

        /// <summary>A dig reached a buried offering: it now lies at this terrain-local spot.</summary>
        public bool Uncover(int index, Vector3 position)
        {
            if (!Collection.Uncover(index))
            {
                return false;
            }

            PilePositions[index] = position;
            Uncovered?.Invoke(_catalog[index]);
            return true;
        }

        /// <summary>The player picked it up from the pile; it goes into the shrine's collection.</summary>
        public bool PickUp(int index)
        {
            return Collection.PickUp(index);
        }

        public bool Place(int index)
        {
            return Collection.Place(index);
        }

        public bool TakeOff(int index)
        {
            return Collection.TakeOff(index);
        }

        /// <summary>Sells an offering from the collection (not from the shelf) for its value in Glow.</summary>
        public bool Sell(int index)
        {
            if (!Collection.Sell(index))
            {
                return false;
            }

            _wallet.Currencies.Add(_catalog[index].SellValue, 0);
            return true;
        }

        /// <summary>Buys a sold offering back for what it sold for.</summary>
        public bool BuyBack(int index)
        {
            if (Collection.StateOf(index) != OfferingState.Sold || !_wallet.Currencies.TrySpend(_catalog[index].SellValue))
            {
                return false;
            }

            return Collection.BuyBack(index);
        }

        public int CountOnShelf(OfferingSet set)
        {
            return Collection.CountOnShelf(MembersOf(set));
        }

        public bool IsComplete(OfferingSet set)
        {
            return Collection.AllOnShelf(MembersOf(set));
        }

        public bool IsActive(OfferingSet set)
        {
            return Active.IsActive(Array.IndexOf(_catalog.Sets, set));
        }

        public int ActiveSetSlots => Active.Slots;

        /// <summary>Makes a complete set the one giving its bonus (replacing another if every slot is taken).</summary>
        public bool Activate(OfferingSet set)
        {
            FillComplete();
            if (!Active.Activate(Array.IndexOf(_catalog.Sets, set), _complete))
            {
                return false;
            }

            ApplyBonuses();
            SetsChanged?.Invoke();
            return true;
        }

        /// <summary>The ids of the sets giving their bonus, for saving.</summary>
        public void CopyActiveSetIds(List<string> output)
        {
            output.Clear();
            foreach (int set in Active.Active)
            {
                output.Add(_catalog.Sets[set].Id);
            }
        }

        /// <summary>Puts back the active sets from a save (after the collection): only complete ones, up to the slots.</summary>
        public void RestoreActiveSets(IReadOnlyList<string> ids)
        {
            List<int> indices = new List<int>();
            foreach (string id in ids)
            {
                int index = Array.FindIndex(_catalog.Sets, set => set != null && set.Id == id);
                if (index >= 0)
                {
                    indices.Add(index);
                }
            }

            FillComplete();
            Active.Restore(indices, _complete);
            ApplyBonuses();
            SetsChanged?.Invoke();
        }

        // index -1 = a save was restored: bonuses follow, but nothing is announced.
        private void OnCollectionChanged(int index)
        {
            RefreshSets(index >= 0);
        }

        private void RefreshSets(bool announce)
        {
            if (_catalog == null)
            {
                return;
            }

            FillComplete();
            OfferingSet[] sets = _catalog.Sets;
            _wasComplete ??= new bool[sets.Length];
            int justCompleted = -1;
            for (int i = 0; i < sets.Length; i++)
            {
                if (_complete[i] && !_wasComplete[i] && announce)
                {
                    justCompleted = i;
                    SetCompleted?.Invoke(sets[i]);
                }

                _wasComplete[i] = _complete[i];
            }

            Active.Update(_complete, justCompleted);
            ApplyBonuses();
            SetsChanged?.Invoke();
        }

        private void FillComplete()
        {
            OfferingSet[] sets = _catalog.Sets;
            if (_complete == null || _complete.Length != sets.Length)
            {
                _complete = new bool[sets.Length];
            }

            for (int i = 0; i < sets.Length; i++)
            {
                _complete[i] = sets[i] != null && IsComplete(sets[i]);
            }
        }

        private void ApplyBonuses()
        {
            if (_wallet == null)
            {
                return;
            }

            OfferingSet[] sets = _catalog.Sets;
            for (int i = 0; i < sets.Length; i++)
            {
                OfferingSet set = sets[i];
                if (set == null)
                {
                    continue;
                }

                float percent = Active.IsActive(i) ? set.Percent : 0f;
                _wallet.Bonuses.Set("set:" + set.Id, set.Bonus == BonusKind.Glow ? percent : 0f, set.Bonus == BonusKind.Heat ? percent : 0f);
            }
        }

        private Vector3[] PilePositions
        {
            get
            {
                _ = Collection;
                return _pilePositions;
            }
        }

        private List<int> MembersOf(OfferingSet set)
        {
            _setMembers ??= new List<int>[_catalog.Sets.Length];
            int setIndex = Array.IndexOf(_catalog.Sets, set);
            if (setIndex < 0)
            {
                return new List<int>();
            }

            if (_setMembers[setIndex] == null)
            {
                List<int> members = new List<int>();
                foreach (OfferingDefinition member in set.Members)
                {
                    members.Add(_catalog.IndexOf(member));
                }

                _setMembers[setIndex] = members;
            }

            return _setMembers[setIndex];
        }
    }
}
