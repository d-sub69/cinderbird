using System.Collections.Generic;
using Cinderbird.Core;
using Cinderbird.Terrain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cinderbird.Embers
{
    /// <summary>
    /// Testing aid (Test Mode → Show All Embers): draws every buried ember of the mountain as a small marker in its
    /// tier's color, and every buried offering as a bigger magenta one, seen through the ash. One mesh in one draw call,
    /// rebuilt only when embers change, and nothing at all while the option is off.
    /// </summary>
    [RequireComponent(typeof(VoxelTerrain))]
    public sealed class EmberXRayView : MonoBehaviour
    {
        // A built-in shader that is always in builds and lets the depth test be switched off.
        private const string ShaderName = "Hidden/Internal-Colored";

        [SerializeField] private TestModeSettings _testMode;
        [SerializeField] private EmberTierCatalog _tierCatalog;
        [Tooltip("Size of each marker (center to tip), in meters.")]
        [SerializeField, Min(0.01f)] private float _markerSize = 0.06f;

        private readonly EmberMarkerBuilder _builder = new EmberMarkerBuilder();
        private VoxelTerrain _terrain;
        private Mesh _mesh;
        private Material _material;
        private bool _dirty = true;

        private void Awake()
        {
            _terrain = GetComponent<VoxelTerrain>();
            Shader shader = Shader.Find(ShaderName);
            if (_testMode == null || _tierCatalog == null || shader == null)
            {
                Debug.LogError($"{nameof(EmberXRayView)} on '{name}' has unassigned references or no '{ShaderName}' shader.", this);
                enabled = false;
                return;
            }

            _material = new Material(shader) { hideFlags = HideFlags.DontSave };
            _material.SetInt("_ZTest", (int)CompareFunction.Always);
            _material.SetInt("_ZWrite", 0);
            _material.SetInt("_Cull", (int)CullMode.Off);
            _mesh = new Mesh { name = "Ember X-Ray", indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
        }

        private void OnEnable()
        {
            if (_mesh == null)
            {
                return;
            }

            _terrain.Built += MarkDirty;
            _terrain.Restored += MarkDirty;
            _terrain.DigStarted += OnDug;
            _terrain.Dug += OnDug;
            _terrain.SettleFinished += OnSettled;
            _dirty = true;
        }

        private void OnDisable()
        {
            if (_mesh == null)
            {
                return;
            }

            _terrain.Built -= MarkDirty;
            _terrain.Restored -= MarkDirty;
            _terrain.DigStarted -= OnDug;
            _terrain.Dug -= OnDug;
            _terrain.SettleFinished -= OnSettled;
        }

        private void OnDestroy()
        {
            Destroy(_mesh);
            Destroy(_material);
        }

        private void MarkDirty()
        {
            _dirty = true;
        }

        private void OnDug(DigResult result, Vector3 center, float radius)
        {
            _dirty = true;
        }

        private void OnSettled(Bounds changed)
        {
            _dirty = true;
        }

        private void LateUpdate()
        {
            if (!_testMode.ShowAllEmbers || _terrain.Terrain == null)
            {
                return;
            }

            if (_dirty)
            {
                Rebuild(_terrain.Terrain);
            }

            RenderParams parameters = new RenderParams(_material)
            {
                layer = gameObject.layer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };
            Graphics.RenderMesh(parameters, _mesh, 0, transform.localToWorldMatrix);
        }

        // Walks every ember of the mountain; only when they changed and the option is on.
        private void Rebuild(AshTerrain terrain)
        {
            _dirty = false;
            _builder.Clear();
            for (int chunk = 0; chunk < terrain.World.ChunkCount; chunk++)
            {
                EmberField embers = terrain.EmbersIn(chunk);
                for (int i = 0; i < embers.Count; i++)
                {
                    Ember ember = embers[i];
                    EmberTier tier = ember.TierIndex < _tierCatalog.Count ? _tierCatalog[ember.TierIndex] : null;
                    Color color = tier != null ? tier.Color : Color.magenta;
                    color.a = 1f;
                    _builder.Add(ember.Position, _markerSize, color);
                }
            }

            // Buried offerings too, bigger and in magenta, so test runs can find them.
            IReadOnlyList<BuriedFind> finds = terrain.Finds;
            for (int i = 0; i < finds.Count; i++)
            {
                _builder.Add(finds[i].Position, _markerSize * 2.5f, Color.magenta);
            }

            _mesh.Clear();
            _mesh.SetVertices(_builder.Vertices);
            _mesh.SetColors(_builder.Colors);
            _mesh.SetTriangles(_builder.Indices, 0);
        }
    }
}
