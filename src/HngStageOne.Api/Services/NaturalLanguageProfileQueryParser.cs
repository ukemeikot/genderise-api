using System.Text.RegularExpressions;
using HngStageOne.Api.Helpers;
using HngStageOne.Api.Helpers.Exceptions;
using HngStageOne.Api.Models;
using HngStageOne.Api.Services.Interfaces;

namespace HngStageOne.Api.Services;

/// <summary>
/// Rule-based parser that maps a small, controlled set of phrasings to a canonical
/// <see cref="ProfileQueryOptions"/>. Designed so semantically-equivalent phrasings
/// produce structurally-identical filter objects (Stage 4B query normalization).
///
/// Parser is deterministic. No AI / LLMs.
/// </summary>
public partial class NaturalLanguageProfileQueryParser : INaturalLanguageProfileQueryParser
{
    public ProfileQueryOptions Parse(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new MissingOrEmptyParameterException();
        }

        var normalized = NormalizeQuery(query);
        var options = new ProfileQueryOptions();
        var matchedSomething = false;

        // Gender (women/men/females/males/girls/boys/...) — exclusive male XOR female
        var mentionsMale = GenderMaleRegex().IsMatch(normalized);
        var mentionsFemale = GenderFemaleRegex().IsMatch(normalized);

        if (mentionsMale ^ mentionsFemale)
        {
            options.Gender = mentionsMale ? "male" : "female";
            matchedSomething = true;
        }
        else if (mentionsMale || mentionsFemale)
        {
            // Both mentioned: treat as no gender filter, but it's still a recognized intent.
            matchedSomething = true;
        }

        // Age group keywords
        if (AgeGroupTeenagerRegex().IsMatch(normalized))
        {
            options.AgeGroup = "teenager";
            matchedSomething = true;
        }
        else if (AgeGroupChildRegex().IsMatch(normalized))
        {
            options.AgeGroup = "child";
            matchedSomething = true;
        }
        else if (AgeGroupAdultRegex().IsMatch(normalized))
        {
            options.AgeGroup = "adult";
            matchedSomething = true;
        }
        else if (AgeGroupSeniorRegex().IsMatch(normalized))
        {
            options.AgeGroup = "senior";
            matchedSomething = true;
        }

        // "young" → 16..24 shorthand
        if (YoungRegex().IsMatch(normalized))
        {
            options.MinAge = 16;
            options.MaxAge = 24;
            matchedSomething = true;
        }

        // "between 20 and 45" / "aged 20 to 45" / "ages 20-45" / "20-45 years old"
        var rangeMatch = AgeRangeRegex().Match(normalized);
        if (rangeMatch.Success)
        {
            options.MinAge = int.Parse(rangeMatch.Groups["min"].Value);
            options.MaxAge = int.Parse(rangeMatch.Groups["max"].Value);
            matchedSomething = true;
        }
        else
        {
            // single-bound forms (above N, below N, at least N, at most N, over N, under N)
            var aboveMatch = AboveAgeRegex().Match(normalized);
            if (aboveMatch.Success)
            {
                options.MinAge = int.Parse(aboveMatch.Groups["age"].Value);
                matchedSomething = true;
            }

            var belowMatch = BelowAgeRegex().Match(normalized);
            if (belowMatch.Success)
            {
                options.MaxAge = int.Parse(belowMatch.Groups["age"].Value);
                matchedSomething = true;
            }
        }

        // Country: prefer the prepositional form ("from Nigeria", "in Nigeria", "living in Nigeria"),
        // then fall back to demonym recognition ("Nigerian females").
        var countryFromPreposition = TryResolveCountryFromPreposition(normalized);
        var countryFromDemonym = TryResolveCountryFromDemonym(normalized);

        if (countryFromPreposition is not null)
        {
            options.CountryId = countryFromPreposition;
            matchedSomething = true;
        }
        else if (countryFromDemonym is not null)
        {
            options.CountryId = countryFromDemonym;
            matchedSomething = true;
        }

        if (!matchedSomething)
        {
            throw new UnableToInterpretQueryException();
        }

        if (options.MinAge.HasValue && options.MaxAge.HasValue && options.MinAge > options.MaxAge)
        {
            throw new InvalidQueryParametersException();
        }

