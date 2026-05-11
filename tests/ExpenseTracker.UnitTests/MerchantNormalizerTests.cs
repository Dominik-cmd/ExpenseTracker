using ExpenseTracker.Application.Services;
using FluentAssertions;

namespace ExpenseTracker.UnitTests;

public sealed class MerchantNormalizerTests
{
    [Fact]
    public void Normalize_ShouldUppercaseAndTrimWhitespace()
    {
        var result = MerchantNormalizer.Normalize("   mercator   ");

        result.Should().Be("MERCATOR");
    }

    [Theory]
    [InlineData("MERCATOR MARIBOR SI")]
    [InlineData("MERCATOR LJUBLJANA SI")]
    [InlineData("MERCATOR CELJE SI")]
    [InlineData("MERCATOR KOPER SI")]
    [InlineData("MERCATOR KRANJ SI")]
    [InlineData("MERCATOR MURSKA SOBOTA SI")]
    [InlineData("MERCATOR NOVO MESTO SI")]
    [InlineData("MERCATOR PTUJ SI")]
    public void Normalize_ShouldStripKnownCityAndCountrySuffix(string merchantRaw)
    {
        var result = MerchantNormalizer.Normalize(merchantRaw);

        result.Should().Be("MERCATOR");
    }

    [Fact]
    public void Normalize_ShouldLeaveMerchantUnchangedWhenNoCityMatchExists()
    {
        var result = MerchantNormalizer.Normalize("Telekom Slovenije");

        result.Should().Be("TELEKOM SLOVENIJE");
    }
}
