using UnityEngine;

namespace Cinderbird.Valley
{
    /// <summary>The frozen valley below the summit: its landform (placeholder art, built by the setup), its colors, and its thaw stages by Heat level.</summary>
    [CreateAssetMenu(fileName = "ValleySettings", menuName = "Cinderbird/Valley Settings")]
    public sealed class ValleySettings : ScriptableObject
    {
        [Header("Thaw")]
        [Tooltip("In order; each comes with its Heat level.")]
        [SerializeField] private ValleyThawStage[] _stages =
        {
            new ValleyThawStage(2, new Vector3(-30f, 0f, -170f), 55f, new Vector2(0.3f, 0.55f), new[] { 0 },
                "Down in the valley, the first snow melts and a window lights up"),
        };
        [Tooltip("Seconds a stage takes to spread.")]
        [SerializeField, Min(0.1f)] private float _transitionSeconds = 6f;

        [Header("Colors")]
        [SerializeField] private Color _snow = new Color(0.82f, 0.86f, 0.93f);
        [SerializeField] private Color _earth = new Color(0.33f, 0.25f, 0.17f);
        [SerializeField] private Color _grass = new Color(0.22f, 0.36f, 0.16f);
        [SerializeField] private Color _rock = new Color(0.26f, 0.24f, 0.23f);
        [SerializeField] private Color _ice = new Color(0.7f, 0.82f, 0.92f);
        [SerializeField] private Color _water = new Color(0.12f, 0.26f, 0.42f);
        [Tooltip("A lit window (HDR: above 1 glows with the bloom).")]
        [ColorUsage(false, true)] [SerializeField] private Color _windowLit = new Color(2.4f, 1.5f, 0.6f);

        [Header("Landform (used when the setup builds it)")]
        [SerializeField] private Vector3 _center = new Vector3(0f, -35f, -258f);
        [SerializeField] private Vector2 _size = new Vector2(520f, 520f);
        [SerializeField, Min(2)] private int _cells = 90;
        [SerializeField, Min(0f)] private float _hillHeight = 10f;
        [SerializeField, Min(0f)] private float _rimRise = 45f;
        [SerializeField, Min(0.0001f)] private float _noiseScale = 0.012f;
        [SerializeField] private int _seed = 7;
        [SerializeField] private Vector3[] _river =
        {
            new Vector3(-260f, 0f, -120f), new Vector3(-140f, 0f, -150f), new Vector3(-40f, 0f, -200f),
            new Vector3(60f, 0f, -185f), new Vector3(160f, 0f, -240f), new Vector3(260f, 0f, -280f),
        };
        [SerializeField, Min(0.5f)] private float _riverWidth = 9f;
        [SerializeField, Min(0f)] private float _riverDepth = 2.5f;
        [SerializeField] private Vector3 _village = new Vector3(-30f, 0f, -165f);
        [SerializeField, Min(1f)] private float _villageRadius = 28f;
        [SerializeField, Min(1)] private int _houses = 8;

        public ValleyThawStage[] Stages => _stages;
        public float TransitionSeconds => _transitionSeconds;
        public Color Snow => _snow;
        public Color Earth => _earth;
        public Color Grass => _grass;
        public Color Rock => _rock;
        public Color Ice => _ice;
        public Color Water => _water;
        public Color WindowLit => _windowLit;
        public int Houses => _houses;

        public ValleyShape ToShape()
        {
            return new ValleyShape(_center, _size, _cells, _hillHeight, _rimRise, _noiseScale, _seed, _river, _riverWidth, _riverDepth,
                _village, _villageRadius);
        }
    }
}
