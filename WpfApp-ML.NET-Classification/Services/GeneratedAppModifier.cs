using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace WpfApp_ML.NET_Classification.Services
{
    public sealed class GeneratedAppModificationResult
    {
        public bool Success { get; set; }
        public string ProgramPath { get; set; }
        public string Message { get; set; }
    }

    public static class GeneratedAppModifier
    {
        private static readonly Regex LoadColumnPattern = new Regex(
            @"\[LoadColumn\((?<index>\d+)\)\][\s\S]{0,300}?public\s+string\s+(?<property>[A-Za-z_]\w*)\s*\{",
            RegexOptions.Compiled);
        private static readonly Regex ClassPattern = new Regex(
            @"\b(?:public|internal|private|protected)\s+(?:static\s+)?(?:partial\s+)?class\s+(?<name>[A-Za-z_]\w*)\b",
            RegexOptions.Compiled);

        public static GeneratedAppModificationResult Modify(string projectPath, int labelColumn)
        {
            var projectDirectory = Path.GetDirectoryName(projectPath);
            var sourceFiles = Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(path => path.IndexOf(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar,
                                  System.StringComparison.OrdinalIgnoreCase) < 0 &&
                              path.IndexOf(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar,
                                  System.StringComparison.OrdinalIgnoreCase) < 0)
                .ToList();
            var programPath = sourceFiles.FirstOrDefault(path =>
                string.Equals(Path.GetFileName(path), "Program.cs", System.StringComparison.OrdinalIgnoreCase));
            if (programPath == null)
            {
                return new GeneratedAppModificationResult
                {
                    Message = "В сгенерированном проекте не найден Program.cs."
                };
            }

            var modelSource = sourceFiles.Select(File.ReadAllText)
                .FirstOrDefault(source => Regex.IsMatch(source, @"class\s+ModelInput\b") &&
                    Regex.IsMatch(source, @"PredictAllLabels\s*\(\s*ModelInput\s+"));
            if (modelSource == null)
            {
                return new GeneratedAppModificationResult
                {
                    ProgramPath = programPath,
                    Message = "Не удалось найти сгенерированный тип ModelInput и метод предсказания модели."
                };
            }

            var modelNamespace = Regex.Match(modelSource,
                @"namespace\s+(?<name>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)");
            var modelInput = Regex.Match(modelSource, @"class\s+ModelInput\b");
            var modelOwner = modelInput.Success
                ? ClassPattern.Matches(modelSource.Substring(0, modelInput.Index)).Cast<Match>().LastOrDefault()
                : null;
            var modelClassName = modelOwner == null ? null : modelOwner.Groups["name"].Value;
            var textProperty = LoadColumnPattern.Matches(modelSource).Cast<Match>()
                .FirstOrDefault(match => int.Parse(match.Groups["index"].Value) != labelColumn);

            if (!modelNamespace.Success || string.IsNullOrWhiteSpace(modelClassName) || textProperty == null)
            {
                return new GeneratedAppModificationResult
                {
                    ProgramPath = programPath,
                    Message = "Не удалось определить пространство имён, текстовую колонку или API предсказания модели."
                };
            }

            Encoding encoding;
            using (var reader = new StreamReader(programPath, Encoding.UTF8, true))
            {
                reader.ReadToEnd();
                encoding = reader.CurrentEncoding;
            }

            var modelType = "global::" + modelNamespace.Groups["name"].Value + "." + modelClassName;
            var modelInputType = modelType + ".ModelInput";
            var propertyName = textProperty.Groups["property"].Value;
            var generatedProgram = new StringBuilder();
            generatedProgram.AppendLine("using System;")
                .AppendLine("using System.Linq;")
                .AppendLine("internal static class Program")
                .AppendLine("{")
                .AppendLine("    private static int Main(string[] args)")
                .AppendLine("    {")
                .AppendLine("        if (args.Length == 0)")
                .AppendLine("        {")
                .AppendLine("            Console.Error.WriteLine(\"Pass text to classify as the first argument.\");")
                .AppendLine("            return 2;")
                .AppendLine("        }")
                .Append("        var input = new ").Append(modelInputType).Append(" { ")
                .Append(propertyName).AppendLine(" = string.Join(\" \", args) };")
                .Append("        var best = ").Append(modelType)
                .AppendLine(".PredictAllLabels(input).FirstOrDefault();")
                .AppendLine("        if (best.Key == null)")
                .AppendLine("        {")
                .AppendLine("            Console.Error.WriteLine(\"The model returned no predictions.\");")
                .AppendLine("            return 3;")
                .AppendLine("        }")
                .AppendLine("        Console.WriteLine(\"Prediction: {0}, Score: {1}\", best.Key, best.Value);")
                .AppendLine("        return 0;")
                .AppendLine("    }")
                .AppendLine("}");

            File.WriteAllText(programPath, generatedProgram.ToString(), encoding);
            return new GeneratedAppModificationResult
            {
                Success = true,
                ProgramPath = programPath,
                Message = "Program.cs изменён: он принимает текст аргументом и выводит предсказанный класс и оценку."
            };
        }
    }
}
