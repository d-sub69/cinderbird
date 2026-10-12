using System.Collections.Generic;
using System.Text;
using Cinderbird.Economy;
using Cinderbird.Embers;
using Cinderbird.Nest;
using Cinderbird.Player;
using Cinderbird.Sifting;
using Cinderbird.Terrain;
using TMPro;
using UnityEngine;

namespace Cinderbird.UI
{
    /// <summary>
    /// Prototype read-out of the sieve, shake, basket and pile, plus short feedback messages.
    /// Marks itself dirty on changes and rebuilds its text at most a few times per second, because
    /// the ash level changes every frame while shaking.
    /// </summary>
    public sealed class DebugHud : MonoBehaviour
    {
        // Debug display timing, not game balance, so these live here rather than in a settings asset.
        private const float RefreshInterval = 0.1f;
        private const float MessageDuration = 2f;

        [SerializeField] private TMP_Text _text;
        [SerializeField] private Sieve _sieve;
        [SerializeField] private SieveShakeController _shakeController;
        [SerializeField] private PourController _pourController;
        [SerializeField] private PlayerInteractor _interactor;
        [SerializeField] private ToolBelt _toolBelt;
        [SerializeField] private Basket _basket;
        [SerializeField] private Wallet _wallet;
        [SerializeField] private NestController _nest;
        [SerializeField] private VoxelChunk _pile;
        [SerializeField] private EmberTierCatalog _tierCatalog;

        private readonly StringBuilder _builder = new StringBuilder();
        private int[] _tierCounts;
        private bool _dirty = true;
        private float _nextRefreshTime;
        private float _shownEnergy = -1f;
        private float _pileVolume;
        private string _message;
        private float _messageEndTime;

        private void Awake()
        {
            if (_text == null || _sieve == null || _shakeController == null || _pourController == null || _interactor == null ||
                _toolBelt == null || _basket == null || _wallet == null || _nest == null || _pile == null || _tierCatalog == null)
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
            _basket.Contents.Changed += MarkDirty;
            _wallet.Currencies.Changed += MarkDirty;
            _interactor.TargetChanged += OnPromptChanged;
            _toolBelt.Inventory.Changed += OnPromptChanged;
            _pile.Dug += OnDug;
            _pourController.Poured += OnPoured;
            _pourController.PourRefused += OnPourRefused;
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
            _basket.Contents.Changed -= MarkDirty;
            _wallet.Currencies.Changed -= MarkDirty;
            _interactor.TargetChanged -= OnPromptChanged;
            _toolBelt.Inventory.Changed -= OnPromptChanged;
            _pile.Dug -= OnDug;
            _pourController.Poured -= OnPoured;
            _pourController.PourRefused -= OnPourRefused;
        }

