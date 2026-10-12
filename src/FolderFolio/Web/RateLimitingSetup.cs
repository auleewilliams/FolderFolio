using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FolderFolio.Web;

public static class RateLimitingSetup
{
    public const string MediaPolicyName = "media";
    public const string HealthPolicyName = "health";

    public static void Configure(RateLimiterOptions options)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = static (context, _) =>
        {
            context.HttpContext.Response.Headers.RetryAfter = "60";
            return ValueTask.CompletedTask;
        };

        options.AddPolicy(MediaPolicyName, CreatePolicy(60));
        options.AddPolicy(HealthPolicyName, CreatePolicy(10));
    }

    private static Func<HttpContext, RateLimitPartition<string>> CreatePolicy(int permitLimit) => context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            GetPartitionKey(context),
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });

    private static string GetPartitionKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
