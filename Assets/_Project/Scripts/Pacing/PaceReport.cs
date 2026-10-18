using System;
using System.Globalization;
using System.Text;

namespace Cinderbird.Pacing
{
    /// <summary>A simulated run as a readable Markdown report: the mountain, the time per layer, the moments, the rates.</summary>
    public static class PaceReport
    {
        /// <param name="targetMinutes">Optional: the design's minutes per layer, shown beside the simulated ones.</param>
        public static string Write(PaceModel model, PaceRun run, float[] targetMinutes = null)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            StringBuilder text = new StringBuilder(8192);
            text.AppendLine("# Cinderbird pacing report").AppendLine();
            text.AppendLine(run.Finished
                ? $"**Main path: {Hours(run.FinishedAt)}** (to the last layer dug out), {run.Purchases} purchases."
                : $"**The run did not finish:** {run.StopReason}. {run.Purchases} purchases.");
            text.AppendLine();

            text.AppendLine("## The mountain").AppendLine();
            text.AppendLine("| Layer | Ash m³ | Embers | Embers/m³ | Glow | Heat | Opens at |");
            text.AppendLine("|---|---|---|---|---|---|---|");
            for (int layer = 0; layer < model.Layers.Count; layer++)
            {
                PaceLayer data = model.Layers[layer];
                long glow = 0;
                long heat = 0;
                for (int tier = 0; tier < data.TierCount; tier++)
                {
                    glow += (long)data.EmbersOf(tier) * At(model.GlowByTier, tier);
                    heat += (long)data.EmbersOf(tier) * At(model.HeatByTier, tier);
                }

                string rule = layer == 0 ? "start" : $"{data.ClearedShare:P0} above + Heat {data.HeatLevel}";
                float density = data.Volume > 0f ? data.TotalEmbers / data.Volume : 0f;
                text.AppendLine(string.Format(c, "| {0} | {1:N0} | {2:N0} | {3:0.00} | {4:N0} | {5:N0} | {6} |",
                    data.Name, data.Volume, data.TotalEmbers, density, glow, heat, rule));
            }

            text.AppendLine().AppendLine("## Time per layer").AppendLine();
            text.AppendLine(targetMinutes != null ? "| Layer | Opened | Minutes in it | Design target |" : "| Layer | Opened | Minutes in it |");
            text.AppendLine(targetMinutes != null ? "|---|---|---|---|" : "|---|---|---|");
            for (int layer = 0; layer < model.Layers.Count; layer++)
            {
                double opened = run.LayerOpenedAt[layer];
                double until = layer + 1 < model.Layers.Count && run.LayerOpenedAt[layer + 1] >= 0.0
                    ? run.LayerOpenedAt[layer + 1]
                    : run.Finished ? run.FinishedAt : -1.0;
                string openedText = opened >= 0.0 ? PaceText.Clock(opened) : "never";
                string minutes = opened >= 0.0 && until >= 0.0 ? ((until - opened) / 60.0).ToString("0", c) : "–";
                string row = $"| {model.Layers[layer].Name} | {openedText} | {minutes} |";
                if (targetMinutes != null)
                {
                    row += layer < targetMinutes.Length ? $" {targetMinutes[layer].ToString("0", c)} |" : " – |";
                }

                text.AppendLine(row);
            }

            text.AppendLine().AppendLine("## Heat levels").AppendLine();
            text.AppendLine("| Level | Heat | Reached |").AppendLine("|---|---|---|");
            for (int level = 0; level < model.HeatThresholds.Count; level++)
            {
                double at = run.LevelReachedAt[level];
                text.AppendLine(string.Format(c, "| {0} | {1:N0} | {2} |", level + 1, model.HeatThresholds[level],
                    at >= 0.0 ? PaceText.Clock(at) : "never"));
            }

            text.AppendLine().AppendLine("## Rates (every 5 minutes)").AppendLine();
            text.AppendLine("| Time | Layer | Heat Lv | Glow | Ash m³/min | Embers/min | Glow/min |");
            text.AppendLine("|---|---|---|---|---|---|---|");
            foreach (PaceSample sample in run.Samples)
            {
                string layer = sample.Layer < model.Layers.Count ? model.Layers[sample.Layer].Name : "?";
                text.AppendLine(string.Format(c, "| {0} | {1} | {2} | {3:N0} | {4:0.0} | {5:0} | {6:N0} |",
                    PaceText.Clock(sample.Seconds), layer, sample.HeatLevel, sample.Glow, sample.AshPerMinute,
                    sample.EmbersPerMinute, sample.GlowPerMinute));
            }

            text.AppendLine().AppendLine("## Moments").AppendLine();
            foreach (PaceEvent moment in run.Events)
            {
                text.Append("- ").Append(PaceText.Clock(moment.Seconds)).Append(' ').AppendLine(moment.Text);
            }

            return text.ToString();
        }

        private static string Hours(double seconds)
        {
            TimeSpan span = TimeSpan.FromSeconds(seconds);
            return $"{(int)span.TotalHours} h {span.Minutes:00} min";
        }

        private static long At(System.Collections.Generic.IReadOnlyList<int> values, int index)
        {
            return index >= 0 && index < values.Count ? values[index] : 0;
        }
    }
}
