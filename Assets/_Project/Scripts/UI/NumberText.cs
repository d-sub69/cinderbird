using System.Text;

namespace Cinderbird.UI
{
    /// <summary>
    /// Appends numbers to a StringBuilder digit by digit. StringBuilder.Append(int/float) goes through ToString
    /// and allocates a string each time, which shows up as per-frame garbage in HUDs that refresh constantly.
    /// </summary>
    public static class NumberText
    {
        public static StringBuilder AppendInt(this StringBuilder builder, long value)
        {
            if (value < 0)
            {
                builder.Append('-');
                // Negating long.MinValue overflows; it never occurs in a HUD, so clamp rather than special-case.
                value = value == long.MinValue ? long.MaxValue : -value;
            }

            int start = builder.Length;
            do
            {
                builder.Append((char)('0' + value % 10));
                value /= 10;
            }
            while (value > 0);

            // Digits were added lowest first; flip them in place.
            for (int left = start, right = builder.Length - 1; left < right; left++, right--)
            {
                char swap = builder[left];
                builder[left] = builder[right];
                builder[right] = swap;
            }

            return builder;
        }

        /// <summary>Appends a value rounded to a fixed number of decimals (0 to 6), e.g. 3.14 for 2.</summary>
        public static StringBuilder AppendFixed(this StringBuilder builder, float value, int decimals)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return builder.Append(float.IsNaN(value) ? "NaN" : value > 0f ? "∞" : "-∞");
            }

            decimals = decimals < 0 ? 0 : decimals > 6 ? 6 : decimals;
            long scale = 1;
            for (int i = 0; i < decimals; i++)
            {
                scale *= 10;
            }

            double scaled = System.Math.Round((double)value * scale, System.MidpointRounding.AwayFromZero);
            bool negative = scaled < 0;
            long units = (long)System.Math.Abs(scaled);
            if (negative)
            {
                builder.Append('-');
            }

            builder.AppendInt(units / scale);
            if (decimals == 0)
            {
                return builder;
            }

            builder.Append('.');
            long fraction = units % scale;
            // Leading zeros of the fraction, e.g. the 0 in 3.05.
            for (long digit = scale / 10; digit > 1 && fraction < digit; digit /= 10)
            {
                builder.Append('0');
            }

            return builder.AppendInt(fraction);
        }
    }
}
