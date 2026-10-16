using System.Collections.Generic;
using Cinderbird.Economy;
using NUnit.Framework;

namespace Cinderbird.Tests.EditMode
{
    public sealed class ShopTests
    {
        private sealed class FakeUpgrade : IUpgrade
        {
            public FakeUpgrade(string id, int maxLevel, int baseCost, float growth)
            {
                Id = id;
                MaxLevel = maxLevel;
                BaseCost = baseCost;
                Growth = growth;
            }

            public string Id { get; }
            public int MaxLevel { get; }
            public string RequiresId { get; set; } = string.Empty;
            private int BaseCost { get; }
            private float Growth { get; }

            public int PriceAt(int level)
            {
                return UpgradePrice.At(BaseCost, Growth, level);
            }
        }

        private static Shop CreateShop(int glow, out Currencies currencies, out UpgradeLevels levels)
        {
            currencies = new Currencies();
            currencies.Add(glow, 0);
            levels = new UpgradeLevels();
            return new Shop(currencies, levels);
        }

        [Test]
        public void RequiredUpgradeNotBought_IsLocked_ThenOnSale()
        {
            Shop shop = CreateShop(1000, out Currencies currencies, out UpgradeLevels levels);
            FakeUpgrade helper = new FakeUpgrade("helper", 1, 100, 1f);
            FakeUpgrade pace = new FakeUpgrade("pace", 2, 50, 1f) { RequiresId = "helper" };

            Assert.That(shop.IsAvailable(pace), Is.False);
            Assert.That(shop.CanAfford(pace), Is.False);
            Assert.That(shop.TryBuy(pace), Is.EqualTo(Purchase.Locked));
            Assert.That(currencies.Glow, Is.EqualTo(1000), "nothing spent");

            Assert.That(shop.TryBuy(helper), Is.EqualTo(Purchase.Bought));

            Assert.That(shop.IsAvailable(pace), Is.True);
            Assert.That(shop.TryBuy(pace), Is.EqualTo(Purchase.Bought));
            Assert.That(levels.LevelOf("pace"), Is.EqualTo(1));
        }

        [Test]
        public void Price_FollowsTheDesignCurve()
        {
            Assert.That(UpgradePrice.At(30, 1.15f, 0), Is.EqualTo(30));
            Assert.That(UpgradePrice.At(30, 1.15f, 1), Is.EqualTo(35));
            Assert.That(UpgradePrice.At(30, 1.15f, 2), Is.EqualTo(40));
            Assert.That(UpgradePrice.At(120, 5f, 1), Is.EqualTo(600));
        }

        [Test]
        public void TryBuy_WithEnoughGlow_SpendsItAndRaisesTheLevel()
        {
            Shop shop = CreateShop(100, out Currencies currencies, out UpgradeLevels levels);
            FakeUpgrade mesh = new FakeUpgrade("mesh", 3, 30, 1.15f);

            Assert.That(shop.TryBuy(mesh), Is.EqualTo(Purchase.Bought));

            Assert.That(currencies.Glow, Is.EqualTo(70));
            Assert.That(levels.LevelOf("mesh"), Is.EqualTo(1));
            Assert.That(shop.NextPrice(mesh), Is.EqualTo(35));
        }

        [Test]
        public void TryBuy_TooLittleGlow_ChangesNothing()
        {
            Shop shop = CreateShop(29, out Currencies currencies, out UpgradeLevels levels);
            FakeUpgrade mesh = new FakeUpgrade("mesh", 3, 30, 1.15f);

            Assert.That(shop.CanAfford(mesh), Is.False);
            Assert.That(shop.TryBuy(mesh), Is.EqualTo(Purchase.NotEnoughGlow));

            Assert.That(currencies.Glow, Is.EqualTo(29));
            Assert.That(levels.LevelOf("mesh"), Is.Zero);
        }

        [Test]
        public void TryBuy_AtTheTopLevel_IsRefusedAndFree()
        {
            Shop shop = CreateShop(10000, out Currencies currencies, out UpgradeLevels levels);
            FakeUpgrade sieve = new FakeUpgrade("sieve", 2, 120, 5f);
            shop.TryBuy(sieve);
            shop.TryBuy(sieve);
            int glow = currencies.Glow;

            Assert.That(shop.IsMaxed(sieve), Is.True);
            Assert.That(shop.NextPrice(sieve), Is.EqualTo(-1));
            Assert.That(shop.TryBuy(sieve), Is.EqualTo(Purchase.AlreadyMaxed));
            Assert.That(currencies.Glow, Is.EqualTo(glow));
            Assert.That(glow, Is.EqualTo(10000 - 120 - 600));
        }

        [Test]
        public void TrySpend_NeverGoesBelowZero_AndHeatIsUntouched()
        {
            Currencies currencies = new Currencies();
            currencies.Add(10, 7);

            Assert.That(currencies.TrySpend(11), Is.False);
            Assert.That(currencies.TrySpend(10), Is.True);
            Assert.That(currencies.Glow, Is.Zero);
            Assert.That(currencies.Heat, Is.EqualTo(7));
        }

        [Test]
        public void Levels_RestoreReplacesAllAndTellsListeners()
        {
            UpgradeLevels levels = new UpgradeLevels();
            levels.SetLevel("old", 2);
            List<string> changed = new List<string>();
            levels.Changed += changed.Add;

            levels.Restore(new[] { new KeyValuePair<string, int>("sieve", 1) });

            Assert.That(levels.LevelOf("old"), Is.Zero);
            Assert.That(levels.LevelOf("sieve"), Is.EqualTo(1));
            Assert.That(changed, Is.EquivalentTo(new[] { "old", "sieve" }));
        }

        [Test]
        public void Levels_CopyToListsOnlyBoughtUpgrades()
        {
            UpgradeLevels levels = new UpgradeLevels();
            levels.SetLevel("mesh", 2);
            levels.SetLevel("basket", 0);
            List<KeyValuePair<string, int>> copy = new List<KeyValuePair<string, int>>();

            levels.CopyTo(copy);

            Assert.That(copy, Is.EqualTo(new[] { new KeyValuePair<string, int>("mesh", 2) }));
        }
    }
}
