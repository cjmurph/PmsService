using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PlexServiceTray
{
    /// <summary>
    /// Static class for writing to the log file
    /// </summary>
    internal static class LogWriter
    {
        private static readonly string AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Plex Service\");

        private static readonly string LogFile = Path.Combine(AppDataPath, "plexServiceLog.txt");

        private static readonly object SyncObject = new();

        internal static void WriteLine(string detail)
        {
            lock (SyncObject)
            {
                if (!Directory.Exists(Path.GetDirectoryName(LogFile))) {
                    var dir = Path.GetDirectoryName(LogFile);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                }

                //reduce its size if its getting big
                if (GetLineCount() > 200)
                {
                    //halve the log file
                    RemoveFirstLines(100);
                }

                // Create a writer and open the file:
                using var log = new StreamWriter(LogFile, true);
                log.WriteLine(DateTime.Now.ToString(CultureInfo.InvariantCulture) + ": " + detail);
            }
        }

        private static void RemoveFirstLines(int lineCount = 1)
        {
            if (File.Exists(LogFile))
            {
                var lines = File.ReadAllLines(LogFile);
                using var log = new StreamWriter(LogFile);
                for (var count = lineCount; count < lines.Length; count++)
                {
                    log.WriteLine(lines[count]);
                }
            }
        }

        internal static void DeleteLog()
        {
            if(File.Exists(LogFile))
            {
                File.Delete(LogFile);
            }
        }

        private static int GetLineCount()
        {
            var count = -1;
            if (File.Exists(LogFile))
            {
                count = File.ReadLines(LogFile).Count();
            }
            return count;
        }

        internal static string Read()
        {
            var log = string.Empty;
            if (!File.Exists(LogFile)) {
                return log;
            }

            try
            {
                lock (SyncObject)
                {
                    log = File.ReadAllText(LogFile);
                }
            } catch {
                // ignored
            }
            return log;
        }
    }
}
