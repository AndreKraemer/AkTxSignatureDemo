using System.Diagnostics;
using System.Reflection;

namespace AkTxSignatureDemo.Server
{
    /// <summary>
    /// Hosted service that launches the TX Text Control Web Server Core process on application startup.
    /// This process handles the WebSocket communication between the Angular editor component and the
    /// server-side TX Text Control library.
    /// </summary>
    public class TXWebServerProcess : IHostedService
    {
        private readonly ILogger<TXWebServerProcess> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="TXWebServerProcess"/>.
        /// </summary>
        /// <param name="logger">The logger used to report startup and error events.</param>
        public TXWebServerProcess(ILogger<TXWebServerProcess> logger) => _logger = logger;

        /// <summary>
        /// Starts the TX Web Server Core process as a child process of the ASP.NET Core host.
        /// The process is expected to be located alongside the executing assembly.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the start operation.</param>
        /// <returns>A completed <see cref="Task"/> once the process has been launched (or the launch attempt has concluded).</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string dllPath = Path.Combine(path ?? "", "TXTextControl.Web.Server.Core.dll");

                if (string.IsNullOrEmpty(path) || !File.Exists(dllPath))
                    _logger.LogWarning("TX Web Server process could not be started.");
                else
                {
                    Process.Start(new ProcessStartInfo("dotnet", $"\"{dllPath}\" &") { UseShellExecute = true, WorkingDirectory = path });
                    _logger.LogInformation("TX Web Server process started.");
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Error starting TX Web Server."); }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when the host is performing a graceful shutdown. Logs the shutdown event.
        /// The TX Web Server Core process is managed by the OS and will be cleaned up separately.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the stop operation.</param>
        /// <returns>A completed <see cref="Task"/>.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping TX Web Server process...");
            return Task.CompletedTask;
        }
    }
}
