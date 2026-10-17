using System;
using Cinderbird.Nest;
using UnityEngine;

namespace Cinderbird.Valley
{
    /// <summary>
    /// Thin adapter: the valley below thaws with the nest's Heat. Each stage reached (<see cref="ValleyThawStage"/>) melts
    /// a patch of snow to earth and grass, sets a stretch of the river running and lights windows; one reached in play
    /// spreads over a few seconds, one that is simply there (a loaded save) shows at once. The colors are the meshes' own
    /// vertex colors, recolored only while a stage spreads.
    /// </summary>
    public sealed class ValleyThaw : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private ValleySettings _settings;
        [SerializeField] private NestHeat _heat;
        [SerializeField] private MeshFilter _ground;
        [SerializeField] private MeshFilter _river;
        [Tooltip("The village's windows, in order (the stages name them by this order).")]
        [SerializeField] private Renderer[] _windows = new Renderer[0];

        private Mesh _groundMesh;
        private Mesh _riverMesh;
        private Color[] _groundSnow;
        private Color[] _groundThawed;
        private Vector3[] _groundCentroids;
        private Color[] _groundColors;
        private float[] _riverAlong;
        private Color[] _riverColors;
        private MaterialPropertyBlock _block;
        private int _stage = -1;
        private float _growth = 1f;
        private bool _dirty = true;
        private bool _reachedInPlay;

        /// <summary>A short line for the HUD when a stage comes.</summary>
        public event Action<string> Notice;

        private void Awake()
        {
            if (_settings == null || _heat == null || _ground == null || _river == null)
            {
                Debug.LogError($"{nameof(ValleyThaw)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _block = new MaterialPropertyBlock();
            PrepareGround();
            PrepareRiver();
        }

        private void OnEnable()
        {
            if (_heat != null)
            {
                _heat.LevelReached += OnLevelReached;
            }
        }

        private void OnDisable()
        {
            if (_heat != null)
            {
                _heat.LevelReached -= OnLevelReached;
            }
        }

        private void OnLevelReached(int level)
        {
            _reachedInPlay = true;
        }

        private void Update()
        {
            int stage = ValleyThawing.StageFor(_settings.Stages, _heat.Level);
            if (stage != _stage)
            {
                ChangeStage(stage);
            }

            if (_growth < 1f)
            {
                _growth = Mathf.Min(1f, _growth + Time.deltaTime / _settings.TransitionSeconds);
                _dirty = true;
            }

            if (_dirty)
            {
                _dirty = false;
                Recolor(Mathf.SmoothStep(0f, 1f, _growth));
            }
        }

        // A stage reached in play spreads; one that is simply there (a fresh game, a loaded save) shows at once.
        private void ChangeStage(int stage)
        {
            bool spreads = _stage >= 0 && stage > _stage && _reachedInPlay;
            _reachedInPlay = false;
            _stage = stage;
            _growth = spreads ? 0f : 1f;
            _dirty = true;
            if (spreads)
            {
                string message = _settings.Stages[stage - 1].Message;
                if (!string.IsNullOrEmpty(message))
                {
                    Notice?.Invoke(message);
                }
            }
        }

        private void Recolor(float growth)
        {
            ValleyThawStage[] stages = _settings.Stages;
            int reached = Mathf.Max(0, _stage);
            for (int t = 0; t < _groundCentroids.Length; t++)
            {
                // One amount per triangle, so it stays flat-shaded while it thaws.
                float thaw = ValleyThawing.GroundAt(stages, reached, growth, _groundCentroids[t]);
                for (int k = 0; k < 3; k++)
                {
                    int v = t * 3 + k;
                    _groundColors[v] = Color.Lerp(_groundSnow[v], _groundThawed[v], thaw);
                }
            }

            _groundMesh.SetColors(_groundColors);

            for (int v = 0; v < _riverColors.Length; v++)
            {
                _riverColors[v] = Color.Lerp(_settings.Ice, _settings.Water, ValleyThawing.RiverAt(stages, reached, growth, _riverAlong[v]));
            }

            _riverMesh.SetColors(_riverColors);

            for (int w = 0; w < _windows.Length; w++)
            {
                if (_windows[w] == null)
                {
                    continue;
                }

                _block.SetColor(EmissionColorId, _settings.WindowLit * ValleyThawing.WindowAt(stages, reached, growth, w));
                _windows[w].SetPropertyBlock(_block);
            }
        }

        // The ground's own colors are the snow; what each triangle thaws to (earth or grass) is picked once, by place.
        private void PrepareGround()
        {
            _groundMesh = _ground.mesh;
            Vector3[] vertices = _groundMesh.vertices;
            _groundSnow = _groundMesh.colors;
            if (_groundSnow.Length != vertices.Length)
            {
                _groundSnow = new Color[vertices.Length];
                for (int i = 0; i < _groundSnow.Length; i++)
                {
                    _groundSnow[i] = _settings.Snow;
                }
            }

            int triangles = vertices.Length / 3;
            _groundCentroids = new Vector3[triangles];
            _groundThawed = new Color[vertices.Length];
            _groundColors = new Color[vertices.Length];
            Matrix4x4 toWorld = _ground.transform.localToWorldMatrix;
            for (int t = 0; t < triangles; t++)
            {
                Vector3 centroid = toWorld.MultiplyPoint3x4((vertices[t * 3] + vertices[t * 3 + 1] + vertices[t * 3 + 2]) / 3f);
                _groundCentroids[t] = centroid;
                float patch = Mathf.PerlinNoise(centroid.x * 0.05f, centroid.z * 0.05f);
                Color thawed = Color.Lerp(_settings.Earth, _settings.Grass, Mathf.SmoothStep(0.35f, 0.65f, patch));
                float tint = 0.9f + 0.2f * Mathf.PerlinNoise(centroid.x * 0.7f, centroid.z * 0.7f);
                thawed = new Color(thawed.r * tint, thawed.g * tint, thawed.b * tint, 1f);
                for (int k = 0; k < 3; k++)
                {
                    _groundThawed[t * 3 + k] = thawed;
                }
            }
        }

        private void PrepareRiver()
        {
            _riverMesh = _river.mesh;
            Vector2[] uv = _riverMesh.uv;
            _riverAlong = new float[uv.Length];
            for (int i = 0; i < uv.Length; i++)
            {
                _riverAlong[i] = uv[i].x;
            }

            _riverColors = new Color[uv.Length];
        }
    }
}
