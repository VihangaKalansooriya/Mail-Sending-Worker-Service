using MailApp1;
using Serilog;
using System;

public static class Logger
{
    private static readonly Serilog.ILogger logger;
    private static readonly Serilog.ILogger Log = new LoggerConfiguration()
        .WriteTo.File(Globalconfig.logfilepath,
        rollingInterval: RollingInterval.Day
        )
        .CreateLogger();

    public static void LogInformation(string message)
    {
        Log.Information(message);
    }

    public static void LogError(string message, Exception ex)
    {
        Log.Error(ex, message);
    }
}