using ProPresenterObsBridge.Util;

namespace ProPresenterObsBridge.Tests;

public class RateLimiterTests
{
    [Fact]
    public void FirstCall_AlwaysReturnsTrue()
    {
        var limiter = new RateLimiter(TimeSpan.FromSeconds(10));
        Assert.True(limiter.TryAcquire());
    }

    [Fact]
    public void RapidSecondCall_ReturnsFalse()
    {
        var limiter = new RateLimiter(TimeSpan.FromSeconds(10));
        limiter.TryAcquire();
        Assert.False(limiter.TryAcquire());
    }

    [Fact]
    public async Task AfterInterval_ReturnsTrue()
    {
        var limiter = new RateLimiter(TimeSpan.FromMilliseconds(50));
        limiter.TryAcquire();
        Assert.False(limiter.TryAcquire());

        await Task.Delay(80);
        Assert.True(limiter.TryAcquire());
    }

    [Fact]
    public void ZeroInterval_AlwaysReturnsTrue()
    {
        var limiter = new RateLimiter(TimeSpan.Zero);
        Assert.True(limiter.TryAcquire());
        Assert.True(limiter.TryAcquire());
        Assert.True(limiter.TryAcquire());
    }

    [Fact]
    public async Task AfterAcquire_ResetsTimer()
    {
        var limiter = new RateLimiter(TimeSpan.FromMilliseconds(50));
        limiter.TryAcquire();

        await Task.Delay(80);
        Assert.True(limiter.TryAcquire()); // resets the timer

        // Immediately after, should be blocked again
        Assert.False(limiter.TryAcquire());
    }
}
