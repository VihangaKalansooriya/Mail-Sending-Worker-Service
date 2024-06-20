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

//***************************************************************************
// File: Email Sending Application
// Author: Vihanga Kalansooriya
// Date: June 16, 2024
// Contact: techsupport.02@24x7retail.com
//***************************************************************************

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
        Globalconfig.TransactionTemplate = config.GetSection("EmailTemplates")["TransactionTemplate"];
        Globalconfig.PermissionTemplate = config.GetSection("EmailTemplates")["PermissionTemplate"];
        Globalconfig.reportgeneratorpath = config.GetSection("RepoGenPath")["reportgeneratorpath"];
        Globalconfig.PdfFullPath = config.GetSection("AppConfig")["PdfFullPath"];
        Globalconfig.SenderEmail = config.GetSection("EmailConfiguration")["SMTPclient"];
        Globalconfig.SenderPassword = config.GetSection("EmailConfiguration")["Port"];

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
