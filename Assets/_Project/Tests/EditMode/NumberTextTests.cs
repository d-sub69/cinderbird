using System.Globalization;
using System.Text;
using Cinderbird.UI;
using NUnit.Framework;

namespace Cinderbird.Tests.EditMode
{
    public sealed class NumberTextTests
    {
        private static string Int(long value)
        {
            return new StringBuilder().AppendInt(value).ToString();
        }

        private static string Fixed(float value, int decimals)
        {
            return new StringBuilder().AppendFixed(value, decimals).ToString();
        }

        [Test]
        public void AppendInt_MatchesToString()
        {
            foreach (long value in new long[] { 0, 7, 10, 42, 1000, 123456789, -1, -305 })
            {
                Assert.That(Int(value), Is.EqualTo(value.ToString(CultureInfo.InvariantCulture)));
            }
        }

        [Test]
        public void AppendInt_AppendsAfterExistingText()
        {
            Assert.That(new StringBuilder("Glow ").AppendInt(25).Append('!').ToString(), Is.EqualTo("Glow 25!"));
        }

        [Test]
        public void AppendFixed_RoundsToTheDecimals()
        {
            Assert.That(Fixed(3.14159f, 2), Is.EqualTo("3.14"));
            Assert.That(Fixed(2.5f, 0), Is.EqualTo("3"));
            Assert.That(Fixed(16.66f, 1), Is.EqualTo("16.7"));
            Assert.That(Fixed(0f, 1), Is.EqualTo("0.0"));
            Assert.That(Fixed(9.999f, 2), Is.EqualTo("10.00"));
        }

        [Test]
        public void AppendFixed_KeepsLeadingZerosOfTheFraction()
        {
            Assert.That(Fixed(3.05f, 2), Is.EqualTo("3.05"));
            Assert.That(Fixed(0.004f, 3), Is.EqualTo("0.004"));
        }

        [Test]
        public void AppendFixed_Negative()
        {
            Assert.That(Fixed(-1.25f, 1), Is.EqualTo("-1.3"));
            Assert.That(Fixed(-0.04f, 2), Is.EqualTo("-0.04"));
        }
    }
}