        private void Update()
        {
            if (!Mathf.Approximately(_shakeController.Energy, _shownEnergy))
            {
                _dirty = true;
            }

            if (_message != null && Time.unscaledTime >= _messageEndTime)
            {
                _message = null;
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

        private void OnPromptChanged()
        {
            _dirty = true;
            // The prompt should appear as you look at something, not up to one refresh interval later.
            _nextRefreshTime = 0f;
        }

        private void OnPoured(ToolDefinition tool, IPourTarget target, int moved)
        {
            int left = _toolBelt.ActiveSource != null ? _toolBelt.ActiveSource.EmberCount : 0;
            if (left > 0)
            {
                ShowMessage($"{Capitalized(target.DisplayName)} is full: {left} ember{(left == 1 ? "" : "s")} left in the {tool.DisplayName.ToLowerInvariant()}");
            }
        }

        private void OnPourRefused(ToolDefinition tool, PourOutcome outcome)
        {
            switch (outcome)
            {
                case PourOutcome.AshRemaining:
                    ShowMessage("Shake out the ash first");
                    break;
                case PourOutcome.NoEmbers:
                    ShowMessage($"The {tool.DisplayName.ToLowerInvariant()} is empty");
                    break;
                case PourOutcome.TargetFull:
                    ShowMessage("That's full: take the basket to the nest");
                    break;
            }
        }

        private static string Capitalized(string text)
        {
            return string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        private void ShowMessage(string message)
        {
            _message = message;
            _messageEndTime = Time.unscaledTime + MessageDuration;
            _dirty = true;
            // Feedback should appear on the press, not up to one refresh interval later.
            _nextRefreshTime = 0f;
        }

        private void Refresh()
        {
            _dirty = false;
            _nextRefreshTime = Time.unscaledTime + RefreshInterval;
            _shownEnergy = _shakeController.Energy;

            SieveContents sieve = _sieve.Contents;
            BasketContents basket = _basket.Contents;
            _builder.Clear();

            _builder.Append("Sieve: ").Append((sieve.AshVolume * 1000f).ToString("F0")).Append(" L ash");
            _builder.Append(sieve.CanScoop ? "" : "  (shake before scooping)");
            if (_shownEnergy > 0f)
            {
                _builder.Append("  Shaking ").Append(Mathf.RoundToInt(_shownEnergy * 100f)).Append('%');
            }

            _builder.AppendLine();
            // While ash remains the contents are a secret; shaking is the reveal.
            if (sieve.AshVolume > 0f)
            {
                _builder.Append("Embers in sieve: ? (shake to reveal)");
            }
            else
            {
                _builder.Append("Embers in sieve: ").Append(sieve.Embers.Count);
                AppendTierBreakdown(sieve.Embers);
            }

            _builder.AppendLine();

            _builder.Append("Basket: ").Append(basket.Embers.Count).Append('/').Append(basket.Capacity)
                .Append(" embers (Glow ").Append(TotalGlow(basket.Embers)).Append(')');
            AppendTierBreakdown(basket.Embers);
            _builder.AppendLine();

            _builder.Append("Pile: ").Append(_pileVolume.ToString("F2")).Append(" m³, ")
                .Append(_pile.BuriedEmberCount).Append(" embers buried");
            _builder.AppendLine();

            Currencies currencies = _wallet.Currencies;
            _builder.Append("Glow ").Append(currencies.Glow)
                .Append("   Heat ").Append(currencies.Heat).Append(" / ").Append(_nest.HeatGoal)
                .Append(" (").Append(Mathf.FloorToInt(_nest.Progress * 100f)).Append("%)");

            AppendPourPrompt();

            if (_message != null)
            {
                _builder.AppendLine().AppendLine().Append(_message);
            }

            _text.SetText(_builder);
        }

        private void AppendPourPrompt()
        {
            IPourTarget target = _interactor.PourTarget;
            if (target == null)
            {
                return;
            }

            _builder.AppendLine().AppendLine();
            ToolDefinition tool = _toolBelt.ActiveTool;
            if (_toolBelt.ActiveSource == null)
            {
                _builder.Append(Capitalized(target.DisplayName)).Append(": hold the sieve or basket to pour");
                return;
            }

            _builder.Append("Q: pour the ").Append(tool.DisplayName.ToLowerInvariant())
                .Append(" into ").Append(target.DisplayName)
                .Append(" (").Append(_toolBelt.ActiveSource.EmberCount).Append(" embers)");
        }

        private void AppendTierBreakdown(IReadOnlyList<Ember> embers)
        {
            System.Array.Clear(_tierCounts, 0, _tierCounts.Length);
            for (int i = 0; i < embers.Count; i++)
            {
                int tier = embers[i].TierIndex;
                if (tier >= 0 && tier < _tierCounts.Length)
                {
                    _tierCounts[tier]++;
                }
            }

            for (int i = 0; i < _tierCounts.Length; i++)
            {
                if (_tierCounts[i] > 0 && _tierCatalog[i] != null)
                {
                    _builder.Append("  ").Append(_tierCatalog[i].DisplayName).Append(' ').Append(_tierCounts[i]);
                }
            }
        }

        private int TotalGlow(IReadOnlyList<Ember> embers)
        {
            int glow = 0;
            for (int i = 0; i < embers.Count; i++)
            {
                int tier = embers[i].TierIndex;
                if (tier >= 0 && tier < _tierCatalog.Count && _tierCatalog[tier] != null)
                {
                    glow += _tierCatalog[tier].GlowValue;
                }
            }

            return glow;
        }
    }
}
