using System.Text;
using Cinderbird.Embers;
using Cinderbird.Sifting;
using Cinderbird.Terrain;
using TMPro;
using UnityEngine;

namespace Cinderbird.UI
{
    /// <summary>
    /// Prototype read-out of the sieve, shake and pile. Marks itself dirty on changes and rebuilds
    /// its text at most a few times per second, because the ash level changes every frame while shaking.
    /// </summary>
    public sealed class DebugHud : MonoBehaviour
    {
        // Debug display cadence, not game balance, so it lives here rather than in a settings asset.
        private const float RefreshInterval = 0.1f;

        [SerializeField] private TMP_Text _text;
        [SerializeField] private Sieve _sieve;
        [SerializeField] private SieveShakeController _shakeController;
        [SerializeField] private VoxelChunk _pile;
        [SerializeField] private EmberTierCatalog _tierCatalog;

        private readonly StringBuilder _builder = new StringBuilder();
        private int[] _tierCounts;
        private bool _dirty = true;
        private float _nextRefreshTime;
        private float _shownEnergy = -1f;
        private float _pileVolume;

        private void Awake()
        {
            if (_text == null || _sieve == null || _shakeController == null || _pile == null || _tierCatalog == null)
            {
                Debug.LogError($"{nameof(DebugHud)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _tierCounts = new int[_tierCatalog.Count];
        }

        private void OnEnable()
        {
            if (_tierCounts == null)
            {
                return;
            }

            _sieve.Contents.Changed += MarkDirty;
            _pile.Dug += OnDug;
        }

        // Start rather than OnEnable: the pile builds its grid in Awake, which may run after ours.
        private void Start()
        {
            _pileVolume = _pile.TotalAshVolume;
            _dirty = true;
        }

        private void OnDisable()
        {
            if (_tierCounts == null)
            {
                return;
            }

            _sieve.Contents.Changed -= MarkDirty;
            _pile.Dug -= OnDug;
        }

        private void Update()
        {
            if (!Mathf.Approximately(_shakeController.Energy, _shownEnergy))
            {
                _dirty = true;
            }

            if (!_dirty || Time.unscaledTime < _nextRefreshTime)
            {
                return;
            }

            Refresh();
        }

        private void MarkDirty()
        {
            _dirty = true;
        }

        private void OnDug(DigResult result)
        {
            // Cached here: the total walks the whole grid, too slow to repeat on every HUD refresh.
            _pileVolume = _pile.TotalAshVolume;
            _dirty = true;
        }

        private void Refresh()
        {
            _dirty = false;
            _nextRefreshTime = Time.unscaledTime + RefreshInterval;
            _shownEnergy = _shakeController.Energy;

            SieveContents contents = _sieve.Contents;
            CountTiers(contents);

            _builder.Clear();
            _builder.Append("Sieve: ").Append((contents.AshVolume * 1000f).ToString("F0")).Append(" L ash");
            _builder.Append(contents.CanScoop ? "" : "  (shake before scooping)");
            if (_shownEnergy > 0f)
            {
                _builder.Append("  Shaking ").Append(Mathf.RoundToInt(_shownEnergy * 100f)).Append('%');
            }

            _builder.AppendLine();
            _builder.Append("Embers in sieve: ").Append(contents.Embers.Count);
            for (int i = 0; i < _tierCounts.Length; i++)
            {
                if (_tierCounts[i] > 0 && _tierCatalog[i] != null)
                {
                    _builder.Append("  ").Append(_tierCatalog[i].DisplayName).Append(' ').Append(_tierCounts[i]);
                }
            }

            _builder.AppendLine();
            _builder.Append("Pile: ").Append(_pileVolume.ToString("F2")).Append(" m³, ")
                .Append(_pile.BuriedEmberCount).Append(" embers buried");

            _text.SetText(_builder);
        }

        private void CountTiers(SieveContents contents)
        {
            System.Array.Clear(_tierCounts, 0, _tierCounts.Length);
            for (int i = 0; i < contents.Embers.Count; i++)
            {
                int tier = contents.Embers[i].TierIndex;
                if (tier >= 0 && tier < _tierCounts.Length)
                {
                    _tierCounts[tier]++;
                }
            }
        }
    }
}
