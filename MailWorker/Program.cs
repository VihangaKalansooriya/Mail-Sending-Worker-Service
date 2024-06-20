using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MailWorker;
using MailApp1;
using Microsoft.Extensions.Configuration;
using Serilog;

//***************************************************************************
// File: Email Sending Application Worker Service
// Author: Vihanga Kalansooriya
// Date: March 26, 2024
// Contact: techsupport.02@24x7retail.com
//***************************************************************************

namespace MailWorker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var basepath = Directory.GetCurrentDirectory();

            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json").Build();

            Globalconfig.ConnectionString = config.GetConnectionString("DefaultConnection");
            Globalconfig.SenderEmail = config.GetSection("EmailConfiguration")["SenderEmail"];
            Globalconfig.SenderPassword = config.GetSection("EmailConfiguration")["SenderPassword"];
            Globalconfig.logfilepath = config.GetSection("LogfilePath")["logfilepath"];
            Globalconfig.databasename = config.GetSection("DatabaseConfiguration")["databasename"];
            Globalconfig.TransactionTemplate = config.GetSection("EmailTemplates")["TransactionTemplate"];
            Globalconfig.PermissionTemplate = config.GetSection("EmailTemplates")["PermissionTemplate"];
            Globalconfig.reportgeneratorpath = config.GetSection("RepoGenPath")["reportgeneratorpath"];
            Globalconfig.PdfFullPath = config.GetSection("AppConfig")["PdfFullPath"];
            Globalconfig.SMTPclient = config.GetSection("EmailConfiguration")["SMTPclient"];
            Globalconfig.Port = int.Parse(config.GetSection("EmailConfiguration")["Port"]);

            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(Globalconfig.logfilepath, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            int daysThreshold = 7;
            ClearOldFiles(daysThreshold);
            CreateHostBuilder(args).Build().Run();

        }

        static void ClearOldFiles(int daysThreshold)
        {
            try
            {
                DirectoryInfo directory = new DirectoryInfo(Globalconfig.logfilepath);

                if (!directory.Exists)
                {
                    Console.WriteLine("Directory not found.");
                    return;
                }

                DateTime thresholdDate = DateTime.Now.AddDays(-daysThreshold);

                foreach (FileInfo file in directory.GetFiles())
                {
                    if (file.LastWriteTime < thresholdDate)
                    {
                        file.Delete();
                        Logger.LogInformation("Deleted old file: {file.FullName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in Clear Old Files include Program.cs:", ex);
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    // Register Worker as a hosted service
                    services.AddHostedService<Worker>();
                });
    }
}
