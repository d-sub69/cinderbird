using System;
using Cinderbird.Offerings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cinderbird.UI
{
    /// <summary>
    /// One offering in the shrine panel: its name and lore once picked up ("???" before), where it is, and what can be
    /// done with it: look closer, place on the shelf or take off, sell, buy back.
    /// </summary>
    public sealed class ShrineRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _lore;
        [SerializeField] private Button _lookButton;
        [SerializeField] private Button _shelfButton;
        [SerializeField] private TMP_Text _shelfLabel;
        [SerializeField] private Button _sellButton;
        [SerializeField] private TMP_Text _sellLabel;

        private Shrine _shrine;
        private int _index;

        public void Bind(Shrine shrine, int index, Action changed, Action<int> lookCloser)
        {
            _shrine = shrine;
            _index = index;
            _lookButton.onClick.RemoveAllListeners();
            _shelfButton.onClick.RemoveAllListeners();
            _sellButton.onClick.RemoveAllListeners();
            _lookButton.onClick.AddListener(() => lookCloser(_index));
            _shelfButton.onClick.AddListener(() =>
            {
                if (!_shrine.Place(_index))
                {
                    _shrine.TakeOff(_index);
                }

                changed();
            });
            _sellButton.onClick.AddListener(() =>
            {
                if (!_shrine.Sell(_index))
                {
                    _shrine.BuyBack(_index);
                }

                changed();
            });
        }

        public void Refresh()
        {
            if (_shrine == null)
            {
                return;
            }

            OfferingDefinition offering = _shrine.Catalog[_index];
            OfferingState state = _shrine.Collection.StateOf(_index);
            if (state == OfferingState.Buried || state == OfferingState.OnPile)
            {
                _name.text = "???";
                _lore.text = state == OfferingState.OnPile
                    ? "Uncovered, lying in the ash: pick it up where it lies."
                    : "Still somewhere in the ash.";
                _lookButton.gameObject.SetActive(false);
                _shelfButton.gameObject.SetActive(false);
                _sellButton.gameObject.SetActive(false);
                return;
            }

            _name.text = state == OfferingState.Sold ? $"{offering.DisplayName} (sold)" : offering.DisplayName;
            _lore.text = offering.Lore;
            _lookButton.gameObject.SetActive(state != OfferingState.Sold);
            _shelfButton.gameObject.SetActive(state != OfferingState.Sold);
            _shelfLabel.text = state == OfferingState.OnShelf ? "Take off shelf" : "Place on shelf";
            _sellButton.gameObject.SetActive(state != OfferingState.OnShelf);
            _sellLabel.text = state == OfferingState.Sold ? $"Buy back {offering.SellValue}" : $"Sell {offering.SellValue}";
            _sellButton.interactable = state != OfferingState.Sold || _shrine.Wallet.Currencies.Glow >= offering.SellValue;
        }
    }
}
