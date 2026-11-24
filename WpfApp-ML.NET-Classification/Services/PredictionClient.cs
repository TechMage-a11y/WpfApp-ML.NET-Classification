using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp_ML.NET_Classification.Services
{
    public sealed class PredictionProcessResult
    {
        public int ExitCode { get; set; }
        public string StandardOutput { get; set; }
        public string StandardError { get; set; }
    }

    public static class PredictionClient
    {
        public static async Task<PredictionProcessResult> PredictAsync(string projectPath, string text)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --no-launch-profile --project " + QuoteArgument(projectPath) +
                    " -- " + QuoteArgument(text),
                WorkingDirectory = System.IO.Path.GetDirectoryName(projectPath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException("Не удалось запустить сгенерированное приложение.");
                }

                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit());
                await Task.WhenAll(stdoutTask, stderrTask);

                return new PredictionProcessResult
                {
                    ExitCode = process.ExitCode,
                    StandardOutput = stdoutTask.Result,
                    StandardError = stderrTask.Result
                };
            }
        }

        private static string QuoteArgument(string argument)
        {
            var result = new StringBuilder("\"");
            var backslashCount = 0;
            foreach (var character in argument)
            {
                if (character == '\\')
                {
                    backslashCount++;
                }
                else if (character == '"')
                {
                    result.Append('\\', backslashCount * 2 + 1).Append('"');
                    backslashCount = 0;
                }
                else
                {
                    result.Append('\\', backslashCount).Append(character);
                    backslashCount = 0;
                }
            }

            result.Append('\\', backslashCount * 2).Append('"');
            return result.ToString();
        }
    }
}
