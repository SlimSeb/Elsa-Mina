namespace ElsaMina.IntegrationTests.Fixtures;

/// <summary>
/// Polling helpers for the parts of the card games that are driven by a timer rather than by a call.
/// </summary>
public static class Wait
{
    private static readonly TimeSpan POLL_INTERVAL = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// Generous on purpose: a loaded CI runner is far slower than a development machine, and a wait
    /// that ends early turns a slow machine into a failing build.
    /// </summary>
    private static readonly TimeSpan DEFAULT_TIMEOUT = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Waits until <paramref name="condition"/> holds, failing the test if it never does.
    /// </summary>
    public static async Task UntilAsync(Func<bool> condition, string description, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? DEFAULT_TIMEOUT);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(POLL_INTERVAL);
        }

        Assert.Fail($"timed out waiting for {description}");
    }

    /// <summary>
    /// Gives any pending timer a chance to fire before the test asserts that nothing happened.
    /// </summary>
    public static Task ForQuietPeriodAsync(TimeSpan period) => Task.Delay(period);
}
