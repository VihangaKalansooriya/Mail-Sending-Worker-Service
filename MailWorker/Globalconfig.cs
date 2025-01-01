using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MailApp1
{
    public static class Globalconfig
    {
        public static string ConnectionString { get; set; }
        public static string SenderEmail { get; set; }
        public static string SenderPassword { get; set; }
        public static string logfilepath { get; set; }
        public static string databasename { get; set; }
        public static string TransactionTemplate { get; set; }
        public static string PermissionTemplate { get; set;}
        public static string reportgeneratorpath { get; set; }
        public static string PdfFullPath { get; set; }
        public static string SMTPclient { get; set; }
        public static int Port { get; set; }
        public static string AttachedFilePath { get; set;}
        public static string AttachedEXFilePath { get; set; }
        public static string ExcelTemplate { get; set; }

    }
}
