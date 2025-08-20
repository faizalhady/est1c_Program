using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace eST1C_ProgramImporter.Services
{
    public class HelloWriterService : BackgroundService
    {
        private readonly string logPath = @"D:\Console Program\est1c Program\eST1C_ProgramImporter\hello_log.txt";

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            while (!stoppingToken.IsCancellationRequested)
            {
                string line = $"[{DateTime.Now:HH:mm:ss}] Helloboi{Environment.NewLine}";
                await File.AppendAllTextAsync(logPath, line, stoppingToken);
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}
