using System.Text;
using HngStageOne.Api.Data;
using HngStageOne.Api.DTOs.Requests;
using HngStageOne.Api.DTOs.Responses;
using HngStageOne.Api.Services;
using HngStageOne.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HngStageOne.Api.Tests;

/// <summary>
/// Validates the Stage 4B ingestion contract: streaming, batched, resilient to bad rows,
/// idempotent on duplicate names, and never aborting on a single bad row.
/// </summary>
public class CsvIngestionServiceTests : IAsyncLifetime
{
    private SqliteFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new SqliteFactory();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ValidRows_AreInserted()
    {
        var csv = """
            name,gender,age,country_id
            Ada Lovelace,female,36,GB
            Alan Turing,male,41,GB
            """;

        var service = BuildService();
        var result = await service.IngestAsync(ToStream(csv));

        Assert.Equal("success", result.Status);
        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.Inserted);
        Assert.Equal(0, result.Skipped);
    }

    [Fact]
    public async Task BadRowsAreSkippedNotFatal()
    {
        var csv = """
            name,gender,age,country_id
            Valid One,female,30,NG
            ,male,25,NG
            Bad Age,female,abc,NG
            Negative Age,male,-1,NG
            Bad Gender,unknown,30,NG
            """;

        var service = BuildService();
        var result = await service.IngestAsync(ToStream(csv));

        Assert.Equal(5, result.TotalRows);
        Assert.Equal(1, result.Inserted);
        Assert.Equal(4, result.Skipped);
        Assert.Equal(1, result.Reasons["missing_fields"]);
        Assert.Equal(2, result.Reasons["invalid_age"]);
        Assert.Equal(1, result.Reasons["invalid_gender"]);
    }

    [Fact]
    public async Task DuplicateNames_InFile_AreSkipped()
    {
        var csv = """
            name,gender,age,country_id
            Twin Person,female,30,NG
            Twin Person,male,40,KE
            """;

        var service = BuildService();
        var result = await service.IngestAsync(ToStream(csv));

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.Inserted);
        Assert.Equal(1, result.Reasons["duplicate_name"]);
    }

    [Fact]
    public async Task DuplicateNames_AlreadyInDb_AreSkipped()
    {
        var csv = """
            name,gender,age,country_id
            Pre Existing,female,30,NG
            New Person,male,40,KE
            """;

        // seed the DB with one of the names
        using (var db = _factory.CreateDbContext())
        {
            db.Profiles.Add(new HngStageOne.Api.Domain.Entities.Profile
            {
                Id = Guid.NewGuid(),
                Name = "Pre Existing",
                Gender = "female",
                GenderProbability = 1.0,
                Age = 25,
                AgeGroup = "adult",
                CountryId = "NG",
                CountryName = "Nigeria",
                CountryProbability = 1.0,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var service = BuildService();
        var result = await service.IngestAsync(ToStream(csv));

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.Inserted);
        Assert.Equal(1, result.Reasons["duplicate_name"]);
    }

    [Fact]
    public async Task MissingRequiredColumns_FailsCleanly()
    {
        var csv = "name,gender\nfoo,male\n";

        var service = BuildService();
        var result = await service.IngestAsync(ToStream(csv));

        Assert.Equal("error", result.Status);
    }

    [Fact]
    public async Task LargerBatch_StreamsAndPersists()
    {
        var sb = new StringBuilder();
        sb.AppendLine("name,gender,age,country_id");
        for (var i = 0; i < 2_500; i++)
        {
            sb.Append("Person ").Append(i).Append(",female,30,NG").AppendLine();
        }

        var service = BuildService();
        var result = await service.IngestAsync(ToStream(sb.ToString()));

        Assert.Equal(2_500, result.TotalRows);
        Assert.Equal(2_500, result.Inserted);
        using var db = _factory.CreateDbContext();
        Assert.Equal(2_500, db.Profiles.Count());
    }

    private CsvIngestionService BuildService()
        => new(_factory, new NoopProfileService(), NullLogger<CsvIngestionService>.Instance);

    private static Stream ToStream(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

    private sealed class SqliteFactory : IDbContextFactory<AppDbContext>, IDisposable
    {
        private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public SqliteFactory()
        {
            _connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            using var ctx = new AppDbContext(_options);
            ctx.Database.EnsureCreated();
        }

        public AppDbContext CreateDbContext() => new(_options);
        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new AppDbContext(_options));

        public void Dispose() => _connection.Dispose();
    }

    private sealed class NoopProfileService : IProfileService
    {
        public Task<SingleProfileResponse> CreateProfileAsync(CreateProfileRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<SingleProfileResponse> GetProfileByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<ProfilesListResponse> GetProfilesAsync(ProfileQueryRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<ProfilesListResponse> SearchProfilesAsync(ProfileSearchRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<IReadOnlyList<ProfileDetailResponse>> ExportProfilesAsync(ProfileQueryRequest request, string? naturalLanguageQuery, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task DeleteProfileAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task InvalidateReadCachesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
