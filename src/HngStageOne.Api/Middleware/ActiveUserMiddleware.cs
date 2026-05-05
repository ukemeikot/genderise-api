using System.Security.Claims;
using HngStageOne.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace HngStageOne.Api.Middleware;

/// <summary>
/// Verifies the bearer token resolves to an active user. The result of this check
/// is cached for a short window to avoid hitting the database on every request,
/// which is what made auth-protected reads slow when the database is remote.
/// </summary>
public class ActiveUserMiddleware
{
    private const string CachePrefix = "user:active:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly RequestDelegate _next;

    public ActiveUserMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext, IDistributedCache cache)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var idValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? context.User.FindFirstValue("sub");

        if (!Guid.TryParse(idValue, out var userId))
        {
            await DenyAsync(context);
            return;
        }

        var cacheKey = CachePrefix + userId.ToString("N");
        var cached = await cache.GetStringAsync(cacheKey, context.RequestAborted);

        bool isActive;
        if (cached is not null)
        {
            isActive = cached == "1";
        }
        else
        {
            isActive = await dbContext.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == userId && user.IsActive, context.RequestAborted);

            await cache.SetStringAsync(cacheKey, isActive ? "1" : "0", new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl
            }, context.RequestAborted);
        }

        if (!isActive)
        {
            await DenyAsync(context);
            return;
        }

        await _next(context);
    }

    private static Task DenyAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return context.Response.WriteAsJsonAsync(new { status = "error", message = "User is inactive" });
    }
}
