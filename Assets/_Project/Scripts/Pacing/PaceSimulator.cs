using System;
using System.Collections.Generic;

namespace Cinderbird.Pacing
{
    /// <summary>
    /// Plays a whole run of the economy in one-second steps: the player, the helper and the wind dig ash from the deepest
    /// open layer (a little from the leftovers of outer ones), embers come out in proportion to the ash dug, are carried
    /// to the nest (trips cost time) and pay Glow and Heat; Heat levels and the layer rule open new layers; Glow buys the
    /// cheapest upgrade on offer as soon as it is affordable. Deterministic: the same model always gives the same run.
    /// </summary>
    public sealed class PaceSimulator
    {
        private const double Step = 1.0;
        private const double SampleInterval = 300.0;
        // No ash moved for this long, with nothing left to buy that helps, means the run is stuck.
        private const double StallSeconds = 900.0;
        private const float ShareTolerance = 1e-4f;
        private const float VolumeEpsilon = 1e-6f;

        // What the game starts with when a line is missing from the model.
        private const float DefaultScoopVolume = 0.16f;
        private const float DefaultBasket = 25f;
        private const float DefaultHelperCapacity = 10f;
        private const float DefaultWindRadius = 2.5f;
        private const float DefaultWindCooldown = 60f;

        private readonly PaceModel _model;
        private readonly PaceBehavior _behavior;
        private readonly int _layerCount;
        private readonly int _tierCount;
        private readonly float[] _volumeLeft;
        private readonly double[][] _embersLeft;
        private readonly double[] _initialEmbers;
        private readonly bool[] _open;
        private readonly int[] _levels;
        private readonly Dictionary<string, int> _upgradeIndex = new Dictionary<string, int>();
        private readonly double[] _found;
        private readonly PaceRun _run;

        private double _glow;
        private double _heat;
        private int _heatLevel;
        private double _busy;
        private double _windTimer;
        private double _lastProgress;
        private double _nextSample = SampleInterval;
        private double _sampleAsh;
        private double _sampleEmbers;
        private double _sampleGlow;

        private PaceSimulator(PaceModel model)
        {
            _model = model;
            _behavior = model.Behavior;
            _layerCount = model.Layers.Count;
            _tierCount = Math.Max(model.GlowByTier.Count, model.HeatByTier.Count);
            _volumeLeft = new float[_layerCount];
            _embersLeft = new double[_layerCount][];
            _initialEmbers = new double[_layerCount];
            _open = new bool[_layerCount];
            _found = new double[_tierCount];
            for (int layer = 0; layer < _layerCount; layer++)
            {
                PaceLayer data = model.Layers[layer];
                _volumeLeft[layer] = data.Volume;
                _embersLeft[layer] = new double[_tierCount];
                for (int tier = 0; tier < _tierCount; tier++)
                {
                    _embersLeft[layer][tier] = data.EmbersOf(tier);
                    _initialEmbers[layer] += data.EmbersOf(tier);
                }
            }

            if (_layerCount > 0)
            {
                _open[0] = true;
            }

            _levels = new int[model.Upgrades.Count];
            for (int i = 0; i < model.Upgrades.Count; i++)
            {
                _upgradeIndex[model.Upgrades[i].Id] = i;
            }

            _run = new PaceRun(_layerCount, model.HeatThresholds.Count);
        }

        public static PaceRun Run(PaceModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            return new PaceSimulator(model).Simulate();
        }

        private PaceRun Simulate()
        {
            double end = Math.Max(0.1, _behavior.MaxHours) * 3600.0;
            double time = 0.0;
            while (time < end)
            {
                time += Step;
                double moved = DigStep();
                UpdateHeatLevel(time);
                OpenLayers(time);
                if (IsFinished())
                {
                    _run.FinishedAt = time;
                    break;
                }

                bool bought = Shop(time);
                TakeSample(time, false);
                if (moved > VolumeEpsilon || bought)
                {
                    _lastProgress = time;
                }
                else if (time - _lastProgress > StallSeconds)
                {
                    _run.StopReason = ExplainStall(time);
                    break;
                }
            }

            if (!_run.Finished && _run.StopReason.Length == 0)
            {
                _run.StopReason = $"not finished after {_behavior.MaxHours:0.#} h";
            }

            TakeSample(time, true);
            return _run;
        }

