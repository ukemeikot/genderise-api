using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using HngStageOne.Api.Data;
using HngStageOne.Api.Domain.Entities;
using HngStageOne.Api.DTOs.Responses;
using HngStageOne.Api.Helpers;
using HngStageOne.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HngStageOne.Api.Services;

/// <summary>
/// Streaming CSV ingester with per-batch upsert semantics.
///
/// Hard requirements (Stage 4B):
///   * Never load the whole file into memory — uses CsvHelper's streaming reader.
///   * Never insert rows one by one — batches into transactions of <see cref="BatchSize"/> rows.
///   * One bad row never aborts the upload — every row is wrapped in try/catch, classified, and counted.
///   * On partial failure, already-committed batches stay in the database.
///   * Concurrent uploads: each call uses its own DbContext via the factory; no shared mutable state.
/// </summary>
public sealed class CsvIngestionService : ICsvIngestionService
{
    private const int BatchSize = 1000;

    private static readonly HashSet<string> ValidGenders =
        new(StringComparer.OrdinalIgnoreCase) { "male", "female" };

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IProfileService _profileService;
    private readonly ILogger<CsvIngestionService> _logger;

    public CsvIngestionService(
        IDbContextFactory<AppDbContext> dbFactory,
        IProfileService profileService,
        ILogger<CsvIngestionService> logger)
    {
        _dbFactory = dbFactory;
        _profileService = profileService;
        _logger = logger;
    }

    public async Task<CsvUploadResponse> IngestAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        var response = new CsvUploadResponse();

