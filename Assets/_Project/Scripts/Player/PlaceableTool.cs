using UnityEngine;

namespace Cinderbird.Player
{
    /// <summary>
    /// A tool that can be set down in the world and picked up again (the basket). The same object moves
    /// between the hands and the world, so whatever it carries (embers, glow) comes along.
    /// </summary>
    public sealed class PlaceableTool : MonoBehaviour
    {
        // Placed tools belong to the world, so the pile and walls hide them like anything else.
        private const int WorldLayer = 0;

        [SerializeField] private ToolDefinition _tool;
        [Tooltip("Trigger the look raycast hits while the tool is placed; disabled while held.")]
        [SerializeField] private Collider _worldCollider;
        [Tooltip("Preview shown where the tool would land; a separate scene object, hidden by default.")]
        [SerializeField] private GameObject _ghost;
        [Tooltip("Height of this object's origin above the ground when standing upright, in meters.")]
        [SerializeField, Min(0f)] private float _placedHeightOffset = 0.1f;

        private Transform _heldParent;
        private Vector3 _heldLocalPosition;
        private Quaternion _heldLocalRotation;
        private int _heldLayer;

        public ToolDefinition Tool => _tool;
        public GameObject Ghost => _ghost;
        public float PlacedHeightOffset => _placedHeightOffset;
        public bool IsPlaced { get; private set; }

        private void Awake()
        {
            if (_tool == null || _worldCollider == null)
            {
                Debug.LogError($"{nameof(PlaceableTool)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            // The pose in the hands as set up in the Editor; picking up returns to it.
            _heldParent = transform.parent;
            _heldLocalPosition = transform.localPosition;
            _heldLocalRotation = transform.localRotation;
            _heldLayer = gameObject.layer;
            _worldCollider.enabled = false;
        }

        /// <param name="groundPoint">Where the tool's base touches the ground.</param>
        public void PlaceAt(Vector3 groundPoint, Quaternion rotation)
        {
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(groundPoint + Vector3.up * _placedHeightOffset, rotation);
            SetLayerRecursively(transform, WorldLayer);
            _worldCollider.enabled = true;
            gameObject.SetActive(true);
            IsPlaced = true;
        }

        /// <summary>
        /// Moves the tool back to its held pose, hidden; the tool belt shows it when the swap reaches it.
        /// </summary>
        public void ReturnToHands()
        {
            _worldCollider.enabled = false;
            transform.SetParent(_heldParent, false);
            transform.localPosition = _heldLocalPosition;
            transform.localRotation = _heldLocalRotation;
            SetLayerRecursively(transform, _heldLayer);
            gameObject.SetActive(false);
            IsPlaced = false;
        }

        // Children include embers spawned at runtime, so this walks the live hierarchy each time.
        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayerRecursively(root.GetChild(i), layer);
            }
        }
    }
}
