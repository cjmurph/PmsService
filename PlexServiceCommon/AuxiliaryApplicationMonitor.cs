using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Serilog;

namespace PlexServiceCommon
{
    /// <summary>
    /// Class that runs up and monitors the life of auxiliary applications
    /// </summary>
    public class AuxiliaryApplicationMonitor
    {
        private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(5);

        private readonly object _sync = new();

        public string Name => _aux.Name;

        public bool StartBeforePlex => _aux.StartBeforePlex;

        public int StartDelay => Math.Max(0, _aux.StartDelay);

        public bool Running { get; private set; }

        /// <summary>
        /// Auxiliary process
        /// </summary>
        private Process _auxProcess;

        /// <summary>
        /// Flag for actual stop rather than crash we should attempt to restart from
        /// </summary>
        private bool _stopping;

        /// <summary>
        /// Auxiliary Application to monitor
        /// </summary>
        private readonly AuxiliaryApplication _aux;

        public AuxiliaryApplicationMonitor(AuxiliaryApplication aux)
        {
            _aux = aux;
        }

        /// <summary>
        /// Start the application
        /// </summary>
        public void Start()
        {
            lock (_sync)
            {
                _stopping = false;
                if (string.IsNullOrEmpty(_aux.FilePath) || !File.Exists(_aux.FilePath))
                {
                    Log.Warning($"Unable to start {_aux.Name}, the file '{_aux.FilePath}' doesn't exist");
                    return;
                }

                ProcStart();
            }
        }

        /// <summary>
        /// Stop the monitor and kill the processes
        /// </summary>
        public void Stop()
        {
            lock (_sync)
            {
                _stopping = true;
                End();
            }
        }

        /// <summary>
        /// This event fires when the process we have a reference to exits
        /// </summary>
        private void AuxProcess_Exited(object sender, EventArgs e)
        {
            lock (_sync)
            {
                if (sender is Process exited)
                {
                    exited.Exited -= AuxProcess_Exited;
                    if (!ReferenceEquals(exited, _auxProcess))
                    {
                        exited.Dispose();
                        return;
                    }
                }

                _auxProcess?.Dispose();
                _auxProcess = null;
                Running = false;

                if (!_aux.KeepAlive || _stopping)
                {
                    Log.Information(_aux.Name + " has completed");
                    return;
                }

                Log.Information($"{_aux.Name} has stopped, re-starting in {RestartDelay.TotalSeconds} seconds");
            }

            Task.Delay(RestartDelay).ContinueWith(_ =>
            {
                lock (_sync)
                {
                    if (!_stopping) ProcStart();
                }
            });
        }

        /// <summary>
        /// Start a new process
        /// </summary>
        private void ProcStart()
        {
            if (_auxProcess != null) {
                return;
            }

            Log.Information("Attempting to start " + _aux.Name);
            var process = new Process {
                StartInfo = {
                    FileName = _aux.FilePath,
                    WorkingDirectory = string.IsNullOrEmpty(_aux.WorkingFolder) ? Path.GetDirectoryName(_aux.FilePath) ?? string.Empty : _aux.WorkingFolder,
                    UseShellExecute = false,
                    Arguments = _aux.Argument ?? string.Empty,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };
            process.Exited += AuxProcess_Exited;

            // both streams always need to be drained, otherwise a chatty app blocks once the pipe buffer is full
            process.OutputDataReceived += (_, e) => {
                if (_aux.LogOutput && !string.IsNullOrEmpty(e.Data)) Log.Debug($"{_aux.Name}: {e.Data}");
            };
            process.ErrorDataReceived += (_, e) => {
                if (_aux.LogOutput && !string.IsNullOrEmpty(e.Data)) Log.Warning($"{_aux.Name}: {e.Data}");
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                _auxProcess = process;
                Running = true;
                Log.Information(_aux.Name + " Started.");
            }
            catch (Exception ex)
            {
                process.Exited -= AuxProcess_Exited;
                process.Dispose();
                Log.Warning(_aux.Name + " failed to start. " + ex.Message);
            }
        }

        /// <summary>
        /// Kill the process
        /// </summary>
        private void End() {
            if (_auxProcess == null) {
                return;
            }

            Log.Information("Killing " + _aux.Name);
            var process = _auxProcess;
            _auxProcess = null;
            Running = false;
            try
            {
                process.Exited -= AuxProcess_Exited;
                if (!process.HasExited) process.Kill();
            } catch (Exception ex) {
                Log.Warning($"Exception stopping auxProc {_aux.Name}: " + ex.Message);
            } finally
            {
                process.Dispose();
            }
        }
    }
}
