using HngStageOne.Api.Clients.Implementations;
using HngStageOne.Api.Clients.Interfaces;
using HngStageOne.Api.Data;
using HngStageOne.Api.Middleware;
using HngStageOne.Api.Repositories.Implementations;
using HngStageOne.Api.Repositories.Interfaces;
using HngStageOne.Api.Services;
using HngStageOne.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Database Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=hng_stage_one.db";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// Services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "HNG Stage 2 Queryable Intelligence API",
        Version = "v1",
        Description = "Queryable demographic intelligence API with advanced filters, pagination, sorting, and natural language search."
    });
});

// Register Repositories
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();

// Register Services
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IProfileQueryValidator, ProfileQueryValidator>();
builder.Services.AddScoped<INaturalLanguageProfileQueryParser, NaturalLanguageProfileQueryParser>();
builder.Services.AddScoped<IProfileSeedService, ProfileSeedService>();

// Register HTTP Clients
builder.Services.AddHttpClient<IGenderizeClient, GenderizeClient>();
builder.Services.AddHttpClient<IAgifyClient, AgifyClient>();
builder.Services.AddHttpClient<INationalizeClient, NationalizeClient>();

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

// Forward headers for reverse proxy (e.g., Nginx on EC2)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Apply Migrations Automatically on startup
// Safe for production as EF Core handles concurrency
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.Migrate();

        var seedLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var seedService = scope.ServiceProvider.GetRequiredService<IProfileSeedService>();
        var seedPath = FindSeedFilePath(app.Environment.ContentRootPath);

        if (seedPath is not null)
        {
            var insertedCount = await seedService.SeedAsync(seedPath);
            seedLogger.LogInformation("Seeded {InsertedCount} profiles from {SeedPath}", insertedCount, seedPath);
        }
        else
        {
            seedLogger.LogWarning("Seed file not found from content root {ContentRootPath}", app.Environment.ContentRootPath);
        }
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Database initialization failed");
        if (app.Environment.IsProduction())
        {
            throw;
        }
    }
}

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "HngStageOne API v1");
        options.RoutePrefix = "swagger";
    });
}

// Use CORS
app.UseCors("AllowAll");

// Global Exception Handling Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Skip HTTPS redirection - let reverse proxy handle it
// app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Health check endpoint
app.MapGet("/", () => Results.Ok(new
{
    status = "success",
    message = "API is running"
}));

await app.RunAsync();

static string? FindSeedFilePath(string contentRootPath)
{
    var directoriesToCheck = new[]
    {
        contentRootPath,
        Directory.GetParent(contentRootPath)?.FullName,
        Directory.GetParent(Directory.GetParent(contentRootPath ?? string.Empty)?.FullName ?? string.Empty)?.FullName
    }
    .Where(path => !string.IsNullOrWhiteSpace(path))
    .Distinct(StringComparer.OrdinalIgnoreCase);

    foreach (var directory in directoriesToCheck)
    {
        var candidatePath = Path.Combine(directory!, "seed_profiles.json");
        if (File.Exists(candidatePath))
        {
            return candidatePath;
        }
    }

    return null;
}
