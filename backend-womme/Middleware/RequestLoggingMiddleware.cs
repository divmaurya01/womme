using System.Text;
using Microsoft.AspNetCore.Http;
 
namespace WommeAPI.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly string LogFolder = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
 
        // Semaphore for async thread-safe logging
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
 
        public RequestLoggingMiddleware(RequestDelegate next)
        {
            _next = next;
 
            if (!Directory.Exists(LogFolder))
                Directory.CreateDirectory(LogFolder);
        }
 
        public async Task InvokeAsync(HttpContext context)
        {
            var requestTime = DateTime.Now;
            int statusCode = 200;
 
            try
            {
                await _next(context); // Call the next middleware
                statusCode = context.Response.StatusCode;
            }
            catch (Exception ex)
            {
                statusCode = 500;
                await LogAsync($"[{requestTime:yyyy-MM-dd HH:mm:ss}] API: {context.Request.Method} {context.Request.Path}{context.Request.QueryString} Status: 500 ERROR: {ex.Message}");
                throw;
            }
 
            await LogAsync($"[{requestTime:yyyy-MM-dd HH:mm:ss}] API: {context.Request.Method} {context.Request.Path}{context.Request.QueryString} Status: {statusCode}");
        }
 
        private async Task LogAsync(string message)
        {
            string logFilePath = Path.Combine(LogFolder, $"api_log_{DateTime.Now:yyyy-MM-dd}.txt");
 
            await _semaphore.WaitAsync();
            try
            {
                await using var stream = new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                await using var writer = new StreamWriter(stream, Encoding.UTF8);
                await writer.WriteLineAsync(message);
            }
            finally
            {
                _semaphore.Release();
            }
 
            await DeleteOldLogsAsync();
        }
 
        private async Task DeleteOldLogsAsync()
        {
            var files = Directory.GetFiles(LogFolder, "api_log_*.txt");
            foreach (var file in files)
            {
                if (File.GetCreationTime(file) < DateTime.Now.AddMonths(-1))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch { /* ignore delete errors */ }
                }
            }
 
            await Task.CompletedTask;
        }
    }
 
    // Extension method
    public static class RequestLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
            => builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}