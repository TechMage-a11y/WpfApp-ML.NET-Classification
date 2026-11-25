using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace WpfApp_ML.NET_Classification.Services
{
    public sealed class TrainingArtifacts
    {
        public string ModelPath { get; set; }
        public string ProjectPath { get; set; }
    }

    public static class MlnetResultParser
    {
        private static readonly string[] MetricNames =
        {
            "MacroAccuracy", "MicroAccuracy", "Accuracy", "F1 Score", "F1Score",
            "AUC", "Log Loss", "LogLoss"
        };

        public static IList<KeyValuePair<string, string>> ParseMetrics(string output)
        {
            var metrics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var text = output ?? string.Empty;
            for (var position = 0; position < text.Length; position++)
            {
                foreach (var metricName in MetricNames)
                {
                    if (!IsMetricAt(text, position, metricName))
                    {
                        continue;
                    }

                    var valueStart = FindMetricValueStart(text, position + metricName.Length);
                    if (valueStart >= 0 && TryReadNumber(text, valueStart, out var value, out var valueEnd))
                    {
                        StoreBestMetric(metrics, metricName.Replace(" ", string.Empty), value);
                        position = valueEnd - 1;
                    }

                    break;
                }
            }

            foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var columns = line.Trim().Trim('|').Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (columns.Length < 3 || !int.TryParse(columns[0], out _) ||
                    !TryReadNumber(columns[2], 0, out var value, out var valueEnd) || valueEnd != columns[2].Length)
                {
                    continue;
                }

                StoreBestMetric(metrics, "MacroAccuracy", value);
            }

            return metrics.OrderBy(metric => metric.Key, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void StoreBestMetric(IDictionary<string, string> metrics, string name, string value)
        {
            if (!TryParseMetricValue(value, out var candidate))
            {
                return;
            }

            if (!metrics.TryGetValue(name, out var currentValue) ||
                !TryParseMetricValue(currentValue, out var current))
            {
                metrics[name] = value;
                return;
            }

            var isLoss = name.Equals("LogLoss", StringComparison.OrdinalIgnoreCase);
            if ((isLoss && candidate < current) || (!isLoss && candidate > current))
            {
                metrics[name] = value;
            }
        }

        private static bool IsMetricAt(string text, int position, string name)
        {
            if (position > 0 && char.IsLetterOrDigit(text[position - 1]))
            {
                return false;
            }

            if (position + name.Length > text.Length ||
                string.Compare(text, position, name, 0, name.Length, StringComparison.OrdinalIgnoreCase) != 0)
            {
                return false;
            }

            var end = position + name.Length;
            return end == text.Length || !char.IsLetterOrDigit(text[end]);
        }

        private static int FindMetricValueStart(string text, int position)
        {
            var separator = position;
            while (position < text.Length && char.IsWhiteSpace(text[position]))
            {
                position++;
            }

            if (position < text.Length && (text[position] == ':' || text[position] == '='))
            {
                position++;
            }
            else
            {
                if (position < text.Length && text[position] == '|')
                {
                    position++;
                }
                else if (position == separator)
                {
                    return -1;
                }
            }

            while (position < text.Length && char.IsWhiteSpace(text[position]))
            {
                position++;
            }

            return position;
        }

        private static bool TryReadNumber(string text, int start, out string value, out int end)
        {
            end = start;
            while (end < text.Length && (char.IsDigit(text[end]) || "+-.,eE".IndexOf(text[end]) >= 0))
            {
                end++;
            }

            value = end == start ? string.Empty : text.Substring(start, end - start);
            double parsed;
            return value.Length > 0 && TryParseMetricValue(value, out parsed);
        }

        private static bool TryParseMetricValue(string value, out double parsed)
        {
            return double.TryParse(value.Replace(',', '.'), NumberStyles.Float,
                CultureInfo.InvariantCulture, out parsed);
        }

        public static TrainingArtifacts FindArtifacts(string outputDirectory, string modelName,
            DateTime notBeforeUtc)
        {
            if (!Directory.Exists(outputDirectory))
            {
                throw new DirectoryNotFoundException("Папка результатов не найдена: " + outputDirectory);
            }

            var modelDirectory = Path.Combine(outputDirectory, modelName);
            var searchDirectory = Directory.Exists(modelDirectory) ? modelDirectory : outputDirectory;
            var modelPath = Directory.GetFiles(searchDirectory, "*.*", SearchOption.AllDirectories)
                .Where(path => (Path.GetExtension(path).Equals(".mlnet", StringComparison.OrdinalIgnoreCase) ||
                                Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase)) &&
                               File.GetLastWriteTimeUtc(path) >= notBeforeUtc)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            var projectPath = Directory.GetFiles(searchDirectory, "*.csproj", SearchOption.AllDirectories)
                .OrderByDescending(path => Path.GetFileNameWithoutExtension(path).Equals(
                    modelName, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            return new TrainingArtifacts { ModelPath = modelPath, ProjectPath = projectPath };
        }
    }
}
