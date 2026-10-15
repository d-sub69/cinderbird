using UnityEngine;

namespace Cinderbird.Offerings
{
    /// <summary>
    /// Shows the offerings placed on the shrine shelf: one slot per offering (in catalog order), holding that offering's
    /// own model, sized to fit the slot, while it's on the shelf.
    /// </summary>
    public sealed class ShrineShelfView : MonoBehaviour
    {
        [SerializeField] private Shrine _shrine;
        [Tooltip("One spot per offering, in catalog order: where its model stands on the shelf.")]
        [SerializeField] private Transform[] _slots = new Transform[0];
        [Tooltip("How big an offering looks on the shelf, in meters across.")]
        [SerializeField, Min(0.02f)] private float _slotSize = 0.26f;

        private GameObject[] _models;

        private void Awake()
        {
            if (_shrine == null)
            {
                Debug.LogError($"{nameof(ShrineShelfView)} on '{name}' has no shrine.", this);
                enabled = false;
                return;
            }

            CreateModels();
        }

        private void OnEnable()
        {
            _shrine.Collection.Changed += OnChanged;
            Refresh();
        }

        private void OnDisable()
        {
            _shrine.Collection.Changed -= OnChanged;
        }

        private void OnChanged(int index)
        {
            Refresh();
        }

        // Made once, hidden; only shown or hidden afterwards.
        private void CreateModels()
        {
            OfferingCatalog catalog = _shrine.Catalog;
            _models = new GameObject[_slots.Length];
            for (int i = 0; i < _slots.Length && i < catalog.Count; i++)
            {
                OfferingDefinition offering = catalog[i];
                if (_slots[i] == null || offering == null || offering.Model == null)
                {
                    continue;
                }

                GameObject model = Instantiate(offering.Model, _slots[i]);
                model.name = offering.DisplayName;
                // On display only: nothing to bump into or aim at (the shelf itself is what the player looks at).
                foreach (Collider part in model.GetComponentsInChildren<Collider>())
                {
                    Destroy(part);
                }

                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * (_slotSize / Mathf.Max(0.01f, offering.Size));
                model.SetActive(false);
                _models[i] = model;
            }
        }

        private void Refresh()
        {
            if (_models == null)
            {
                return;
            }

            OfferingCollection collection = _shrine.Collection;
            for (int i = 0; i < _models.Length; i++)
            {
                if (_models[i] != null)
                {
                    _models[i].SetActive(i < collection.Count && collection.StateOf(i) == OfferingState.OnShelf);
                }
            }
        }
    }
}
