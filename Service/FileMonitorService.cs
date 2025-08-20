using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace eST1C_ProgramImporter.Services
{
    public class FileMonitorService : BackgroundService
    {
        private readonly string watchDir = @"\\mypenm0opsapp01\SmartTorque$\Log Directory";
        private readonly string logJsonPath = @"D:\Console Program\est1c Program\eST1C_ProgramImporter\Data\LogCreated.json";
        private FileSystemWatcher watcher;

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logJsonPath)!);

            watcher = new FileSystemWatcher
            {
                Path = watchDir,
                IncludeSubdirectories = true,
                Filter = "*.*", // All files
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime
            };

            watcher.Created += OnFileCreated;

            // Keep the background service alive
            return Task.CompletedTask;
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                var logEntry = new
                {
                    FileName = Path.GetFileName(e.FullPath),
                    Directory = Path.GetDirectoryName(e.FullPath),
                    FullPath = e.FullPath,
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                string jsonString = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(logJsonPath, jsonString); // Overwrites each time
            }
            catch (Exception ex)
            {
                // Optional: add error logging here
            }
        }
    }
}
