using NUnit.Framework;

public class TierUtilTests
{
    [Test]
    public void All_ContainsExactlyTheFourScaledTiersInOrder()
    {
        CollectionAssert.AreEqual(
            new[] { Tier.Normal, Tier.Rare, Tier.Epic, Tier.Legend },
            TierUtil.All);
    }

    [TestCase(Tier.Normal, "normal")]
    [TestCase(Tier.Rare, "rare")]
    [TestCase(Tier.Epic, "epic")]
    [TestCase(Tier.Legend, "legend")]
    [TestCase(Tier.Chieftain, "normal")]
    public void FieldName_MapsToPerTierFieldName(Tier tier, string expected)
    {
        Assert.AreEqual(expected, TierUtil.FieldName(tier));
    }
}
