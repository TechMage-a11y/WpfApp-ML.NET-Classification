using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace WpfApp_ML.NET_Classification.Services
{
    public sealed class TrainingArtifacts
    {
        public string ModelPath { get; set; }
        public string ProjectPath { get; set; }
    }

    public static class MlnetResultParser
    {
        private static readonly Regex MetricPattern = new Regex(
            @"(?<name>MacroAccuracy|MicroAccuracy|Accuracy|F1\s*Score|F1Score|AUC|Log\s*Loss|LogLoss)\s*(?:[:=]|\|?\s+)\s*(?<value>[+-]?[0-9]+(?:[.,][0-9]+)?(?:[eE][+-]?[0-9]+)?)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static IList<KeyValuePair<string, string>> ParseMetrics(string output)
        {
            var metrics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var text = output ?? string.Empty;
            foreach (Match match in MetricPattern.Matches(text))
            {
                var name = Regex.Replace(match.Groups["name"].Value, @"\s+", string.Empty);
                if (!metrics.ContainsKey(name))
                {
                    metrics[name] = match.Groups["value"].Value;
                }
            }

            var macroAccuracy = new Regex(
                @"^\s*\|\s*\d+\s+\S+\s+(?<value>[+-]?(?:\d+(?:[.,]\d*)?|[.,]\d+)(?:[eE][+-]?\d+)?)(?:\s+[^|]*)?\s*\|?\s*$",
                RegexOptions.Compiled);
            foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var match = macroAccuracy.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                var value = match.Groups["value"].Value;
                double parsedValue;
                double currentValue;
                if (!metrics.TryGetValue("MacroAccuracy", out var current) ||
                    (double.TryParse(value.Replace(',', '.'), NumberStyles.Float,
                         CultureInfo.InvariantCulture, out parsedValue) &&
                     double.TryParse(current.Replace(',', '.'), NumberStyles.Float,
                         CultureInfo.InvariantCulture, out currentValue) && parsedValue > currentValue))
                {
                    metrics["MacroAccuracy"] = value;
                }
            }

            return metrics.OrderBy(metric => metric.Key, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static TrainingArtifacts FindArtifacts(string outputDirectory, string modelName)
        {
            return FindArtifacts(outputDirectory, modelName, DateTime.MinValue);
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
                .Where(path => !IsBuildOutput(path) &&
                    (string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(Path.GetExtension(path), ".mlnet", StringComparison.OrdinalIgnoreCase)) &&
                    File.GetLastWriteTimeUtc(path) >= notBeforeUtc)
                .OrderByDescending(path => string.Equals(Path.GetFileNameWithoutExtension(path), modelName,
                    StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            var projectDirectory = modelPath == null ? searchDirectory : Path.GetDirectoryName(modelPath);
            var projectCandidates = Directory.GetFiles(projectDirectory, "*.csproj", SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path))
                .Select(path => new
                {
                    Path = path,
                    ProgramPath = FindProgramFile(Path.GetDirectoryName(path)),
                    ExactName = string.Equals(Path.GetFileNameWithoutExtension(path), modelName,
                        StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), modelName,
                            StringComparison.OrdinalIgnoreCase)
                })
                .Where(candidate => candidate.ProgramPath != null)
                .OrderByDescending(candidate => candidate.ExactName)
                .ThenByDescending(candidate => File.GetLastWriteTimeUtc(candidate.Path))
                .ToList();

            return new TrainingArtifacts
            {
                ModelPath = modelPath,
                ProjectPath = projectCandidates.FirstOrDefault()?.Path
            };
        }

        private static string FindProgramFile(string projectDirectory)
        {
            return Directory.GetFiles(projectDirectory, "Program.cs", SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path))
                .OrderBy(path => path.Length)
                .FirstOrDefault();
        }

        private static bool IsBuildOutput(string path)
        {
            return path.IndexOf(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
