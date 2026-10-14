using Cinderbird.Terrain;
using NUnit.Framework;

namespace Cinderbird.Tests.EditMode
{
    public sealed class LooseEmberRulesTests
    {
        private const float Radius = 0.1f;

        [Test]
        public void Settle_FirstMeasure_KeepsItsHeight()
        {
            float y = LooseEmberRules.Settle(1.05f, 1f, float.NaN, Radius, out float rest);

            Assert.That(y, Is.EqualTo(1.05f).Within(1e-5f));
            Assert.That(rest, Is.EqualTo(0.05f).Within(1e-5f));
        }

        [Test]
        public void Settle_FirstMeasure_PartlyInTheWall_StaysHalfSunkAtMost()
        {
            float y = LooseEmberRules.Settle(0.9f, 1f, float.NaN, Radius, out float rest);

            Assert.That(rest, Is.Zero, "center at the surface: half sunk");
            Assert.That(y, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void Settle_AshPiledOver_LiftsItToItsRest()
        {
            float y = LooseEmberRules.Settle(1.0f, 1.5f, Radius, Radius, out float rest);

            Assert.That(y, Is.EqualTo(1.6f).Within(1e-5f));
            Assert.That(rest, Is.EqualTo(Radius));
        }

        [Test]
        public void Settle_AshRanAwayUnderIt_LetsItDownToTheSurface()
        {
            float y = LooseEmberRules.Settle(1.0f, 0.6f, Radius, Radius, out float rest);

            Assert.That(y, Is.EqualTo(0.7f).Within(1e-5f));
            Assert.That(rest, Is.EqualTo(Radius));
        }

        [Test]
        public void Settle_NeverSinksDeeperThanBefore()
        {
            // Half sunk (rest 0.02) and the ash rises a little around it: it is lifted, not sunk.
            float y = LooseEmberRules.Settle(1.02f, 1.05f, 0.02f, Radius, out float rest);

            Assert.That(rest, Is.EqualTo(0.02f).Within(1e-5f));
            Assert.That(y, Is.EqualTo(1.07f).Within(1e-5f));
        }

        [Test]
        public void Settle_AshDroppedALittleUnderIt_ItSitsHigherFromNowOn()
        {
            float y = LooseEmberRules.Settle(1.02f, 0.97f, 0.02f, Radius, out float rest);

            Assert.That(y, Is.EqualTo(1.02f).Within(1e-5f), "still held up by its bottom");
            Assert.That(rest, Is.EqualTo(0.05f).Within(1e-5f), "and the rest only grows");
        }
    }
}
