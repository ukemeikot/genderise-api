using System.Globalization;
using System.Text;
using HngStageOne.Api.Models;

namespace HngStageOne.Api.Services.Caching;

/// <summary>
/// Canonical, deterministic representation of a profile query.
/// Two semantically equivalent queries (same filters, same ordering, same paging) produce
/// the same key regardless of how they were expressed by the caller.
/// </summary>
public static class CanonicalQueryKey
{
    public static string ForList(ProfileQueryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var builder = new StringBuilder(160);
        AppendField(builder, "gender",   Lower(options.Gender));
        AppendField(builder, "age_group", Lower(options.AgeGroup));
        AppendField(builder, "country",  Upper(options.CountryId));
        AppendField(builder, "min_age",  options.MinAge?.ToString(CultureInfo.InvariantCulture));
        AppendField(builder, "max_age",  options.MaxAge?.ToString(CultureInfo.InvariantCulture));
        AppendField(builder, "min_gp",   FormatProbability(options.MinGenderProbability));
        AppendField(builder, "min_cp",   FormatProbability(options.MinCountryProbability));
        AppendField(builder, "sort",     Lower(options.SortBy) ?? "created_at");
        AppendField(builder, "order",    Lower(options.Order)  ?? "desc");
        AppendField(builder, "page",     options.Page.ToString(CultureInfo.InvariantCulture));
        AppendField(builder, "limit",    options.Limit.ToString(CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    public static string ForExport(ProfileQueryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var builder = new StringBuilder(160);
        AppendField(builder, "gender",   Lower(options.Gender));
        AppendField(builder, "age_group", Lower(options.AgeGroup));
        AppendField(builder, "country",  Upper(options.CountryId));
        AppendField(builder, "min_age",  options.MinAge?.ToString(CultureInfo.InvariantCulture));
        AppendField(builder, "max_age",  options.MaxAge?.ToString(CultureInfo.InvariantCulture));
        AppendField(builder, "min_gp",   FormatProbability(options.MinGenderProbability));
        AppendField(builder, "min_cp",   FormatProbability(options.MinCountryProbability));
        AppendField(builder, "sort",     Lower(options.SortBy) ?? "created_at");
        AppendField(builder, "order",    Lower(options.Order)  ?? "desc");
        return builder.ToString();
    }

    public static string ForSingle(Guid id) => id.ToString("N");

    private static void AppendField(StringBuilder builder, string name, string? value)
    {
        if (value is null) return;
        if (builder.Length > 0) builder.Append('|');
        builder.Append(name).Append('=').Append(value);
    }

    private static string? Lower(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim().ToLowerInvariant();
    }

    private static string? Upper(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim().ToUpperInvariant();
    }

    private static string? FormatProbability(double? value)
    {
        if (!value.HasValue) return null;
        return value.Value.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
