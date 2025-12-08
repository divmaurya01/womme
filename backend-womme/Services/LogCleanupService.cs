namespace WommeAPI.Services
{
    public class LogCleanupService : BackgroundService
    {
        private readonly string _logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (Directory.Exists(_logDirectory))
                {
                    var logFiles = Directory.GetFiles(_logDirectory, "*.txt");
                    foreach (var file in logFiles)
                    {
                        var creationTime = File.GetCreationTime(file);
                        if (creationTime < DateTime.Now.AddMonths(-1))
                        {
                            File.Delete(file);
                        }
                    }
                }

                await Task.Delay(TimeSpan.FromHours(24), stoppingToken); // run daily
            }
        }
    }
}
