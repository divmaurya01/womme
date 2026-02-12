using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

public class DataSyncScheduler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataSyncScheduler> _logger;
    private bool _isRunning = false;

    public DataSyncScheduler(IServiceProvider serviceProvider, ILogger<DataSyncScheduler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public void Start(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            _logger.LogWarning("Scheduler is already running. Ignoring new start request.");
            return;
        }

        _isRunning = true;
        _logger.LogInformation("Scheduler started at {time}", DateTime.UtcNow);

        Task.Run(async () =>
        {
            try
            {
                var timer = new PeriodicTimer(TimeSpan.FromMinutes(5)); // every 5 minutes

                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    _logger.LogInformation("Starting sync cycle at {time}", DateTime.UtcNow);

                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();

                        await syncService.SyncJobMstAsync();
                        _logger.LogInformation("SyncJobMstAsync completed successfully.");

                        await syncService.SyncJobRouteAsync();
                        _logger.LogInformation("SyncJobRouteAsync completed successfully.");

                        await syncService.SyncJobMatlMstAsync();
                        _logger.LogInformation("SyncJobMatlMstAsync completed successfully.");

                        await syncService.SyncWcMstAsync();
                        _logger.LogInformation("SyncWcMstAsync completed successfully.");

                        await syncService.SyncEmployeeMstAsync();
                        _logger.LogInformation("SyncEmployeeMstAsync completed successfully.");

                        // Optional: Uncomment if needed
                       //  await syncService.SyncJobTranMstAsync();
                         await syncService.SyncJobSchMstAsync();
                          _logger.LogInformation("SyncItemMstAsync completed successfully."); 

                        await syncService.SyncItemMstAsync();
                        _logger.LogInformation("SyncItemMstAsync completed successfully.");

                         await syncService.SyncJobTranMstAsync();
                        _logger.LogInformation("SyncJobMatlMstAsync completed successfully.");

                        await syncService.SyncWomWcEmployeeAsync();
                        _logger.LogInformation("SyncWomWcEmployeeAsync completed successfully.");

                        _logger.LogInformation("Sync cycle finished at {time}", DateTime.UtcNow);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Sync cycle cancelled at {time}", DateTime.UtcNow);
                        break; // exit loop gracefully
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error occurred during sync cycle at {time}", DateTime.UtcNow);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error in scheduler at {time}", DateTime.UtcNow);
            }
            finally
            {
                _isRunning = false; // mark as stopped
                _logger.LogInformation("Scheduler stopped at {time}", DateTime.UtcNow);
            }
        }, cancellationToken);
    }
}