        private double DigStep()
        {
            int target = DeepestOpenWithAsh();
            if (target < 0)
            {
                return 0.0;
            }

            double density = DensityOf(target);
            double sifted = PlayerDig(density) + HelperDig(density);
            double blown = WindDig();
            double total = sifted + blown;
            if (total <= 0.0)
            {
                return 0.0;
            }

            Array.Clear(_found, 0, _found.Length);
            Remove(target, total);
            ReturnSlippedSparks(target, total > 0.0 ? sifted / total : 0.0);
            Earn();
            _sampleAsh += total;
            return total;
        }

        private double PlayerDig(double density)
        {
            if (_busy > 0.0)
            {
                _busy -= Step;
                return 0.0;
            }

            double scoop = ValueOr(PaceEffect.Sieve, DefaultScoopVolume);
            double shake = scoop * 1000.0 / (_model.ShakeLitersPerSecond * Math.Max(0.05, _behavior.ShakeEnergy));
            double carry = Math.Max(1.0, ValueOr(PaceEffect.Basket, DefaultBasket));
            double trips = scoop * density * _behavior.TripSeconds / carry;
            double secondsPerScoop = Math.Max(0.1, _behavior.ScoopOverheadSeconds + shake + trips);
            return Math.Max(0.0, _behavior.ActiveShare) * scoop / secondsPerScoop * Step;
        }

        private double HelperDig(double density)
        {
            if (LevelOf(PaceEffect.HelperHire) < 1 || _model.HelperScoopVolume <= 0f)
            {
                return 0.0;
            }

            double scoop = _model.HelperScoopVolume;
            double pace = Math.Max(0.05, ValueOr(PaceEffect.HelperPace, 1f));
            double carry = Math.Max(1.0, ValueOr(PaceEffect.HelperCapacity, DefaultHelperCapacity));
            double secondsPerScoop = _model.HelperSieveSeconds * pace + scoop * density * _behavior.HelperTripSeconds / carry;
            return scoop / Math.Max(0.1, secondsPerScoop) * Step;
        }

        private double WindDig()
        {
            if (_heatLevel < _model.WindHeatLevel || _model.WindDepth <= 0f)
            {
                return 0.0;
            }

            _windTimer += Step;
            double cooldown = ValueOr(PaceEffect.WindCooldown, DefaultWindCooldown) / Math.Max(0.05, _behavior.WindUse);
            if (_windTimer < cooldown)
            {
                return 0.0;
            }

            _windTimer -= cooldown;
            double radius = ValueOr(PaceEffect.WindRadius, DefaultWindRadius);
            return Math.PI * radius * radius * _model.WindDepth * Math.Max(0.0, _behavior.WindCoverage);
        }

        // Most of the ash comes from the deepest open layer; a share from the leftovers of the outer ones.
        private void Remove(int target, double volume)
        {
            double outerLeft = 0.0;
            for (int layer = 0; layer < target; layer++)
            {
                outerLeft += _open[layer] ? _volumeLeft[layer] : 0f;
            }

            double outer = outerLeft > VolumeEpsilon ? volume * Math.Max(0f, Math.Min(1f, _behavior.OuterShare)) : 0.0;
            for (int layer = 0; layer < target && outer > 0.0; layer++)
            {
                if (_open[layer] && _volumeLeft[layer] > VolumeEpsilon)
                {
                    DigLayer(layer, outer * _volumeLeft[layer] / outerLeft);
                }
            }

            DigLayer(target, volume - outer);
        }

