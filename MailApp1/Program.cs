using Microsoft.Extensions.Configuration;
using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using MailApp1;
using Serilog;

class Program
{
    static async Task Main()
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

        try
        {
            await ProcessEmailsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
    static async Task ProcessEmailsAsync()
    {
        var loggerService = new LoggerService();
        var nMail = new mailApp(loggerService);
        await nMail.ProcessEmails();
    }

}
