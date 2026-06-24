using System.Diagnostics;

namespace CMIForge.Services;

public sealed class SystemTelemetryService
{
    private const int MaxRecentRequests = 30;
    private readonly object sync = new();
    private readonly Dictionary<string, RouteMetricAccumulator> routeMetrics = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, long> statusCounts = [];
    private readonly Queue<RecentRequestMetric> recentSlowRequests = [];
    private readonly DateTimeOffset startedAt = DateTimeOffset.UtcNow;
    private long totalRequests;
    private long totalRequestMilliseconds;
    private long slowestRequestMilliseconds;
    private string slowestRequestPath = string.Empty;
    private DateTimeOffset? lastRequestAt;
    private long totalConflictSearches;
    private long totalConflictSearchMilliseconds;
    private long slowestConflictSearchMilliseconds;
    private DateTimeOffset? lastConflictSearchAt;
    private int lastConflictSearchTermCount;
    private int lastConflictSearchResultCount;
    private long lastConflictSearchMilliseconds;

    public DateTimeOffset StartedAt => startedAt;

    public void RecordRequest(string method, string path, int statusCode, long elapsedMilliseconds)
    {
        if (IsStaticAsset(path))
        {
            return;
        }

        var routeKey = NormalizeRouteKey(method, path);
        lock (sync)
        {
            totalRequests++;
            totalRequestMilliseconds += elapsedMilliseconds;
            lastRequestAt = DateTimeOffset.UtcNow;

            if (!statusCounts.TryAdd(statusCode, 1))
            {
                statusCounts[statusCode]++;
            }

            if (!routeMetrics.TryGetValue(routeKey, out var route))
            {
                route = new RouteMetricAccumulator(routeKey);
                routeMetrics[routeKey] = route;
            }

            route.Record(elapsedMilliseconds, statusCode);

            if (elapsedMilliseconds >= slowestRequestMilliseconds)
            {
                slowestRequestMilliseconds = elapsedMilliseconds;
                slowestRequestPath = routeKey;
            }

            if (elapsedMilliseconds >= 750)
            {
                recentSlowRequests.Enqueue(new RecentRequestMetric(routeKey, statusCode, elapsedMilliseconds, DateTimeOffset.UtcNow));
                while (recentSlowRequests.Count > MaxRecentRequests)
                {
                    recentSlowRequests.Dequeue();
                }
            }
        }
    }

    public void RecordConflictSearch(int termCount, int resultCount, long elapsedMilliseconds)
    {
        lock (sync)
        {
            totalConflictSearches++;
            totalConflictSearchMilliseconds += elapsedMilliseconds;
            lastConflictSearchAt = DateTimeOffset.UtcNow;
            lastConflictSearchTermCount = termCount;
            lastConflictSearchResultCount = resultCount;
            lastConflictSearchMilliseconds = elapsedMilliseconds;
            slowestConflictSearchMilliseconds = Math.Max(slowestConflictSearchMilliseconds, elapsedMilliseconds);
        }
    }

    public SystemTelemetrySnapshot GetSnapshot()
    {
        lock (sync)
        {
            return new SystemTelemetrySnapshot(
                startedAt,
                DateTimeOffset.UtcNow - startedAt,
                totalRequests,
                Average(totalRequestMilliseconds, totalRequests),
                slowestRequestMilliseconds,
                slowestRequestPath,
                lastRequestAt,
                statusCounts
                    .OrderBy(x => x.Key)
                    .Select(x => new StatusCodeMetric(x.Key, x.Value))
                    .ToList(),
                routeMetrics.Values
                    .OrderByDescending(x => x.TotalMilliseconds)
                    .Take(10)
                    .Select(x => x.ToSnapshot())
                    .ToList(),
                recentSlowRequests
                    .Reverse()
                    .ToList(),
                totalConflictSearches,
                Average(totalConflictSearchMilliseconds, totalConflictSearches),
                slowestConflictSearchMilliseconds,
                lastConflictSearchAt,
                lastConflictSearchTermCount,
                lastConflictSearchResultCount,
                lastConflictSearchMilliseconds);
        }
    }

    public static async Task TrackRequestAsync(HttpContext context, RequestDelegate next, SystemTelemetryService telemetry)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            telemetry.RecordRequest(
                context.Request.Method,
                context.Request.Path.Value ?? "/",
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }

    private static double Average(long total, long count)
    {
        return count == 0 ? 0 : total / (double)count;
    }

    private static bool IsStaticAsset(string path)
    {
        return path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/images/", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRouteKey(string method, string path)
    {
        var cleanPath = string.IsNullOrWhiteSpace(path) ? "/" : path.Split('?', '#')[0];
        var segments = cleanPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => Guid.TryParse(segment, out _) ? "{id}" : segment)
            .Select(segment => int.TryParse(segment, out _) ? "{number}" : segment);

        return $"{method.ToUpperInvariant()} /{string.Join('/', segments)}";
    }

    private sealed class RouteMetricAccumulator(string route)
    {
        private long totalMilliseconds;
        private long slowestMilliseconds;

        public string Route { get; } = route;

        public long Count { get; private set; }

        public long TotalMilliseconds => totalMilliseconds;

        public int LastStatusCode { get; private set; }

        public void Record(long elapsedMilliseconds, int statusCode)
        {
            Count++;
            totalMilliseconds += elapsedMilliseconds;
            slowestMilliseconds = Math.Max(slowestMilliseconds, elapsedMilliseconds);
            LastStatusCode = statusCode;
        }

        public RouteMetric ToSnapshot()
        {
            return new RouteMetric(Route, Count, Average(totalMilliseconds, Count), slowestMilliseconds, LastStatusCode);
        }
    }
}

public sealed record SystemTelemetrySnapshot(
    DateTimeOffset StartedAt,
    TimeSpan Uptime,
    long TotalRequests,
    double AverageRequestMilliseconds,
    long SlowestRequestMilliseconds,
    string SlowestRequestPath,
    DateTimeOffset? LastRequestAt,
    IReadOnlyList<StatusCodeMetric> StatusCodes,
    IReadOnlyList<RouteMetric> TopRoutes,
    IReadOnlyList<RecentRequestMetric> RecentSlowRequests,
    long TotalConflictSearches,
    double AverageConflictSearchMilliseconds,
    long SlowestConflictSearchMilliseconds,
    DateTimeOffset? LastConflictSearchAt,
    int LastConflictSearchTermCount,
    int LastConflictSearchResultCount,
    long LastConflictSearchMilliseconds);

public sealed record RouteMetric(string Route, long Count, double AverageMilliseconds, long SlowestMilliseconds, int LastStatusCode);

public sealed record StatusCodeMetric(int StatusCode, long Count);

public sealed record RecentRequestMetric(string Route, int StatusCode, long ElapsedMilliseconds, DateTimeOffset CapturedAt);
