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
                if (getLineCount(fileName) > 200)
                {
                    //halve the log file
                    removeFirstLines(fileName, 100);
                }
                log = File.AppendText(fileName);
            }
            log.WriteLine(DateTime.Now.ToString() + ": " + detail);

            // Close the stream:
            log.Close();
            log.Dispose();
        }

        private static void removeFirstLines(string fileName, int lineCount = 1)
        {
            string[] lines = File.ReadAllLines(fileName);
            using (StreamWriter log = new StreamWriter(fileName))
            {
                for (int count = lineCount; count < lines.Length; count++)
                {
                    log.WriteLine(lines[count]);
                }
            }
        }

        internal static void deleteLog(string fileName)
        {
            if(File.Exists(fileName))
            {
                File.Delete(fileName);
            }
        }

        internal static int getLineCount(string fileName)
        {
            int count = -1;
            if (File.Exists(fileName))
            {
                count = File.ReadLines(fileName).Count();
            }
            return count;
        }
    }
}
