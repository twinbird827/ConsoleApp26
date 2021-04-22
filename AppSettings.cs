using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp26
{
    public class AppSettings
    {
        public static int Dpi => int.Parse(ConfigurationManager.AppSettings["dpi"]);

        public static int CopyPdf => int.Parse(ConfigurationManager.AppSettings["copy_pdf"]);

    }
}
