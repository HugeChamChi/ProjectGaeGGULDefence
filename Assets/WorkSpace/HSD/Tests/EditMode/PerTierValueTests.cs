using NUnit.Framework;

public class PerTierValueTests
{
    [Test]
    public void PerTierFloat_Get_ReturnsMatchingTierValue()
    {
        var value = new PerTierFloat { normal = 1f, rare = 2f, epic = 3f, legend = 4f };

        Assert.AreEqual(1f, value.Get(Tier.Normal));
        Assert.AreEqual(2f, value.Get(Tier.Rare));
        Assert.AreEqual(3f, value.Get(Tier.Epic));
        Assert.AreEqual(4f, value.Get(Tier.Legend));
    }

    [Test]
    public void PerTierFloat_Get_ChieftainFallsBackToNormal()
    {
        var value = new PerTierFloat { normal = 1f, rare = 2f, epic = 3f, legend = 4f };

        Assert.AreEqual(1f, value.Get(Tier.Chieftain));
    }

    [Test]
    public void PerTierFloat_Set_OnlyUpdatesTargetTier()
    {
        var value = new PerTierFloat { normal = 1f, rare = 2f, epic = 3f, legend = 4f };

        value.Set(Tier.Rare, 20f);

        Assert.AreEqual(1f, value.normal);
        Assert.AreEqual(20f, value.rare);
        Assert.AreEqual(3f, value.epic);
        Assert.AreEqual(4f, value.legend);
    }

    [Test]
    public void PerTierInt_Get_ReturnsMatchingTierValue()
    {
        var value = new PerTierInt { normal = 1, rare = 2, epic = 3, legend = 4 };

        Assert.AreEqual(1, value.Get(Tier.Normal));
        Assert.AreEqual(2, value.Get(Tier.Rare));
        Assert.AreEqual(3, value.Get(Tier.Epic));
        Assert.AreEqual(4, value.Get(Tier.Legend));
    }

    [Test]
    public void PerTierInt_Set_OnlyUpdatesTargetTier()
    {
        var value = new PerTierInt { normal = 1, rare = 2, epic = 3, legend = 4 };

        value.Set(Tier.Legend, 40);

        Assert.AreEqual(1, value.normal);
        Assert.AreEqual(2, value.rare);
        Assert.AreEqual(3, value.epic);
        Assert.AreEqual(40, value.legend);
    }

    [Test]
    public void ConstantFloat_Get_IgnoresTier()
    {
        var value = new ConstantFloat { value = 5f };

        Assert.AreEqual(5f, value.Get(Tier.Normal));
        Assert.AreEqual(5f, value.Get(Tier.Legend));
    }
}