        // Buffered text reader keeps memory bounded regardless of file size.
        using var reader = new StreamReader(fileStream, leaveOpen: false);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            BadDataFound = null,
            MissingFieldFound = null,
            HeaderValidated = null,
            TrimOptions = TrimOptions.Trim,
            Delimiter = ",",
            DetectColumnCountChanges = false
        });

        // Read header once. Map by name so column order doesn't matter.
        try
        {
            await csv.ReadAsync();
            csv.ReadHeader();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CSV header could not be read");
            response.Status = "error";
            return response;
        }

        if (csv.HeaderRecord is null || csv.HeaderRecord.Length == 0)
        {
            response.Status = "error";
            return response;
        }

        var headerSet = new HashSet<string>(csv.HeaderRecord, StringComparer.OrdinalIgnoreCase);
        if (!headerSet.Contains("name") || !headerSet.Contains("gender") || !headerSet.Contains("age") || !headerSet.Contains("country_id"))
        {
            response.Status = "error";
            response.Reasons["missing_required_columns"] = 1;
            return response;
        }

        var batch = new List<Profile>(BatchSize);
        var batchNames = new HashSet<string>(BatchSize, StringComparer.OrdinalIgnoreCase);

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            response.TotalRows++;

            ParsedRow parsed;
            try
            {
                parsed = ParseRow(csv);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Row {Row} malformed", response.TotalRows);
                Skip(response, "malformed");
                continue;
            }

            if (parsed.SkipReason is not null)
            {
                Skip(response, parsed.SkipReason);
                continue;
            }

            // In-batch duplicate check (same name appearing twice in the same file).
            if (!batchNames.Add(parsed.Profile!.Name))
            {
                Skip(response, "duplicate_name");
                continue;
            }

            batch.Add(parsed.Profile);

            if (batch.Count >= BatchSize)
            {
                await FlushBatchAsync(batch, batchNames, response, cancellationToken);
                batch.Clear();
                batchNames.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await FlushBatchAsync(batch, batchNames, response, cancellationToken);
        }

        response.Skipped = response.TotalRows - response.Inserted;

        // Best-effort cache invalidation. Failure here doesn't fail the upload.
        try
        {
            await _profileService.InvalidateReadCachesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache invalidation after CSV ingest failed");
        }

        return response;
    }

    private async Task FlushBatchAsync(
        List<Profile> batch,
        HashSet<string> batchNames,
        CsvUploadResponse response,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0) return;

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            // Single round-trip duplicate check against DB for the whole batch.
            var existing = await db.Profiles
                .AsNoTracking()
                .Where(p => batchNames.Contains(p.Name))
                .Select(p => p.Name)
                .ToListAsync(cancellationToken);

            var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
            var toInsert = batch.Where(p => !existingSet.Contains(p.Name)).ToList();
            var droppedAsDuplicate = batch.Count - toInsert.Count;
            for (var i = 0; i < droppedAsDuplicate; i++)
            {
                Skip(response, "duplicate_name");
            }

            if (toInsert.Count == 0) return;

            await db.Profiles.AddRangeAsync(toInsert, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            response.Inserted += toInsert.Count;
        }
        catch (DbUpdateException ex)
        {
            // A unique-name collision can still race here on concurrent uploads.
            // EF Core 9 does not surface per-row errors from a single SaveChanges; treat the
            // whole failed batch as duplicate-skipped and keep going.
            _logger.LogWarning(ex, "Batch insert failed; counting batch as duplicate-skipped");
            for (var i = 0; i < batch.Count; i++)
            {
                Skip(response, "duplicate_name");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch insert failed unexpectedly");
            for (var i = 0; i < batch.Count; i++)
            {
                Skip(response, "batch_failed");
            }
        }
    }

    private static ParsedRow ParseRow(CsvReader csv)
    {
        var name = csv.GetField("name")?.Trim();
        var gender = csv.GetField("gender")?.Trim().ToLowerInvariant();
        var ageRaw = csv.GetField("age")?.Trim();
        var countryIdRaw = csv.GetField("country_id")?.Trim();

        if (string.IsNullOrWhiteSpace(name)
            || string.IsNullOrWhiteSpace(gender)
            || string.IsNullOrWhiteSpace(ageRaw)
            || string.IsNullOrWhiteSpace(countryIdRaw))
        {
            return ParsedRow.SkippedAs("missing_fields");
        }

        if (!ValidGenders.Contains(gender))
        {
            return ParsedRow.SkippedAs("invalid_gender");
        }

        if (!int.TryParse(ageRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var age))
        {
            return ParsedRow.SkippedAs("invalid_age");
        }

        if (age < 0 || age > 120)
        {
            return ParsedRow.SkippedAs("invalid_age");
        }

        var countryId = countryIdRaw.ToUpperInvariant();
        if (countryId.Length != 2 || !countryId.All(char.IsLetter))
        {
            return ParsedRow.SkippedAs("invalid_country");
        }

        var countryName = TryGetField(csv, "country_name");
        if (string.IsNullOrWhiteSpace(countryName))
        {
            countryName = CountryLookup.ResolveName(countryId);
        }

        var ageGroupRaw = TryGetField(csv, "age_group");
        var ageGroup = string.IsNullOrWhiteSpace(ageGroupRaw)
            ? AgeGroupClassifier.Classify(age)
            : ageGroupRaw.Trim().ToLowerInvariant();

        var genderProbability = ParseProbability(TryGetField(csv, "gender_probability"), 1.0);
        var countryProbability = ParseProbability(TryGetField(csv, "country_probability"), 1.0);

        return ParsedRow.Ok(new Profile
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Gender = gender,
            GenderProbability = genderProbability,
            Age = age,
            AgeGroup = ageGroup,
            CountryId = countryId,
            CountryName = countryName,
            CountryProbability = countryProbability,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static string? TryGetField(CsvReader csv, string name)
    {
        try
        {
            return csv.GetField(name);
        }
        catch
        {
            return null;
        }
    }

    private static double ParseProbability(string? raw, double fallback)
    {
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        if (!double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return fallback;
        if (value < 0) return 0;
        if (value > 1) return 1;
        return value;
    }

    private static void Skip(CsvUploadResponse response, string reason)
    {
        response.Reasons.TryGetValue(reason, out var existing);
        response.Reasons[reason] = existing + 1;
    }

    private readonly struct ParsedRow
    {
        public Profile? Profile { get; init; }
        public string? SkipReason { get; init; }

        public static ParsedRow Ok(Profile profile) => new() { Profile = profile, SkipReason = null };
        public static ParsedRow SkippedAs(string reason) => new() { Profile = null, SkipReason = reason };
    }
}
