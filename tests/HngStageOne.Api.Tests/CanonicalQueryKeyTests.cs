using HngStageOne.Api.Models;
using HngStageOne.Api.Services;
using HngStageOne.Api.Services.Caching;

namespace HngStageOne.Api.Tests;

/// <summary>
/// Verifies the Stage 4B normalization contract: semantically equivalent natural-language
/// queries must produce identical canonical cache keys.
/// </summary>
public class CanonicalQueryKeyTests
{
    private static readonly NaturalLanguageProfileQueryParser Parser = new();

    [Fact]
    public void ParaphrasedQueries_ProduceSameCanonicalKey()
    {
        var a = Parser.Parse("Nigerian females between ages 20 and 45");
        var b = Parser.Parse("Women aged 20-45 living in Nigeria");

        // Apply identical paging so the comparison is filter-only.
        a.Page = 1; a.Limit = 10;
        b.Page = 1; b.Limit = 10;

        Assert.Equal(CanonicalQueryKey.ForList(a), CanonicalQueryKey.ForList(b));
    }

    [Fact]
    public void DashAndUnicodeRange_AreEquivalent()
    {
        var a = Parser.Parse("women aged 20-45 in nigeria");
        var b = Parser.Parse("women aged 20–45 in nigeria");
        a.Page = 1; a.Limit = 10;
        b.Page = 1; b.Limit = 10;

        Assert.Equal(CanonicalQueryKey.ForList(a), CanonicalQueryKey.ForList(b));
    }

    [Fact]
    public void DifferentPaging_ProducesDifferentKeys()
    {
        var a = Parser.Parse("women aged 20-45 in nigeria");
        var b = Parser.Parse("women aged 20-45 in nigeria");
        a.Page = 1; a.Limit = 10;
        b.Page = 2; b.Limit = 10;

        Assert.NotEqual(CanonicalQueryKey.ForList(a), CanonicalQueryKey.ForList(b));
    }

    [Fact]
    public void CaseDifferencesAreNormalized()
    {
        var a = new ProfileQueryOptions { Gender = "female", CountryId = "NG", MinAge = 20, MaxAge = 45 };
        var b = new ProfileQueryOptions { Gender = "FEMALE", CountryId = "ng", MinAge = 20, MaxAge = 45 };

        Assert.Equal(CanonicalQueryKey.ForList(a), CanonicalQueryKey.ForList(b));
    }

    [Theory]
    [InlineData("kenyan men", "male", "KE")]
    [InlineData("british teenagers", null, "GB")]
    [InlineData("males above 30 from kenya", "male", "KE")]
    [InlineData("adult males from kenya", "male", "KE")]
    public void DemonymAndPrepositionalForms_ResolveCountry(string input, string? expectedGender, string expectedCountry)
    {
        var options = Parser.Parse(input);
        Assert.Equal(expectedGender, options.Gender);
        Assert.Equal(expectedCountry, options.CountryId);
    }
}
