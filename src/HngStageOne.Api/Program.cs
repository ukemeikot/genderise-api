using HngStageOne.Api.Clients.Implementations;
using HngStageOne.Api.Clients.Interfaces;
using HngStageOne.Api.Data;
using HngStageOne.Api.Middleware;
using HngStageOne.Api.Repositories.Implementations;
using HngStageOne.Api.Repositories.Interfaces;
using HngStageOne.Api.Services;
using HngStageOne.Api.Services.Interfaces;
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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "HngStageOne API",
        Version = "v1",
        Description = "HNG Stage 1 Backend Assessment API - Profile Classification Service"
    });
});

// Register Repositories
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();

// Register Services
builder.Services.AddScoped<IProfileService, ProfileService>();

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
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Database migration failed");
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
else
{
    // In production, disable Swagger
    app.UseExceptionHandler("/error");
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