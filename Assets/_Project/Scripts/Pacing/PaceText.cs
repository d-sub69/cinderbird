using System;
using System.Globalization;
using System.Text;

namespace Cinderbird.Pacing
{
    /// <summary>The pacing log's lines, as text a person can read and a tool can still pick apart.</summary>
    public static class PaceText
    {
        public const string Tag = "[Pace]";

        /// <summary>Play time as h:mm:ss (or m:ss under an hour).</summary>
        public static string Clock(double seconds)
        {
            long total = (long)Math.Max(0.0, Math.Floor(seconds));
            long hours = total / 3600;
            long minutes = total / 60 % 60;
            long secs = total % 60;
            return hours > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", hours, minutes, secs)
                : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", minutes, secs);
        }

        /// <summary>A one-off moment: a Heat level, a layer opening, a purchase.</summary>
        public static string Event(double playSeconds, string what)
        {
            return $"{Tag} {Clock(playSeconds)} | {what}";
        }

        /// <summary>The line for one closed window, with where the game stands and the session so far.</summary>
        public static string Window(double playSeconds, PaceWindow window, PaceWindow session, PaceStatus status)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            StringBuilder line = new StringBuilder(320);
            line.Append(Tag).Append(' ').Append(Clock(playSeconds)).Append(" | ");
            line.Append(status.LayerName).Append(' ').Append((status.LayerCleared * 100f).ToString("0", c)).Append("% · Heat ")
                .Append(status.Heat.ToString("N0", c)).Append(" Lv").Append(status.HeatLevel);
            if (status.NextLevelHeat > 0)
            {
                line.Append(" (next ").Append(status.NextLevelHeat.ToString("N0", c)).Append(')');
            }

            line.Append(" · Glow ").Append(status.Glow.ToString("N0", c));
            line.Append(" | last ").Append(Clock(window.Seconds)).Append(": ");
            AppendSplit(line, "ash", window, true);
            line.Append(" · ");
            AppendSplit(line, "embers dug", window, false);
            line.Append(" · fed ").Append(window.EmbersFed).Append(" in ").Append(window.Feeds).Append(window.Feeds == 1 ? " trip" : " trips");
            line.Append(" · Glow +").Append(window.GlowEarned.ToString("N0", c)).Append(" -").Append(window.GlowSpent.ToString("N0", c));
            line.Append(" · bought ").Append(window.Purchases);
            line.Append(" | you: ").Append(window.Scoops(PaceSource.Player)).Append(" scoops, ")
                .Append(window.LitersPerScoop(PaceSource.Player).ToString("0", c)).Append(" L each, ")
                .Append(window.SecondsPerScoop.ToString("0.0", c)).Append(" s apart");
            line.Append(" | session: ash ").Append(session.TotalVolume.ToString("0.0", c)).Append(" m³, fed ")
                .Append(session.EmbersFed).Append(", bought ").Append(session.Purchases);
            return line.ToString();
        }

        private static void AppendSplit(StringBuilder line, string label, PaceWindow window, bool volume)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            string total = volume ? window.TotalVolume.ToString("0.00", c) + " m³" : window.TotalEmbers.ToString(c);
            line.Append(label).Append(' ').Append(total).Append(" (you ").Append(Part(window, PaceSource.Player, volume))
                .Append(", Sally ").Append(Part(window, PaceSource.Helper, volume))
                .Append(", wind ").Append(Part(window, PaceSource.Wind, volume)).Append(')');
        }

        private static string Part(PaceWindow window, PaceSource source, bool volume)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            return volume ? window.Volume(source).ToString("0.00", c) : window.Embers(source).ToString(c);
        }
    }
}
