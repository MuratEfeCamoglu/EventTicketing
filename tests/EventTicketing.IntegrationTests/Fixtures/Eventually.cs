using System.Diagnostics;

namespace EventTicketing.IntegrationTests.Fixtures;

/// <summary>
/// Polls for the result of work that happens on another process or thread (RabbitMQ delivery, the Notification service,
/// Mailpit). This waits for an outcome, not for time to pass, so it does not replace <c>FakeTimeProvider</c>.
/// </summary>
public static class Eventually
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>Returns the first value of <paramref name="probe"/> that satisfies <paramref name="done"/>; fails the test after <paramref name="timeout"/>.</summary>
    public static async Task<T> WaitForAsync<T>(Func<Task<T>> probe, Func<T, bool> done, TimeSpan timeout, string because)
    {
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            var value = await probe();
            if (done(value))
            {
                return value;
            }

            if (Stopwatch.GetElapsedTime(started) >= timeout)
            {
                throw new TimeoutException($"Not satisfied within {timeout}: {because}");
            }

            await Task.Delay(PollInterval, TestContext.Current.CancellationToken);
        }
    }
}
