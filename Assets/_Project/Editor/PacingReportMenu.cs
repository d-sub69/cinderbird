using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Cinderbird.Economy;
using Cinderbird.Embers;
using Cinderbird.Helpers;
using Cinderbird.Pacing;
using Cinderbird.Player;
using Cinderbird.Powers;
using Cinderbird.Sifting;
using Cinderbird.Terrain;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Cinderbird.EditorTools
{
    /// <summary>
    /// Cinderbird → Report → Mountain Pacing: builds the real mountain from its data (no scene needed), measures its
    /// layers and the sieves' scoops with the game's dig code, plays a whole run in the pacing simulator with the upgrades,
    /// prices and settings as they are now and the player profile (Data/PacingProfile), and writes Logs/PacingReport.md.
    /// Run it after any change to the mountain, embers, prices or upgrades.
    /// </summary>
    internal static class PacingReportMenu
    {
        private const string ProfilePath = "Assets/_Project/Data/PacingProfile.asset";
        private const string ReportPath = "Logs/PacingReport.md";

        // The design doc's minutes per layer (v0.2 time budget), shown beside the simulated ones.
        private static readonly float[] TargetMinutes = { 25f, 35f, 45f, 35f, 35f, 15f };

        [MenuItem("Cinderbird/Report/Mountain Pacing")]
        private static void Run()
        {
            Stopwatch watch = Stopwatch.StartNew();
            PaceModel model = BuildModel(out string problem);
            if (model == null)
            {
                Debug.LogError($"Pacing report: {problem}");
                return;
            }

            PaceRun run = PaceSimulator.Run(model);
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "Logs");
            File.WriteAllText(ReportPath, PaceReport.Write(model, run, TargetMinutes));
            string verdict = run.Finished
                ? $"main path {PaceText.Clock(run.FinishedAt)}, {run.Purchases} purchases"
                : $"did not finish: {run.StopReason}";
            Debug.Log($"Pacing report ({watch.ElapsedMilliseconds} ms): {verdict}. Written to {Path.GetFullPath(ReportPath)}");
            EditorUtility.RevealInFinder(ReportPath);
        }

        /// <summary>The model from the project's data as it is now; null (and why) when something is missing.</summary>
        public static PaceModel BuildModel(out string problem)
        {
            problem = null;
            VoxelTerrainSettings terrainSettings = SetupUtility.FindAsset<VoxelTerrainSettings>();
            LayerCatalog layers = SetupUtility.FindAsset<LayerCatalog>();
            EmberSpawnSettings spawn = SetupUtility.FindAsset<EmberSpawnSettings>();
            EmberTierCatalog tiers = SetupUtility.FindAsset<EmberTierCatalog>();
            HeatLevelTable heatTable = SetupUtility.FindAsset<HeatLevelTable>();
            ShakeSettings shake = SetupUtility.FindAsset<ShakeSettings>();
            HelperSettings helper = SetupUtility.FindAsset<HelperSettings>();
            WarmWindSettings wind = SetupUtility.FindAsset<WarmWindSettings>();
            ScoopSettings scoop = SetupUtility.FindAsset<ScoopSettings>();
            if (terrainSettings == null || layers == null || spawn == null || tiers == null || heatTable == null ||
                shake == null || helper == null || wind == null || scoop == null)
            {
                problem = "a settings asset is missing (see the errors above)";
                return null;
            }

            PaceProfile profile = AssetDatabase.LoadAssetAtPath<PaceProfile>(ProfilePath);
            PaceBehavior behavior = profile != null ? profile.ToBehavior() : PaceBehavior.Default;
            if (profile == null)
            {
                Debug.LogWarning($"No {ProfilePath}: the pacing report uses the starting guesses. Run the 3A setup to create it.");
            }

            AshTerrain terrain = MountainBuilder.Build(terrainSettings, layers, spawn, tiers, out IReadOnlyList<EmberCluster> clusters);
            AshPileShape shape = terrainSettings.PileShape;
            float[] volumes = MountainSurvey.LayerVolumes(terrain, shape);
            float[] clusterVolumes = MountainSurvey.ClusterVolumes(clusters, volumes);
            int[][] embers = MountainSurvey.EmbersByLayer(terrain, layers.Count, tiers.Count);
            int[][] clusterEmbers = MountainSurvey.ClusterEmbersByLayer(terrain, layers.Count, tiers.Count);
            List<PaceLayer> paceLayers = new List<PaceLayer>();
            for (int layer = 0; layer < layers.Count; layer++)
            {
                paceLayers.Add(new PaceLayer(layers.NameOf(layer), volumes[layer], embers[layer], layers.UnlockHeatLevelOf(layer),
                    layers.UnlockClearedShareOf(layer), clusterVolumes[layer], clusterEmbers[layer]));
            }

            int[] glow = new int[tiers.Count];
            int[] heat = new int[tiers.Count];
            bool[] slips = new bool[tiers.Count];
            for (int tier = 0; tier < tiers.Count; tier++)
            {
                glow[tier] = tiers[tier] != null ? tiers[tier].GlowValue : 0;
                heat[tier] = tiers[tier] != null ? tiers[tier].HeatValue : 0;
                slips[tier] = tiers[tier] != null && tiers[tier].SlipsThroughMesh;
            }

            HeatLevels levels = heatTable.CreateLevels();
            int[] thresholds = new int[levels.MaxLevel];
            for (int level = 1; level <= levels.MaxLevel; level++)
            {
                thresholds[level - 1] = levels.HeatFor(level);
            }

            Dictionary<float, float> scoopVolumes = new Dictionary<float, float>();
            List<PaceUpgrade> upgrades = new List<PaceUpgrade>();
            float helperScoop = 0f;
            foreach (string guid in AssetDatabase.FindAssets("t:UpgradeDefinition"))
            {
                UpgradeDefinition definition = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (definition == null)
                {
                    continue;
                }

                upgrades.Add(ToPace(definition, radius => ScoopVolume(scoopVolumes, terrain, radius, scoop.Penetration, shape.Radius)));
                if (definition is SieveUpgrade sieve)
                {
                    // The helper's sieve is a copy of the first one.
                    helperScoop = ScoopVolume(scoopVolumes, terrain, sieve.TierAt(0).ScoopRadius, scoop.Penetration, shape.Radius);
                }
            }

            return new PaceModel(paceLayers, glow, heat, slips, thresholds, upgrades, behavior,
                shake.ToParameters().MaxRemovalRate * 1000f, helper.SieveSeconds, helperScoop, wind.RequiredHeatLevel,
                wind.Depth);
        }

        private static PaceUpgrade ToPace(UpgradeDefinition definition, System.Func<float, float> scoopVolume)
        {
            int max = definition.MaxLevel;
            int[] prices = new int[max];
            for (int level = 0; level < max; level++)
            {
                prices[level] = definition.PriceAt(level);
            }

            float[] values = new float[max + 1];
            int[] capacities = null;
            float[] scales = null;
            PaceEffect effect = EffectOf(definition);
            for (int level = 0; level <= max; level++)
            {
                switch (definition)
                {
                    case SieveUpgrade sieve:
                        capacities ??= new int[max + 1];
                        values[level] = scoopVolume(sieve.TierAt(level).ScoopRadius);
                        capacities[level] = sieve.TierAt(level).EmberCapacity;
                        scales ??= new float[max + 1];
                        scales[level] = SieveShakeScale.For(sieve.TierAt(0).ScoopRadius, sieve.TierAt(level).ScoopRadius);
                        break;
                    case BasketUpgrade basket:
                        values[level] = basket.LevelAt(level).Capacity;
                        break;
                    case MeshUpgrade mesh:
                        values[level] = mesh.SlipChanceAt(level);
                        break;
                    case TableUpgrade table:
                        values[level] = table.ValueAt(level);
                        break;
                }
            }

            return new PaceUpgrade(definition.Id, definition.DisplayName, effect, prices, values, capacities, definition.RequiresId,
                definition.RequiredHeatLevel, scales);
        }

        private static PaceEffect EffectOf(UpgradeDefinition definition)
        {
            switch (definition)
            {
                case SieveUpgrade _:
                    return PaceEffect.Sieve;
                case BasketUpgrade _:
                    return PaceEffect.Basket;
                case MeshUpgrade _:
                    return PaceEffect.Mesh;
            }

            switch (definition.Id)
            {
                case "sally":
                    return PaceEffect.HelperHire;
                case "sally_pace":
                    return PaceEffect.HelperPace;
                case "sally_sieve":
                    return PaceEffect.HelperCapacity;
                case "wind_radius":
                    return PaceEffect.WindRadius;
                case "wind_cooldown":
                    return PaceEffect.WindCooldown;
                default:
                    Debug.LogWarning($"Pacing report: '{definition.Id}' isn't modelled; it is bought for its price and changes nothing.");
                    return PaceEffect.Other;
            }
        }

        private static float ScoopVolume(Dictionary<float, float> cache, AshTerrain terrain, float radius, float penetration,
            float pileRadius)
        {
            if (!cache.TryGetValue(radius, out float volume))
            {
                volume = MountainSurvey.ScoopVolume(terrain, radius, penetration, pileRadius);
                cache[radius] = volume;
            }

            return volume;
        }
    }
}
