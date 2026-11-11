using FurnitureEcommerce.Application.Orders;

namespace FurnitureEcommerce.Api.BackgroundServices;

/// <summary>Runs the order reconciliation on a fixed interval for as long as the API is running.</summary>
public class OrderReconciliationWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<OrderReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(configuration.GetValue("Reconciliation:IntervalSeconds", 60));
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                // The services use a database context, which lives per scope, not for the whole application.
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IOrderReconciliationService>();

                var summary = await service.RunOnceAsync(stoppingToken);

                if (summary.Total > 0)
                {
                    logger.LogInformation("Order reconciliation finished: {Summary}", summary);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A failed run must not end the loop. The next run tries again.
                logger.LogError(exception, "Order reconciliation failed.");
            }
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
