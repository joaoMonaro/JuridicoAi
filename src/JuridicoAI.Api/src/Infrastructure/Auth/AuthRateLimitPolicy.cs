using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace JuridicoAI.Api.Infrastructure.Auth;

public static class AuthRateLimitPolicy
{
    public const string Name = "auth";

    public static void Configure(RateLimiterOptions options)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy(Name, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    }
}
