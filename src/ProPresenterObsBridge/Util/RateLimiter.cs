namespace ProPresenterObsBridge.Util;

/// <summary>
/// Simple time-based rate limiter. Returns true if at least <paramref name="interval"/>
/// has elapsed since the last time it returned true.
/// </summary>
public sealed class RateLimiter(TimeSpan interval)
{
    private DateTimeOffset _lastAllowed = DateTimeOffset.MinValue;

    public bool TryAcquire()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastAllowed >= interval)
        {
            _lastAllowed = now;
            return true;
        }
        return false;
    }
}