        return options;
    }

    private static string? TryResolveCountryFromPreposition(string normalized)
    {
        var match = CountryPrepositionalRegex().Match(normalized);
        if (!match.Success) return null;

        var raw = match.Groups["country"].Value.Trim().Trim('.', ',', ';');
        if (string.IsNullOrWhiteSpace(raw)) return null;

        if (CountryLookup.TryResolveCode(raw, out var code))
        {
            return code;
        }

        // Allow demonym-shaped tokens after a preposition (e.g., "in Nigerian context" — rare).
        if (DemonymLookup.TryResolveCountryAlias(raw, out var alias)
            && CountryLookup.TryResolveCode(alias, out code))
        {
            return code;
        }

        throw new UnableToInterpretQueryException();
    }

    private static string? TryResolveCountryFromDemonym(string normalized)
    {
        // Try multi-word demonyms first ("south african") then single-word ("nigerian").
        foreach (var match in DemonymRegex().Matches(normalized).Cast<Match>())
        {
            var token = match.Groups["demonym"].Value.Trim();
            if (DemonymLookup.TryResolveCountryAlias(token, out var alias)
                && CountryLookup.TryResolveCode(alias, out var code))
            {
                return code;
            }
        }

        return null;
    }

    private static string NormalizeQuery(string query)
    {
        var lowered = query.Trim().ToLowerInvariant();
        // Treat unicode dashes as plain hyphens so "20–45" parses identically to "20-45".
        lowered = lowered.Replace('–', '-').Replace('—', '-');
        return MultiWhitespaceRegex().Replace(lowered, " ");
    }

    [GeneratedRegex(@"\b(male|males|man|men|boy|boys)\b")]
    private static partial Regex GenderMaleRegex();

    [GeneratedRegex(@"\b(female|females|woman|women|girl|girls)\b")]
    private static partial Regex GenderFemaleRegex();

    [GeneratedRegex(@"\b(teenager|teenagers|teen|teens)\b")]
    private static partial Regex AgeGroupTeenagerRegex();

    [GeneratedRegex(@"\b(child|children|kid|kids)\b")]
    private static partial Regex AgeGroupChildRegex();

    [GeneratedRegex(@"\b(adult|adults)\b")]
    private static partial Regex AgeGroupAdultRegex();

    [GeneratedRegex(@"\b(senior|seniors|elderly)\b")]
    private static partial Regex AgeGroupSeniorRegex();

    [GeneratedRegex(@"\byoung\b")]
    private static partial Regex YoungRegex();

    /// <summary>
    /// Range forms covered:
    ///   "between 20 and 45"
    ///   "between ages 20 and 45"
    ///   "aged 20 to 45"
    ///   "aged 20-45"
    ///   "ages 20 to 45"
    ///   "ages 20-45"
    ///   "20 to 45 years old"
    ///   "20-45 years old"
    /// </summary>
    [GeneratedRegex(@"\b(?:between(?:\s+ages?)?|aged|ages|from\s+age)\s+(?<min>\d{1,3})\s*(?:and|to|-)\s*(?<max>\d{1,3})\b|\b(?<min>\d{1,3})\s*-\s*(?<max>\d{1,3})\s+years?\s+old\b|\b(?<min>\d{1,3})\s+to\s+(?<max>\d{1,3})\s+years?\s+old\b")]
    private static partial Regex AgeRangeRegex();

    [GeneratedRegex(@"\b(?:above|over|older than|at least)\s+(?<age>\d{1,3})\b")]
    private static partial Regex AboveAgeRegex();

    [GeneratedRegex(@"\b(?:below|under|younger than|at most)\s+(?<age>\d{1,3})\b")]
    private static partial Regex BelowAgeRegex();

    /// <summary>
    /// Stops at known terminator words so "from kenya above 30" only captures "kenya".
    /// </summary>
    [GeneratedRegex(@"\b(?:from|in|living\s+in|based\s+in|located\s+in|residing\s+in)\s+(?<country>[a-z][a-z\s'.\-]*?)(?=\s+\b(?:above|over|older than|at least|below|under|younger than|at most|between|aged|ages|and|with|having|who|that)\b|\s*$|\s*[,.;])")]
    private static partial Regex CountryPrepositionalRegex();

    /// <summary>
    /// Recognises adjectival country forms preceding a population noun.
    /// "nigerian females", "south african men", "british teenagers".
    /// </summary>
    [GeneratedRegex(@"\b(?<demonym>(?:south\s+african|new\s+zealander|[a-z]+))\s+(?:males?|females?|men|women|boys?|girls?|teenagers?|teens?|adults?|kids?|children|seniors?|elderly|people|persons?)\b")]
    private static partial Regex DemonymRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiWhitespaceRegex();
}
