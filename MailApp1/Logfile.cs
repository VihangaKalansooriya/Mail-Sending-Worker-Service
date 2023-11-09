using MailApp1;
using Serilog;

public class LoggerService
{
    private readonly ILogger logger;

    public LoggerService()
    {
        logger = new LoggerConfiguration()
            .WriteTo.File(Globalconfig.logfilepath) 
            .CreateLogger();
    }

    public void LogInformation(string message)
    {
        logger.Information(message);
        Log.CloseAndFlush();
    }

    public void LogWarning(string message)
    {
        logger.Warning(message);
        Log.CloseAndFlush();
    }

    public void LogError(string message)
    {
        logger.Error(message);
        Log.CloseAndFlush();
    }
}
