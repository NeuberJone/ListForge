namespace ListForge.Services;

internal static class UpdateCheckPolicy
{
    internal static readonly TimeSpan AutomaticCheckInterval = TimeSpan.FromHours(24);
    internal static readonly TimeSpan ManualCheckInterval = TimeSpan.FromMinutes(1);

    internal static bool ShouldRunAutomaticCheck(DateTimeOffset? lastCheckUtc, DateTimeOffset nowUtc)
    {
        if (!lastCheckUtc.HasValue)
            return true;

        var elapsed = nowUtc - lastCheckUtc.Value;
        return elapsed < TimeSpan.Zero || elapsed >= AutomaticCheckInterval;
    }

    internal static bool ShouldRunManualCheck(DateTimeOffset? lastManualCheckUtc, DateTimeOffset nowUtc)
    {
        if (!lastManualCheckUtc.HasValue)
            return true;

        var elapsed = nowUtc - lastManualCheckUtc.Value;
        return elapsed < TimeSpan.Zero || elapsed >= ManualCheckInterval;
    }
}
