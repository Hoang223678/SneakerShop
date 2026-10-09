using Microsoft.Extensions.Logging;

namespace SneakerShop.BuildingBlocks;

/// <summary>Retries startup work (e.g. database creation) while SQL Server is still booting.</summary>
public static class StartupRetry
{
    public static async Task RunAsync(Func<Task> action, ILogger logger, int attempts = 10, int delaySeconds = 3)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception ex) when (attempt < attempts)
            {
                logger.LogWarning("Startup step failed (attempt {Attempt}/{Max}): {Message}. Retrying in {Delay}s...",
                    attempt, attempts, ex.Message, delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
        }
    }
}
