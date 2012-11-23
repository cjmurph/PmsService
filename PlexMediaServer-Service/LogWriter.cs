using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace PlexMediaServer_Service
{
    static class LogWriter
    {
        internal static void WriteLine(string detail, string fileName)
        {
            // Create a writer and open the file:
            StreamWriter log;

            if (!File.Exists(fileName))
            {
                log = new StreamWriter(fileName);
            }
            else
            {
                log = File.AppendText(fileName);
            }
            log.WriteLine();
            log.WriteLine(detail);

            // Close the stream:
            log.Close();
        }

        internal static void deleteLog(string fileName)
        {
            if(File.Exists(fileName))
            {
                File.Delete(fileName);
            }
        }
    }
}
