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
        try
        {
            List<EmailData> emailDataList = GetEmailDataFromDatabase();

            foreach (var emailData in emailDataList)
            {
                await SendEmailAsync(emailData.RecipientEmail, emailData.Id);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error processing emails: " + ex.ToString());
            loggerService.LogError("An error occurred while processing emails: " + ex.Message);
        }
    }
    private List<EmailData> GetEmailDataFromDatabase()
    {
        List<EmailData> emailDataList = new List<EmailData>();

        using (SqlConnection sourceConnection = new SqlConnection(Globalconfig.ConnectionString))
        {
            sourceConnection.Open();
            string query = "SELECT TB_ID, TB_RECEIVERMAIL FROM M_TBLMAILDETAILS WHERE TB_STATUS = 0";

            using (SqlCommand command = new SqlCommand(query, sourceConnection))
            {
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int id = (int)reader["TB_ID"];
                        string recipientEmail = reader["TB_RECEIVERMAIL"].ToString();

                        emailDataList.Add(new EmailData { Id = id, RecipientEmail = recipientEmail });
                    }
                }
            }
        }

        return emailDataList;
    }
    public async Task SendEmailAsync(string recipientEmail, int id)
    {
        try
        {
            loggerService.LogInformation("Processing emails...");
            MailMessage mail = null;
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
                string query = "SELECT TB_TYPE, TB_RUNNO, TB_TRTYPE, TB_URL FROM M_TBLMAILDETAILS WHERE TB_ID = @ID";

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

                            string trType = reader["TB_TRTYPE"].ToString();
                            string url = reader["TB_URL"].ToString();

                            if (trType == "T")
                            {
                                mail = new MailMessage(Globalconfig.SenderEmail, recipientEmail)
                                {
                                    Subject = $"{argsval["menuCode"]} Report Attached.",
                                    Body = Globalconfig.TransactionTemplate.Replace("{menuCode}", argsval["menuCode"].ToString())
                                              .Replace("{id}", id.ToString())
                                              .Replace("{trType}", trType)
                                };
                            }
                            else if (trType == "P")
                            {
                                mail = new MailMessage(Globalconfig.SenderEmail, recipientEmail)
                                {
                                    Subject = $" Request for Permission {argsval["menuCode"]} Report",
                                    Body = Globalconfig.PermissionTemplate.Replace("{menuCode}", argsval["menuCode"].ToString())
                                             .Replace("{id}", id.ToString())
                                             .Replace("{trType}", trType)
                                             .Replace("{url}", url)
                                };
                            }
                        }
                    }
                }
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = Globalconfig.reportgeneratorpath,
                Arguments = JsonConvert.SerializeObject(JsonConvert.SerializeObject(argsval)),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                await process.WaitForExitAsync();
                var output = await process.StandardOutput.ReadToEndAsync();
                string resultString = output.Replace("\r", "").Replace("\n", "");
                string pdfFullPath = Path.Combine(Globalconfig.PdfFullPath, $"{resultString}.pdf");
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
        string updateQuery = "UPDATE M_TBLMAILDETAILS SET TB_STATUS = 1 WHERE TB_ID = @ID";
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
public class EmailData
{
    public int Id { get; set; }
    public string RecipientEmail { get; set; }
}
