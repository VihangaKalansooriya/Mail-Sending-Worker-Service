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
using ClosedXML.Excel;
using System.IO;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Office2010.Excel;

public class mailApp
{
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
            //Console.WriteLine("Error processing emails: " + ex.ToString());
            Logger.LogError("Error processing emails:", ex);
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
            MailMessage mail = null;
            SmtpClient smtpClient = new SmtpClient(Globalconfig.SMTPclient)
            {
                Port = Globalconfig.Port,
                EnableSsl = true,
                Credentials = new NetworkCredential(Globalconfig.SenderEmail, Globalconfig.SenderPassword)
            }; 

            Dictionary<string, object> argsval = new Dictionary<string, object>();

            using (SqlConnection dbConnection = new SqlConnection(Globalconfig.ConnectionString))
            {
                dbConnection.Open();
                string query = "SELECT md.TB_LOCATION, md.TB_DATE, md.TB_TYPE, md.TB_RUNNO, md.TB_DESC, md.TB_TRTYPE, md.TB_URL, md.TB_SUBJECT, md.TB_BODY, ml.LOC_DESC, md.TB_TRAMT, sp.SM_NAME, cm.CM_FULLNAME, md.TB_TRSUP, md.TB_TRCUS FROM M_TBLMAILDETAILS md INNER JOIN M_TBLLOCATIONS ml ON md.TB_LOCATION = ml.LOC_CODE LEFT OUTER JOIN M_TBLSUPPLIER sp ON md.TB_TRSUP = sp.SM_CODE LEFT OUTER JOIN M_TBLCUSTOMER cm ON md.TB_TRCUS = cm.CM_CODE WHERE TB_ID = @ID";

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

                            string subject = reader["TB_SUBJECT"] as string;
                            string body = reader["TB_BODY"] as string;
                            string trType = reader["TB_TRTYPE"].ToString();
                            string url = reader["TB_URL"].ToString();
                            string tbType = reader["TB_TYPE"] as string;
                            string tbDesc = reader["TB_DESC"] as string;
                            string tbRunno = reader["TB_RUNNO"].ToString();
                            string locCode = reader["TB_LOCATION"] as string;
                            string locName = reader["LOC_DESC"].ToString();
                            string supName = reader["SM_NAME"].ToString();
                            string cusName = reader["CM_FULLNAME"].ToString();
                            string amount = reader["TB_TRAMT"].ToString();
                            DateTime dateValue = reader["TB_DATE"] as DateTime? ?? DateTime.MinValue; 
                            string dateOnly = dateValue.ToString("yyyy-MM-dd");

                            if (trType == "T")
                            {
                                mail = new MailMessage(Globalconfig.SenderEmail, recipientEmail)
                                {
                                    Subject = $"Processed Transaction: {argsval["menuCode"]} - [{tbRunno}] Location: {locName}",
                                    Body = Globalconfig.TransactionTemplate.Replace("{menuCode}", argsval["menuCode"].ToString())
                                              .Replace("{id}", id.ToString())
                                              .Replace("{trType}", trType)
                                };

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
                                    Logger.LogInformation($"PDF Attached:{resultString}.pdf");
                                    Console.WriteLine("Email sent with PDF attachment for ID " + id);
                                    UpdateStatus(id);
                                    Log.CloseAndFlush();
                                }
                            }

