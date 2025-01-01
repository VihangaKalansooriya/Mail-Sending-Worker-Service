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
            Globalconfig.ExcelTemplate = config.GetSection("EmailTemplates")["ExcelTemplate"];
            Globalconfig.reportgeneratorpath = config.GetSection("RepoGenPath")["reportgeneratorpath"];
            Globalconfig.PdfFullPath = config.GetSection("AppConfig")["PdfFullPath"];
            Globalconfig.SMTPclient = config.GetSection("EmailConfiguration")["SMTPclient"];
            Globalconfig.Port = int.Parse(config.GetSection("EmailConfiguration")["Port"]);
            Globalconfig.AttachedFilePath = config.GetSection("PDFfilePath")["AttachedFilePath"];
            Globalconfig.AttachedEXFilePath = config.GetSection("EXCELfilePath")["AttachedEXFilePath"];

            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(Globalconfig.logfilepath, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            int daysThreshold = 7;
            ClearOldFiles(daysThreshold);
            ClearOldExcelFiles(daysThreshold);
            CreateHostBuilder(args).Build().Run();

        }

        static void ClearOldFiles(int daysThreshold)
        {
            try
            {
                DirectoryInfo directory = new DirectoryInfo(Globalconfig.logfilepath);

                if (!directory.Exists)
                {
                    //Console.WriteLine("Directory not found.");
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
        static void ClearOldExcelFiles(int daysThreshold)
        {
            try
            {
                // Define the path where Excel files are stored
                string excelDirectoryPath = Globalconfig.AttachedEXFilePath;
                DirectoryInfo directory = new DirectoryInfo(excelDirectoryPath);

                if (!directory.Exists)
                {
                    Console.WriteLine("Excel directory not found.");
                    return;
                }

                DateTime thresholdDate = DateTime.Now.AddDays(-daysThreshold);

                foreach (FileInfo file in directory.GetFiles("*.xlsx")) // Looks specifically for Excel files
                {
                    if (file.LastWriteTime < thresholdDate)
                    {
                        file.Delete();
                        Logger.LogInformation($"Deleted old Excel file: {file.FullName}");
                        //Console.WriteLine($"Deleted old Excel file: {file.FullName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error clearing old Excel files:", ex);
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