        private void DigLayer(int layer, double volume)
        {
            double left = _volumeLeft[layer];
            if (left <= VolumeEpsilon || volume <= 0.0)
            {
                return;
            }

            double share = Math.Min(1.0, volume / left);
            // Aiming finds more embers per cubic meter than the average while there are still easy ones left.
            double emberShare = Math.Min(1.0, share * Math.Max(1f, _behavior.FindBonus));
            for (int tier = 0; tier < _tierCount; tier++)
            {
                double taken = _embersLeft[layer][tier] * emberShare;
                _embersLeft[layer][tier] -= taken;
                _found[tier] += taken;
            }

            _volumeLeft[layer] = share >= 1.0 ? 0f : (float)(left - volume);
        }

        // Sparks that slip through the mesh fall back onto the pile; some are picked up later, the rest stay to be dug.
        private void ReturnSlippedSparks(int target, double siftedShare)
        {
            double slip = ValueOr(PaceEffect.Mesh, 0f) * (1.0 - Math.Max(0f, Math.Min(1f, _behavior.SlippedPickedUp)));
            if (slip <= 0.0 || siftedShare <= 0.0)
            {
                return;
            }

            for (int tier = 0; tier < _tierCount && tier < _model.TierSlips.Count; tier++)
            {
                if (!_model.TierSlips[tier])
                {
                    continue;
                }

                double back = _found[tier] * siftedShare * slip;
                _found[tier] -= back;
                _embersLeft[target][tier] += back;
            }
        }

        private void Earn()
        {
            for (int tier = 0; tier < _tierCount; tier++)
            {
                double count = _found[tier];
                if (count <= 0.0)
                {
                    continue;
                }

                double glow = count * ValueAt(_model.GlowByTier, tier);
                _glow += glow;
                _heat += count * ValueAt(_model.HeatByTier, tier);
                _sampleGlow += glow;
                _sampleEmbers += count;
            }
        }

        private void UpdateHeatLevel(double time)
        {
            while (_heatLevel < _model.HeatThresholds.Count && _heat >= _model.HeatThresholds[_heatLevel])
            {
                _heatLevel++;
                _run.LevelReachedAt[_heatLevel - 1] = time;
                _run.Events.Add(new PaceEvent(time, $"Heat level {_heatLevel}"));
            }
        }

        private void OpenLayers(double time)
        {
            for (int layer = 1; layer < _layerCount; layer++)
            {
                if (_open[layer])
                {
                    continue;
                }

                PaceLayer data = _model.Layers[layer];
                if (_heatLevel < data.HeatLevel || LeastClearedAbove(layer) + ShareTolerance < data.ClearedShare)
                {
                    return;
                }

                _open[layer] = true;
                _run.LayerOpenedAt[layer] = time;
                _run.Events.Add(new PaceEvent(time, $"{data.Name} opened"));
            }
        }

        private bool IsFinished()
        {
            int last = _layerCount - 1;
            return last >= 0 && _open[last] && (_volumeLeft[last] <= VolumeEpsilon || ClearedShare(last) >= 1f - ShareTolerance);
        }

        private bool Shop(double time)
        {
            bool visited = false;
            while (true)
            {
                int best = CheapestOnOffer();
                if (best < 0)
                {
                    break;
                }

                PaceUpgrade upgrade = _model.Upgrades[best];
                int price = upgrade.PriceFrom(_levels[best]);
                if (price > _glow)
                {
                    break;
                }

                _glow -= price;
                _levels[best]++;
                _run.Purchases++;
                _run.Events.Add(new PaceEvent(time, $"bought {upgrade.Name} {_levels[best]} for {price:N0} Glow"));
                visited = true;
            }

            if (visited)
            {
                _busy += Math.Max(0f, _behavior.ShopSeconds);
            }

            return visited;
        }

        private int CheapestOnOffer()
        {
            int best = -1;
            int bestPrice = int.MaxValue;
            for (int i = 0; i < _levels.Length; i++)
            {
                PaceUpgrade upgrade = _model.Upgrades[i];
                if (_levels[i] >= upgrade.MaxLevel || _heatLevel < upgrade.HeatLevel || !RequirementMet(upgrade))
                {
                    continue;
                }

                int price = upgrade.PriceFrom(_levels[i]);
                if (price < bestPrice)
                {
                    best = i;
                    bestPrice = price;
                }
            }

            return best;
        }

