using HngStageOne.Api.DTOs.Responses;

namespace HngStageOne.Api.Services.Interfaces;

public interface ICsvIngestionService
{
    /// <summary>
    /// Streams the CSV from <paramref name="fileStream"/>, validates rows, batch-upserts
    /// valid new ones into the database, and returns a summary of what happened.
    /// Bad rows are skipped, never abort the whole upload. Already-inserted rows from
    /// earlier batches are not rolled back if a later batch fails.
    /// </summary>
    Task<CsvUploadResponse> IngestAsync(Stream fileStream, CancellationToken cancellationToken = default);
}
