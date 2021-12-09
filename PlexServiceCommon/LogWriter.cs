#nullable enable
using System;
using System.IO;
using Serilog;

namespace PlexServiceCommon
{
    /// <summary>
    /// Static class for writing to the log file
    /// </summary>
    public static class LogWriter {
        private static ILogger? _log;
        private static readonly string AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Plex Service\");
        public static readonly string LogFile = Path.Combine(AppDataPath, "plexService.log");

        public static void Init() {
            if (_log != null) return;
            if (!Directory.Exists(Path.GetDirectoryName(LogFile))) {
                var dir = Path.GetDirectoryName(LogFile);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            }
            const string outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}]{Caller} {Message}{NewLine}{Exception}";
            //var tr1 = new TextWriterTraceListener(Console.Out);
            //Trace.Listeners.Add(tr1);
            var lc = new LoggerConfiguration()
                .Enrich.WithCaller()
                .MinimumLevel.Debug()
                .Filter.ByExcluding(c => c.Properties["Caller"].ToString().Contains("SerilogLogger"))
                .Enrich.FromLogContext()
                .WriteTo.Async(a =>
                    a.File(LogFile, outputTemplate: outputTemplate));

            
            Log.Logger = lc.CreateLogger();
            _log = Log.Logger;
        }
    }
}
