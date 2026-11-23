using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace WpfApp_ML.NET_Classification.Services
{
    public sealed class ProcessOutputEventArgs : EventArgs
    {
        public ProcessOutputEventArgs(string line, bool isError)
        {
            Line = line;
            IsError = isError;
        }

        public string Line { get; private set; }
        public bool IsError { get; private set; }
    }

    public sealed class MlnetProcessRunner
    {
        private Process _process;

        public event EventHandler<ProcessOutputEventArgs> OutputReceived;

        public async Task<int> RunAsync(ProcessStartInfo startInfo)
        {
            using (var process = new Process { StartInfo = startInfo })
            {
                process.OutputDataReceived += (sender, args) => RaiseOutputReceived(args.Data, false);
                process.ErrorDataReceived += (sender, args) => RaiseOutputReceived(args.Data, true);

                _process = process;
                try
                {
                    if (!process.Start())
                    {
                        throw new InvalidOperationException("Не удалось запустить процесс mlnet.");
                    }

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    await Task.Run(() => process.WaitForExit());
                    process.WaitForExit();

                    return process.ExitCode;
                }
                finally
                {
                    _process = null;
                }
            }
        }

        public void Stop()
        {
            var process = _process;
            if (process != null && !process.HasExited)
            {
                process.Kill();
            }
        }

        private void RaiseOutputReceived(string line, bool isError)
        {
            if (line != null)
            {
                OutputReceived?.Invoke(this, new ProcessOutputEventArgs(line, isError));
            }
        }
    }
}