        private bool RequirementMet(PaceUpgrade upgrade)
        {
            return upgrade.RequiresId.Length == 0 ||
                   (_upgradeIndex.TryGetValue(upgrade.RequiresId, out int index) && _levels[index] >= 1);
        }

        private void TakeSample(double time, bool final)
        {
            if (!final && time < _nextSample)
            {
                return;
            }

            double minutes = Math.Max(1.0 / 60.0, (time - (_nextSample - SampleInterval)) / 60.0);
            _run.Samples.Add(new PaceSample(time, DeepestOpen(), _heatLevel, (long)_glow, (float)(_sampleAsh / minutes),
                (float)(_sampleEmbers / minutes), (float)(_sampleGlow / minutes)));
            _sampleAsh = 0.0;
            _sampleEmbers = 0.0;
            _sampleGlow = 0.0;
            _nextSample = time + SampleInterval;
        }

        private string ExplainStall(double time)
        {
            int deepest = DeepestOpen();
            int next = deepest + 1;
            if (next >= _layerCount)
            {
                return $"stuck at {PaceText.Clock(time)}: nothing left to dig";
            }

            PaceLayer data = _model.Layers[next];
            if (_heatLevel < data.HeatLevel)
            {
                return $"stuck at {PaceText.Clock(time)}: {data.Name} needs Heat level {data.HeatLevel}, " +
                       $"but every open layer is dug out at Heat level {_heatLevel} ({_heat:N0} Heat)";
            }

            return $"stuck at {PaceText.Clock(time)}: {data.Name} needs {data.ClearedShare:P0} of the layers above cleared " +
                   $"(least cleared: {LeastClearedAbove(next):P0})";
        }

        private int DeepestOpenWithAsh()
        {
            for (int layer = _layerCount - 1; layer >= 0; layer--)
            {
                if (_open[layer] && _volumeLeft[layer] > VolumeEpsilon)
                {
                    return layer;
                }
            }

            return -1;
        }

        private int DeepestOpen()
        {
            for (int layer = _layerCount - 1; layer >= 0; layer--)
            {
                if (_open[layer])
                {
                    return layer;
                }
            }

            return 0;
        }

        private double DensityOf(int layer)
        {
            double left = _volumeLeft[layer];
            if (left <= VolumeEpsilon)
            {
                return 0.0;
            }

            double embers = 0.0;
            for (int tier = 0; tier < _tierCount; tier++)
            {
                embers += _embersLeft[layer][tier];
            }

            return embers / left;
        }

        private float ClearedShare(int layer)
        {
            if (_initialEmbers[layer] <= 0.0)
            {
                return 1f;
            }

            double left = 0.0;
            for (int tier = 0; tier < _tierCount; tier++)
            {
                left += _embersLeft[layer][tier];
            }

            return (float)(1.0 - left / _initialEmbers[layer]);
        }

        private float LeastClearedAbove(int layer)
        {
            float least = 1f;
            for (int above = 0; above < layer; above++)
            {
                least = Math.Min(least, ClearedShare(above));
            }

            return least;
        }

        private int LevelOf(PaceEffect effect)
        {
            for (int i = 0; i < _levels.Length; i++)
            {
                if (_model.Upgrades[i].Effect == effect)
                {
                    return _levels[i];
                }
            }

            return 0;
        }

        private double ValueOr(PaceEffect effect, float fallback)
        {
            for (int i = 0; i < _levels.Length; i++)
            {
                if (_model.Upgrades[i].Effect == effect)
                {
                    return _model.Upgrades[i].ValueAt(_levels[i]);
                }
            }

            return fallback;
        }

        private static double ValueAt(IReadOnlyList<int> values, int index)
        {
            return index >= 0 && index < values.Count ? values[index] : 0.0;
        }
    }
}
