using System;
using Cinderbird.Core;
using Cinderbird.Offerings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Cinderbird.UI
{
    /// <summary>
    /// Looking at an offering up close: its model floats from where it lay to the middle of the view, turns slowly, and
    /// turns with the mouse (drag) and comes closer with the wheel, next to a card with its name, lore and set.
    /// <list type="bullet">
    /// <item>Picked up from the pile (<see cref="InspectFound"/>): only "Place in the shrine", which takes it into the
    /// shrine's collection. (Should the game pause meanwhile, it goes back to where it lay, to be picked up again.)</item>
    /// <item>From the shrine panel's "Look closer" (<see cref="InspectOwned"/>): only "Back" (or Esc).</item>
    /// </list>
    /// The game keeps running (<see cref="GamePhase.InMenu"/>), like the shop.
    /// </summary>
    public sealed class OfferingInspectView : MonoBehaviour
    {
        // Seconds the offering takes to float from the pile to the middle of the view.
        private const float LiftSeconds = 0.45f;
        // Degrees per second it turns by itself while not being turned by hand.
        private const float IdleSpin = 20f;
        // Degrees per pixel of mouse drag.
        private const float DragDegreesPerPixel = 0.35f;
        // Seconds after the last drag before it turns by itself again.
        private const float IdleAfterDrag = 1.5f;
        private const float MaxZoom = 0.18f;

        [SerializeField] private PauseController _pause;
        [SerializeField] private Shrine _shrine;
        [Tooltip("Hands over offerings picked up from the pile.")]
        [SerializeField] private OfferingFinder _finder;
        [Tooltip("Where the offering floats while inspected: in front of the camera, on the layer the held-items camera draws.")]
        [SerializeField] private Transform _anchor;
        [Tooltip("How big the offering looks when inspected, in meters across at the anchor.")]
        [SerializeField, Min(0.02f)] private float _viewSize = 0.22f;
        [Tooltip("The card, shown while inspecting.")]
        [SerializeField] private GameObject _card;
        [SerializeField] private TMP_Text _heading;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _lore;
        [SerializeField] private TMP_Text _set;
        [SerializeField] private Button _keepButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private TMP_Text _backLabel;

        private GameObject _shown;
        private Action<bool> _onFoundClosed;
        private Action _onOwnedClosed;
        private bool _ownsPhase;
        private float _openedAt;
        private Vector3 _liftFrom;
        private Quaternion _liftFromRotation;
        private float _lastDragTime = float.MinValue;
        private float _zoom;
        private bool _foundMode;

        public bool IsOpen => _shown != null;

        private void Awake()
        {
            if (_pause == null || _shrine == null || _finder == null || _anchor == null || _card == null || _heading == null || _name == null ||
                _lore == null || _set == null || _keepButton == null || _backButton == null || _backLabel == null)
            {
                Debug.LogError($"{nameof(OfferingInspectView)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _card.SetActive(false);
            _keepButton.onClick.AddListener(() => Close(true));
            _backButton.onClick.AddListener(() => Close(false));
        }

        private void OnEnable()
        {
            _pause.BackPressed += OnBackPressed;
            _pause.PhaseChanged += OnPhaseChanged;
            _finder.PickUpRequested += OnPickUpRequested;
        }

        private void OnDisable()
        {
            _pause.BackPressed -= OnBackPressed;
            _pause.PhaseChanged -= OnPhaseChanged;
            _finder.PickUpRequested -= OnPickUpRequested;
        }

        private void OnPickUpRequested(int index, Vector3 from, Quaternion rotation)
        {
            InspectFound(index, from, rotation, kept =>
            {
                if (kept)
                {
                    _shrine.PickUp(index);
                }
                else
                {
                    _finder.PutBack(index);
                }
            });
        }

        /// <summary>An offering just picked up from the pile; <paramref name="closed"/> says whether the player kept it.</summary>
        /// <param name="from">Where it lay, in the world: it floats from there.</param>
        public void InspectFound(int index, Vector3 from, Quaternion rotation, Action<bool> closed)
        {
            if (IsOpen || !_pause.OpenMenu())
            {
                closed?.Invoke(false);
                return;
            }

            _ownsPhase = true;
            _onFoundClosed = closed;
            _onOwnedClosed = null;
            Open(index, from, rotation, true);
        }

        /// <summary>An offering the player already has, from the shrine panel (already in a menu).</summary>
        public void InspectOwned(int index, Action closed)
        {
            if (IsOpen)
            {
                return;
            }

            _ownsPhase = _pause.OpenMenu();
            _onFoundClosed = null;
            _onOwnedClosed = closed;
            Open(index, _anchor.position, _anchor.rotation, false);
        }

        private void Open(int index, Vector3 from, Quaternion rotation, bool found)
        {
            OfferingDefinition offering = _shrine.Catalog[index];
            _shown = offering.Model != null ? Instantiate(offering.Model) : GameObject.CreatePrimitive(PrimitiveType.Cube);
            _shown.name = offering.DisplayName + " (inspected)";
            // Only looked at: nothing to bump into or aim at.
            foreach (Collider part in _shown.GetComponentsInChildren<Collider>())
            {
                Destroy(part);
            }

            SetLayer(_shown.transform, _anchor.gameObject.layer);
            _shown.transform.SetParent(_anchor, true);
            _shown.transform.SetPositionAndRotation(from, rotation);
            float size = offering.Model != null ? offering.Size : 1f;
            _shown.transform.localScale *= _viewSize / Mathf.Max(0.01f, size);
            _liftFrom = _shown.transform.localPosition;
            _liftFromRotation = _shown.transform.localRotation;
            _openedAt = Time.unscaledTime;
            _lastDragTime = float.MinValue;
            _zoom = 0f;

            _heading.text = found ? "You found an offering" : "From your shrine";
            _name.text = offering.DisplayName;
            _lore.text = offering.Lore;
            _set.text = SetLine(offering);
            // A found offering can only go into the shrine; Back is only for looking again at one already owned.
            _foundMode = found;
            _keepButton.gameObject.SetActive(found);
            _backButton.gameObject.SetActive(!found);
            _card.SetActive(true);
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            Transform shown = _shown.transform;
            float lift = Mathf.Clamp01((Time.unscaledTime - _openedAt) / LiftSeconds);
            float eased = 1f - (1f - lift) * (1f - lift);
            Vector3 rest = new Vector3(0f, 0f, -_zoom);
            if (lift < 1f)
            {
                shown.localPosition = Vector3.Lerp(_liftFrom, rest, eased);
                shown.localRotation = Quaternion.Slerp(_liftFromRotation, Quaternion.identity, eased);
                return;
            }

            shown.localPosition = rest;
            Mouse mouse = Mouse.current;
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (mouse != null && mouse.leftButton.isPressed && !overUi)
            {
                Vector2 delta = mouse.delta.ReadValue();
                shown.Rotate(_anchor.up, -delta.x * DragDegreesPerPixel, Space.World);
                shown.Rotate(_anchor.right, delta.y * DragDegreesPerPixel, Space.World);
                _lastDragTime = Time.unscaledTime;
            }
            else if (Time.unscaledTime - _lastDragTime > IdleAfterDrag)
            {
                shown.Rotate(_anchor.up, IdleSpin * Time.unscaledDeltaTime, Space.World);
            }

            if (mouse != null && !overUi)
            {
                float scroll = mouse.scroll.ReadValue().y;
                _zoom = Mathf.Clamp(_zoom + Mathf.Sign(scroll) * (Mathf.Abs(scroll) > 0f ? 0.02f : 0f), -MaxZoom * 0.5f, MaxZoom);
            }
        }

        private void Close(bool keep)
        {
            if (!IsOpen)
            {
                return;
            }

            Destroy(_shown);
            _shown = null;
            _card.SetActive(false);
            if (_ownsPhase)
            {
                _ownsPhase = false;
                _pause.CloseMenu();
            }

            Action<bool> found = _onFoundClosed;
            Action owned = _onOwnedClosed;
            _onFoundClosed = null;
            _onOwnedClosed = null;
            found?.Invoke(keep && found != null);
            owned?.Invoke();
        }

        // Esc closes a closer look; a just-found offering waits for "Place in the shrine".
        private void OnBackPressed()
        {
            if (!_foundMode)
            {
                Close(false);
            }
        }

        // Pausing (focus lost) puts a just-found offering back on the pile; it can be picked up again after.
        private void OnPhaseChanged(GamePhase phase)
        {
            if (IsOpen && phase == GamePhase.Paused)
            {
                _ownsPhase = false;
                Close(false);
            }
        }

        private string SetLine(OfferingDefinition offering)
        {
            foreach (OfferingSet set in _shrine.Catalog.Sets)
            {
                if (set != null && Array.IndexOf(set.Members, offering) >= 0)
                {
                    return $"Part of \"{set.DisplayName}\" ({_shrine.CountOnShelf(set)}/{set.Members.Length} on the shelf): {set.DescribeBonus()}";
                }
            }

            return string.Empty;
        }

        private static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root)
            {
                SetLayer(child, layer);
            }
        }
    }
}
