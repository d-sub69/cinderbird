namespace Cinderbird.Pacing
{
    /// <summary>What an upgrade changes in the pacing simulator; anything else is bought for its price and changes nothing.</summary>
    public enum PaceEffect
    {
        Other = 0,
        /// <summary>Values: scoop volume (m³) per level; capacities: embers the sieve holds.</summary>
        Sieve = 1,
        /// <summary>Values: embers the basket carries per trip.</summary>
        Basket = 2,
        /// <summary>Values: chance a spark slips through the mesh.</summary>
        Mesh = 3,
        /// <summary>Level 1 hires the helper.</summary>
        HelperHire = 4,
        /// <summary>Values: share of the time her sieve takes (1 = steady, 0.55 = swift).</summary>
        HelperPace = 5,
        /// <summary>Values: embers she carries per trip to the nest.</summary>
        HelperCapacity = 6,
        /// <summary>Values: gust radius (m).</summary>
        WindRadius = 7,
        /// <summary>Values: seconds between gusts.</summary>
        WindCooldown = 8,
    }
}
