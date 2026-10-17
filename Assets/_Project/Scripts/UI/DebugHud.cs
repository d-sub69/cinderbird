using System.Collections.Generic;
using System.Text;
using Cinderbird.Core;
using Cinderbird.Economy;
using Cinderbird.Embers;
using Cinderbird.Helpers;
using Cinderbird.Nest;
using Cinderbird.Offerings;
using Cinderbird.Player;
using Cinderbird.Powers;
using Cinderbird.Save;
using Cinderbird.Sifting;
using Cinderbird.Terrain;
using Cinderbird.Valley;
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
        // News about offerings stays a little longer than other messages.
        private const float FoundCardDuration = 5f;

        [SerializeField] private TMP_Text _text;
        [SerializeField] private Sieve _sieve;
        [SerializeField] private SieveShakeController _shakeController;
        [SerializeField] private PourController _pourController;
        [Tooltip("Optional: shows why a scoop was refused.")]
        [SerializeField] private ScoopInteractor _scoop;
        [SerializeField] private PlayerInteractor _interactor;
        [SerializeField] private ToolBelt _toolBelt;
        [SerializeField] private ToolPlacementController _placement;
        [SerializeField] private Basket _basket;
        [SerializeField] private Wallet _wallet;
        [SerializeField] private NestController _nest;
        [SerializeField] private VoxelTerrain _pile;
        [SerializeField] private EmberTierCatalog _tierCatalog;
        [Tooltip("Optional: shows save and load messages.")]
        [SerializeField] private SaveController _save;
        [Tooltip("Optional: shows the Heat level and what the next layer needs.")]
        [SerializeField] private NestHeat _heat;
        [Tooltip("Optional: shows a reminder while test mode is on.")]
        [SerializeField] private TestModeSettings _testMode;
        [Tooltip("Optional: confirms purchases.")]
        [SerializeField] private ShopView _shop;
        [Tooltip("Optional: tells when sparks slip through the sieve's mesh.")]
        [SerializeField] private SieveMeshSlip _meshSlip;
        [Tooltip("Optional: tells when an offering is uncovered and when a shelf set is complete.")]
        [SerializeField] private Shrine _shrine;
        [Tooltip("Optional: shows what Sally (the helper) is doing and how full her sieve is.")]
        [SerializeField] private Sally _sally;
        [Tooltip("Optional: prompts while choosing where to send Sally.")]
        [SerializeField] private HelperAssignController _assign;
        [Tooltip("Optional: shows whether the Warm Wind is ready, and its messages.")]
        [SerializeField] private WarmWind _warmWind;
        [Tooltip("Optional: tells when the phoenix takes a new shape in the fire.")]
        [SerializeField] private PhoenixView _phoenix;
        [Tooltip("Optional: tells when the valley thaws.")]
        [SerializeField] private ValleyThaw _valley;

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
                _toolBelt == null || _placement == null || _basket == null || _wallet == null || _nest == null || _pile == null || _tierCatalog == null)
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
            _placement.StateChanged += OnPromptChanged;
            _placement.PickUpRefused += OnPickUpRefused;
            _pile.Dug += OnDug;
            _pile.DigBlocked += OnDigBlocked;
            _pile.LayerUnlocked += OnLayerUnlocked;
            _pile.Restored += OnPileChanged;
            _pile.Built += OnPileChanged;
            _pile.EmbersCollected += OnPileChanged;
            OnPileChanged();
            if (_save != null)
            {
                _save.Notice += ShowMessage;
            }

            if (_heat != null)
            {
                _heat.LevelReached += OnLevelReached;
            }
            _pourController.Poured += OnPoured;
            if (_scoop != null)
            {
                _scoop.ScoopRefused += OnScoopRefused;
            }
            _pourController.PourRefused += OnPourRefused;
            if (_shop != null)
            {
                _shop.Purchased += OnPurchased;
            }

            if (_meshSlip != null)
            {
                _meshSlip.Slipped += OnSlipped;
            }

            if (_shrine != null)
            {
                _shrine.Uncovered += OnOfferingUncovered;
                _shrine.SetCompleted += OnSetCompleted;
            }

            if (_sally != null)
            {
                _sally.Changed += MarkDirty;
                _sally.Notice += ShowMessage;
                _sally.Sieve.Contents.Changed += MarkDirty;
            }

            if (_assign != null)
            {
                _assign.StateChanged += OnPromptChanged;
            }

            if (_warmWind != null)
            {
                _warmWind.Changed += MarkDirty;
                _warmWind.Notice += ShowMessage;
            }

            if (_phoenix != null)
            {
                _phoenix.Notice += ShowStoryMoment;
            }

            if (_valley != null)
            {
                _valley.Notice += ShowStoryMoment;
            }
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
            _placement.StateChanged -= OnPromptChanged;
            _placement.PickUpRefused -= OnPickUpRefused;
            _pile.Dug -= OnDug;
            _pile.DigBlocked -= OnDigBlocked;
            _pile.LayerUnlocked -= OnLayerUnlocked;
            _pile.Restored -= OnPileChanged;
            _pile.Built -= OnPileChanged;
            _pile.EmbersCollected -= OnPileChanged;
            if (_save != null)
            {
                _save.Notice -= ShowMessage;
            }

            if (_heat != null)
            {
                _heat.LevelReached -= OnLevelReached;
            }
            _pourController.Poured -= OnPoured;
            if (_scoop != null)
            {
                _scoop.ScoopRefused -= OnScoopRefused;
            }
            _pourController.PourRefused -= OnPourRefused;
            if (_shop != null)
            {
                _shop.Purchased -= OnPurchased;
            }

            if (_meshSlip != null)
            {
                _meshSlip.Slipped -= OnSlipped;
            }

            if (_shrine != null)
            {
                _shrine.Uncovered -= OnOfferingUncovered;
                _shrine.SetCompleted -= OnSetCompleted;
            }

            if (_sally != null)
            {
                _sally.Changed -= MarkDirty;
                _sally.Notice -= ShowMessage;
                _sally.Sieve.Contents.Changed -= MarkDirty;
            }

            if (_assign != null)
            {
                _assign.StateChanged -= OnPromptChanged;
            }

            if (_warmWind != null)
            {
                _warmWind.Changed -= MarkDirty;
                _warmWind.Notice -= ShowMessage;
            }

            if (_phoenix != null)
            {
                _phoenix.Notice -= ShowStoryMoment;
            }

            if (_valley != null)
            {
                _valley.Notice -= ShowStoryMoment;
            }
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

        private void OnDigBlocked(int layer)
        {
            ShowMessage($"The {_pile.LayerName(layer)} is still sealed");
        }

        private void OnLayerUnlocked(int layer)
        {
            ShowMessage($"The {_pile.LayerName(layer)} melts open");
        }

        private void OnDug(DigResult result, Vector3 center, float radius)
        {
            // Read on dig only: it changes only then.
            _pileVolume = _pile.TotalAshVolume;
            _dirty = true;
        }

        // Built, restored from a save: the totals jumped without a dig.
        private void OnPileChanged()
        {
            _pileVolume = _pile.TotalAshVolume;
            _dirty = true;
        }

        private void OnPromptChanged()
        {
            _dirty = true;
            // The prompt should appear as you look at something, not up to one refresh interval later.
            _nextRefreshTime = 0f;
        }

        private void OnScoopRefused(ScoopRefusal reason)
        {
            switch (reason)
            {
                case ScoopRefusal.AshInSieve:
                    ShowMessage("Shake out the ash first");
                    break;
                case ScoopRefusal.SieveFullOfEmbers:
                    ShowMessage("The sieve is full of embers: pour them into the basket or the nest");
                    break;
            }
        }

        private void OnPickUpRefused(PlaceableTool tool)
        {
            ShowMessage($"No free slot for the {tool.Tool.DisplayName.ToLowerInvariant()}");
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
            ShowMessage(message, MessageDuration);
        }

        private void ShowMessage(string message, float seconds)
        {
            _message = message;
            _messageEndTime = Time.unscaledTime + seconds;
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
            if (_testMode != null && _testMode.Active)
            {
                _builder.AppendLine("<color=#FFB040>TEST MODE: big scoop, no shaking, unlimited sieve</color>");
            }

            _builder.Append("Sieve: ").AppendFixed(sieve.AshVolume * 1000f, 0).Append(" L ash");
            if (sieve.HasAsh)
            {
                _builder.Append("  (shake before scooping)");
            }
            else if (sieve.IsFullOfEmbers)
            {
                _builder.Append("  (full of embers)");
            }
            if (_shownEnergy > 0f)
            {
                _builder.Append("  Shaking ").AppendInt(Mathf.RoundToInt(_shownEnergy * 100f)).Append('%');
            }

            _builder.AppendLine();
            // While ash remains the contents are a secret; shaking is the reveal.
            if (sieve.AshVolume > 0f)
            {
                _builder.Append("Embers in sieve: ? (shake to reveal)");
            }
            else
            {
                _builder.Append("Embers in sieve: ").AppendInt(sieve.Embers.Count);
                if (sieve.EmberCapacity < int.MaxValue)
                {
                    _builder.Append('/').AppendInt(sieve.EmberCapacity);
                }
                AppendTierBreakdown(sieve.Embers);
            }

            _builder.AppendLine();

            _builder.Append("Basket: ").AppendInt(basket.Embers.Count).Append('/').AppendInt(basket.Capacity)
                .Append(" embers (Glow ").AppendInt(TotalGlow(basket.Embers)).Append(')');
            AppendTierBreakdown(basket.Embers);
            _builder.AppendLine();

            _builder.Append("Pile: ").AppendFixed(_pileVolume, 2).Append(" m³, ")
                .AppendInt(_pile.BuriedEmberCount).Append(" embers buried, open down to the ")
                .Append(_pile.LayerName(_pile.DeepestUnlockedLayer));
            _builder.AppendLine();

            Currencies currencies = _wallet.Currencies;
            _builder.Append("Glow ").AppendInt(currencies.Glow)
                .Append("   Heat ").AppendInt(currencies.Heat).Append(" / ").AppendInt(_nest.HeatGoal)
                .Append(" (").AppendInt(Mathf.FloorToInt(_nest.Progress * 100f)).Append("%)");

            AppendHeatLevel();
            AppendPhoenixStage();
            AppendHelper();
            AppendWarmWind();
            AppendPourPrompt();
            AppendPickUpPrompt();
            AppendUsePrompt();
            AppendAssignPrompt();
            AppendPlacementPrompt();

            if (_message != null)
            {
                _builder.AppendLine().AppendLine().Append(_message);
            }

            _text.SetText(_builder);
        }

        private void OnPurchased(UpgradeDefinition upgrade, int level)
        {
            ShowMessage($"Bought: {upgrade.LevelName(level)}");
        }

        private void OnOfferingUncovered(OfferingDefinition offering)
        {
            ShowMessage("Something glints in the ash: look in the hole and pick it up (E)", FoundCardDuration);
        }

        private void OnSetCompleted(OfferingSet set)
        {
            ShowMessage($"Set complete: {set.DisplayName}. {set.DescribeBonus()}", FoundCardDuration);
        }

        private void OnSlipped(int count)
        {
            ShowMessage(count == 1 ? "A spark slipped through the mesh" : $"{count} sparks slipped through the mesh");
        }

        // "Sally: sifting the north slice (on her own), sieve 3 / 10".
        private void AppendHelper()
        {
            if (_sally == null || !_sally.IsHired)
            {
                return;
            }

            _builder.AppendLine().Append("Sally: ").Append(HelperActivityText(_sally.Activity));
            if (_sally.WorkLayer >= 0 && _sally.Mode != HelperMode.Off && !_sally.SpotPending)
            {
                _builder.Append(" in the ").Append(_pile.LayerName(_sally.WorkLayer));
            }

            _builder.Append(_sally.Mode == HelperMode.Assigned ? " (sent to a spot)" : _sally.Mode == HelperMode.Off ? " (resting)" : " (on her own)")
                .Append(", sieve ").AppendInt(_sally.Sieve.Contents.EmberCount).Append(" / ").AppendInt(_sally.Sieve.Contents.EmberCapacity);
            if (_sally.CantWorkReason.Length > 0 && _sally.Mode != HelperMode.Off)
            {
                _builder.AppendLine().Append("  She can't work: ").Append(_sally.CantWorkReason);
            }

            // For testing: what she is doing inside, so a stuck Sally can be told apart from an idle one.
            if (DevTools.Enabled)
            {
                HelperMover mover = _sally.Mover;
                _builder.AppendLine().Append("  [debug] ").Append(_sally.Activity.ToString()).Append(", target ")
                    .Append(_sally.HasTarget ? "yes" : "no").Append(", path ")
                    .Append(mover.HasFailed ? "failed" : mover.HasArrived ? "arrived" : mover.IsWalking ? "walking" : "none");
                if (_sally.LastProblem.Length > 0)
                {
                    _builder.Append(", last: ").Append(_sally.LastProblem);
                }
            }
        }

        private static string HelperActivityText(HelperActivity activity)
        {
            switch (activity)
            {
                case HelperActivity.Idle:
                    return "waiting by the shop";
                case HelperActivity.Wait:
                    return "listening";
                case HelperActivity.GoHome:
                    return "walking back to the shop";
                case HelperActivity.GoToSpot:
                    return "walking to where you sent her";
                case HelperActivity.GoToWork:
                    return "walking to the ash";
                case HelperActivity.GoToNest:
                    return "carrying embers to the nest";
                case HelperActivity.Pour:
                    return "feeding the nest";
                default:
                    return "sifting";
            }
        }

        // "Warm Wind (hold F): ready" or "Warm Wind: gathers again in 42 s", once the nest is warm enough.
        private void AppendWarmWind()
        {
            if (_warmWind == null || !_warmWind.IsUnlocked)
            {
                return;
            }

            int seconds = Mathf.CeilToInt(_warmWind.CooldownRemaining);
            _builder.AppendLine();
            if (seconds <= 0)
            {
                _builder.Append("Warm Wind (hold F, let go): ready");
            }
            else
            {
                _builder.Append("Warm Wind: gathers again in ").AppendInt(seconds).Append(" s");
            }
        }

        private void AppendAssignPrompt()
        {
            if (_assign == null || !_assign.IsActive)
            {
                return;
            }

            _builder.AppendLine();
            switch (_assign.Check)
            {
                case AssignCheck.Valid when _assign.TargetLayer >= 0:
                    _builder.Append("Left-click: send Sally here to sift the ").Append(_pile.LayerName(_assign.TargetLayer));
                    break;
                case AssignCheck.Valid:
                    _builder.Append("Left-click: send Sally here to sift the outer ash");
                    break;
                case AssignCheck.Sealed:
                    _builder.Append("That layer is still sealed");
                    break;
                case AssignCheck.NoAshNearby:
                    _builder.Append("Too far from the ash");
                    break;
                case AssignCheck.Unreachable:
                    _builder.Append("Sally can't get there");
                    break;
                default:
                    _builder.Append("Point at the ash, or the ground beside it");
                    break;
            }

            _builder.Append("   Right-click: cancel");
        }

        // For testing: which phoenix stage shows, next to the Heat level it follows.
        private void AppendPhoenixStage()
        {
            if (_phoenix == null || !DevTools.Enabled)
            {
                return;
            }

            _builder.AppendLine().Append("  [debug] phoenix stage ").AppendInt(_phoenix.Stage);
        }

        // The phoenix and the valley changing are the story moving on: their lines stay a little longer.
        private void ShowStoryMoment(string message)
        {
            ShowMessage(message, FoundCardDuration);
        }

        private void OnLevelReached(int level)
        {
            ShowMessage($"The nest reaches Heat level {level}");
        }

        // "Heat level 1 (120 / 250)" and what the next sealed layer still needs.
        private void AppendHeatLevel()
        {
            if (_heat == null || _heat.Levels == null)
            {
                return;
            }

            int heat = _heat.Heat;
            int level = _heat.Level;
            _builder.AppendLine().Append("Heat level ").AppendInt(level);
            if (level < _heat.Levels.MaxLevel)
            {
                _builder.Append(" (").AppendInt(heat).Append(" / ").AppendInt(_heat.Levels.HeatFor(level + 1)).Append(')');
            }

            int next = _heat.NextLayer;
            if (next < 0)
            {
                return;
            }

            int neededLevel = _heat.RequiredLevelFor(next);
            float neededShare = _heat.RequiredClearedShareFor(next);
            float cleared = _heat.LeastClearedAbove(next, out int holding);
            _builder.AppendLine().Append("To open the ").Append(_pile.LayerName(next)).Append(": Heat level ").AppendInt(neededLevel)
                .Append(level >= neededLevel ? " (done)" : string.Empty);
            if (holding >= 0)
            {
                _builder.Append(", ").Append(_pile.LayerName(holding)).Append(' ')
                    .AppendInt(Mathf.FloorToInt(cleared * 100f)).Append("% of ").AppendInt(Mathf.RoundToInt(neededShare * 100f))
                    .Append("% cleared");
            }
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
                .Append(" (").AppendInt(_toolBelt.ActiveSource.EmberCount).Append(" embers)");
        }

        private void AppendPickUpPrompt()
        {
            PlaceableTool tool = _interactor.PickUpTarget;
            if (tool == null)
            {
                return;
            }

            _builder.AppendLine().Append("E: pick up the ").Append(tool.Tool.DisplayName.ToLowerInvariant());
            IEmberHolder holder = tool.GetComponent<IEmberHolder>();
            if (holder != null)
            {
                _builder.Append(" (").AppendInt(holder.Source.EmberCount).Append(" embers)");
            }
        }

        private void AppendUsePrompt()
        {
            IInteractable target = _interactor.UseTarget;
            if (target != null)
            {
                _builder.AppendLine().Append("E: ").Append(target.Prompt);
            }
        }

        private void AppendPlacementPrompt()
        {
            PlaceableTool tool = _placement.Placing;
            if (tool == null || _placement.Placement == PlacementResult.NoSurface)
            {
                return;
            }

            string toolName = tool.Tool.DisplayName.ToLowerInvariant();
            _builder.AppendLine().AppendLine();
            switch (_placement.Placement)
            {
                case PlacementResult.Valid:
                    _builder.Append("G: place the ").Append(toolName);
                    break;
                case PlacementResult.OnAsh:
                    _builder.Append("Can't place the ").Append(toolName).Append(" on the ash pile");
                    break;
                case PlacementResult.InNoPlacementZone:
                    _builder.Append("Can't place the ").Append(toolName).Append(" in ")
                        .Append(_placement.BlockingZone != null ? _placement.BlockingZone.AreaName : "here");
                    break;
                case PlacementResult.TooSteep:
                    _builder.Append("Too steep to place the ").Append(toolName);
                    break;
                case PlacementResult.Blocked:
                    _builder.Append("Not enough room for the ").Append(toolName);
                    break;
            }
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
                    _builder.Append("  ").Append(_tierCatalog[i].DisplayName).Append(' ').AppendInt(_tierCounts[i]);
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
