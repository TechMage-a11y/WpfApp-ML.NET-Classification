using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace WpfApp_ML.NET_Classification.Services
{
    public static class MlnetCommandBuilder
    {
        public static ProcessStartInfo CreateStartInfo(string datasetPath, int labelColumn,
            bool hasHeader, string modelName, int trainTime)
        {
            var arguments = "classification --dataset " + QuoteArgument(datasetPath) +
                " --label-col " + labelColumn +
                (hasHeader ? " --has-header" : string.Empty) +
                " --name " + QuoteArgument(modelName) +
                " --train-time " + trainTime;

            return new ProcessStartInfo
            {
                FileName = "mlnet",
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(datasetPath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
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
