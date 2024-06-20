using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using MailWorker;
using MailApp1; 

namespace MailWorker
{
    public class Worker : BackgroundService
    {
        private readonly IConfiguration _configuration;
        public Worker(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessEmails();
                    var intervalMinutes = _configuration.GetValue<int>("WorkerServiceOptions:IntervalMinutes");
                    var delayInterval = TimeSpan.FromMinutes(intervalMinutes);
                    await Task.Delay(delayInterval, stoppingToken);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in Clear Old Files :", ex); 
                }
            }
        }

        private async Task ProcessEmails()
        {
            var nMail = new mailApp();
            await nMail.ProcessEmails();
        }
    }
}