                            else if (trType == "P")
                            {
                                mail = new MailMessage(Globalconfig.SenderEmail, recipientEmail)
                                {
                                    Subject = $"Approval Required: {tbDesc} - [{tbRunno}] @Location: {locName}",
                                    Body = Globalconfig.PermissionTemplate.Replace("{menuCode}", argsval["menuCode"].ToString())
                                             .Replace("{id}", id.ToString())
                                             .Replace("{tbType}", tbType)
                                             .Replace("{tbDesc}", tbDesc)
                                             .Replace("{locName}", locName)
                                             .Replace("{tbRunno}", tbRunno)
                                             .Replace("{locCode}", locCode)
                                             .Replace("{url}", url)
                                             .Replace("{supName}", supName)
                                             .Replace("{cusName}", cusName)
                                             .Replace("{amount}", amount)
                                             .Replace("{dateOnly}", dateOnly)
                                };

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
                                    Logger.LogInformation($"PDF Attached:{resultString}.pdf");
                                    Console.WriteLine("Email sent with PDF attachment for ID " + id);
                                    UpdateStatus(id);
                                    Log.CloseAndFlush();
                                }
                            }
                            else if (trType == "C")
                            {
                                mail = new MailMessage(Globalconfig.SenderEmail, recipientEmail)
                                {
                                    Subject = $"Approval Required: {argsval["menuCode"]} Location: {locName}",
                                    Body = Globalconfig.PermissionTemplate.Replace("{menuCode}", argsval["menuCode"].ToString())
                                             .Replace("{id}", id.ToString())
                                             .Replace("{trType}", trType)
                                             .Replace("{url}", url)
                                };

                             
                                await smtpClient.SendMailAsync(mail);
                                Logger.LogInformation($"Email sent without PDF for ID {id}");
                                Console.WriteLine("Email sent without PDF attachment for ID " + id);
                                UpdateStatus(id);
                                Log.CloseAndFlush();
                                return;
                            }
                            else if(trType == "E")
                            {
                                mail = new MailMessage(Globalconfig.SenderEmail, recipientEmail)
                                {
                                    Subject = $"Processed Transaction: {argsval["menuCode"]} - [{tbRunno}] Location: {locName}",
                                    Body = Globalconfig.ExcelTemplate.Replace("{menuCode}", argsval["menuCode"].ToString())
                                             .Replace("{id}", id.ToString())
                                             .Replace("{trType}", trType)
                                             .Replace("{url}", url)
                                };
                                
                                string excelFilePath = CreateExcelReport(id, tbType, tbRunno, locCode);
                                Attachment excelAttachment = new Attachment(excelFilePath, MediaTypeNames.Application.Octet);
                                mail.Attachments.Add(excelAttachment);
                                smtpClient = new SmtpClient(Globalconfig.SMTPclient)
                                {
                                    Port = Globalconfig.Port,
                                    EnableSsl = true,
                                    Credentials = new NetworkCredential(Globalconfig.SenderEmail, Globalconfig.SenderPassword)
                                };

                                await smtpClient.SendMailAsync(mail);
                                Console.WriteLine($"Email with Excel attachment sent for ID"+ id);
                                UpdateStatus(id);
                            }

                        }
                    }
                }
            }


        }
        catch (Exception ex)
        {
            //Console.WriteLine("Error attaching or sending email: " + ex.ToString());
            Logger.LogError("Error running ReportGenerator.exe", ex);
        }

    }

    //private string GenerateAndAttachPdf(Dictionary<string, object> argsval, String tbRunno)
    //{
    //    var startInfo = new ProcessStartInfo
    //    {
    //        FileName = Globalconfig.reportgeneratorpath,
    //        Arguments = JsonConvert.SerializeObject(argsval),
    //        RedirectStandardOutput = true,
    //        RedirectStandardError = true,
    //        UseShellExecute = false,
    //        CreateNoWindow = true
    //    };

    //    using (Process process = new Process())
    //    {
    //        process.StartInfo = startInfo;
    //        process.Start();
    //        process.WaitForExit();

    //        var output = process.StandardOutput.ReadToEnd();
    //        string resultString = output.Trim();
    //        string pdfFullPath = Path.Combine(Globalconfig.PdfFullPath, $"{resultString}.pdf");

    //        if (!File.Exists(pdfFullPath))
    //        {
    //            throw new FileNotFoundException("PDF not generated.", pdfFullPath);
    //        }

    //        Logger.LogInformation($"PDF Generated: {resultString}.pdf");
    //        return pdfFullPath;
    //    }
    //}

    private string CreateExcelReport(int id,String tbType,String tbRunno,String locCode)
    {
        string filePath = Path.Combine(Globalconfig.AttachedEXFilePath, $"TransactionReport_{id}.xlsx");

        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Transaction Report");

            // Add Header Row
            worksheet.Cell(1, 1).Value = "RUN NO";
            worksheet.Cell(1, 2).Value = "LOCATION";
            worksheet.Cell(1, 3).Value = "PRODUCT CODE";
            worksheet.Cell(1, 4).Value = "PRODUCT DESCRIPTION";
            worksheet.Cell(1, 5).Value = "CASE QTY";
            worksheet.Cell(1, 6).Value = "COST PRICE";
            worksheet.Cell(1, 7).Value = "AMOUNT";

            // Fetch transaction details from the database
            using (SqlConnection dbConnection = new SqlConnection(Globalconfig.ConnectionString))
            {
                dbConnection.Open();
                string query = "DECLARE @DetailTableName NVARCHAR(100);"+
                "SELECT @DetailTableName = TX_DETAILTABLE FROM U_TBLTXNSETUP WHERE TX_TYPE = @TB_TYPE;"+
                "DECLARE @SQLQuery NVARCHAR(MAX);"+
                "SET @SQLQuery = N'SELECT * FROM ' + QUOTENAME(@DetailTableName) + ' WHERE DET_RUNNO = @TB_RUNNO';"+
                "EXEC sp_executesql @SQLQuery, N'@TB_RUNNO NVARCHAR(13), @Location NVARCHAR(5)', @TB_RUNNO, @Location;";

                using (SqlCommand command = new SqlCommand(query, dbConnection))
                {
                    command.Parameters.AddWithValue("@ID", id);
                    command.Parameters.AddWithValue("@TB_TYPE", tbType);
                    command.Parameters.AddWithValue("@TB_RUNNO", tbRunno.ToString());
                    command.Parameters.AddWithValue("@Location", locCode.ToString());
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        int row = 2; 
                        while (reader.Read())
                        {
                            worksheet.Cell(row, 1).Value = reader["DET_RUNNO"].ToString();
                            worksheet.Cell(row, 2).Value = reader["DET_LOCFROM"].ToString();
                            worksheet.Cell(row, 3).Value = reader["DET_PROCODE"].ToString();
                            worksheet.Cell(row, 4).Value = reader["DET_PRODESC"].ToString();
                            worksheet.Cell(row, 5).Value = reader["DET_CASEQTY"].ToString();
                            worksheet.Cell(row, 6).Value = reader["DET_CPRICE"].ToString();
                            worksheet.Cell(row, 7).Value = reader["DET_AMOUNT"].ToString();
                            row++; 
                        }
                    }
                }
            }
            worksheet.Columns().AdjustToContents();
            workbook.SaveAs(filePath);
        }

        return filePath;
    }
    static void UpdateStatus(int id)
    {
        string updateQuery = "UPDATE M_TBLMAILDETAILS SET TB_STATUS = 1 WHERE TB_ID = @ID";
        //Console.WriteLine("Status Update: " + id);
        Logger.LogInformation("Email sent with PDF attachment for ID " + id);
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
