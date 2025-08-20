using System;
using eST1C_ProgramImporter.Services;

namespace eST1C_ProgramImporter
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("🚀 eST1C Program Importer Started");
            Console.WriteLine("------------------------------------");

            // var importer = new ProgramDbImporter();
            // importer.ImportAll();
            ProgramDbImporterFromList.ImportFromList();


            Console.WriteLine("------------------------------------");
            Console.WriteLine("✅ Import complete. Press any key to exit.");
            Console.ReadKey();
        }
    }
}



// using Microsoft.Extensions.DependencyInjection;
// using Microsoft.Extensions.Hosting;
// using eST1C_ProgramImporter.Services; 

// namespace eST1C_ProgramImporter
// {
//     public class Program
//     {
//         public static void Main(string[] args)
//         {
//             CreateHostBuilder(args).Build().Run();
//         }

//         public static IHostBuilder CreateHostBuilder(string[] args) =>
//             Host.CreateDefaultBuilder(args)
//                 .UseWindowsService() // enables running as Windows Service
//                 .ConfigureServices((hostContext, services) =>
//                 {
//                     // services.AddHostedService<HelloWriterService>();
//                     services.AddHostedService<FileMonitorService>();

//                 });
//     }
// }




