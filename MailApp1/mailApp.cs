using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Mail;
using System.Net.Mime;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MailApp1;
using Serilog;
using System.Data;
using System.Reflection.PortableExecutable;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Transactions;

public class mailApp
{
    private readonly LoggerService loggerService;

    public mailApp(LoggerService loggerService)
    {
        this.loggerService = loggerService;
    }

    public async Task ProcessEmails()
    {
        DataTable dt = new DataTable();
        using (SqlConnection sourceConnection = new SqlConnection(Globalconfig.ConnectionString))
        {
            sourceConnection.Open();
            string query = "SELECT TB_ID, TB_RECEIVERMAIL FROM TB_MAILDETAILS WHERE TB_STATUS = 0";

            using (SqlCommand command = new SqlCommand(query, sourceConnection))
            {
                using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                {
                    adapter.Fill(dt); 
                }
            }
        }

        foreach (DataRow row in dt.Rows)
        {
            int id = (int)row["TB_ID"];
            string recipientEmail = row["TB_RECEIVERMAIL"].ToString();

            await SendEmailAsync(recipientEmail, id);
        }
    }

    public async Task SendEmailAsync(string recipientEmail, int id)
    {
        try
        {
            loggerService.LogInformation("Processing emails...");
            SmtpClient smtpClient = new SmtpClient("smtp.office365.com")
            {
                Port = 587,
                EnableSsl = true,
                Credentials = new NetworkCredential(Globalconfig.SenderEmail, Globalconfig.SenderPassword)
            }; 

            Dictionary<string, object> argsval = new Dictionary<string, object>();

            using (SqlConnection dbConnection = new SqlConnection(Globalconfig.ConnectionString))
            {
                dbConnection.Open();
                string query = "SELECT TB_TYPE, TB_RUNNO FROM TB_MAILDETAILS WHERE TB_ID = @ID";

                using (SqlCommand command = new SqlCommand(query, dbConnection))
                {
                    command.Parameters.AddWithValue("@ID", id);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            argsval.Add("reportName", "transaction");
                            argsval.Add("userType", "ADMIN");
                            argsval.Add("code", reader["TB_RUNNO"].ToString());
                            argsval.Add("menuCode", reader["TB_TYPE"].ToString());
                            argsval.Add("db", Globalconfig.databasename);
                        }
                    }
                }
            }

            MailMessage mail = new MailMessage(Globalconfig.SenderEmail, recipientEmail)
            {
                Subject = $"{argsval["menuCode"]} Report",
                Body = $"Dear Sir/Madam,\nThis email contains a {argsval["menuCode"]} PDF attachment for ID {id}\nThank You."
            };

            var startInfo = new ProcessStartInfo
            {
                FileName = $"C:\\Users\\User\\source\\repos\\MailApp1\\ReportGen\\ReportGenerator.exe",
                Arguments = JsonConvert.SerializeObject(JsonConvert.SerializeObject(argsval)),
                RedirectStandardOutput = true
            };

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                await process.WaitForExitAsync();
                var output = await process.StandardOutput.ReadToEndAsync();
                string resultString = output.Replace("\r", "").Replace("\n", "");
                string pdfFullPath = $"C:\\Users\\User\\source\\repos\\MailApp1\\GeneratedFiles\\{resultString}.pdf";
                Attachment attachment = new Attachment(pdfFullPath, MediaTypeNames.Application.Pdf);
                mail.Attachments.Add(attachment);
                await smtpClient.SendMailAsync(mail);
                Console.WriteLine("Email sent with PDF attachment for ID " + id);
                UpdateStatus(id);
                Log.Information("Program ended at {EndTime}", DateTime.Now);
                Log.CloseAndFlush();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error attaching or sending email: " + ex.ToString());
            loggerService.LogError("An error occurred: " + ex.Message);
        }

    }

    static void UpdateStatus(int id)
    {
        string updateQuery = "UPDATE TB_MAILDETAILS SET TB_STATUS = 1 WHERE TB_ID = @ID";
        Console.WriteLine("Status Update: " + id);

        using (SqlConnection con = new SqlConnection(Globalconfig.ConnectionString))
        {
            using (SqlCommand updateCommand = new SqlCommand(updateQuery, con))
            {
                con.Open();
                updateCommand.Parameters.AddWithValue("@ID", id);
                updateCommand.ExecuteNonQuery();
                con.Close();
            }
        }
    }  

}
