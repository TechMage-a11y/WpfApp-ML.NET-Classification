using System.IO;
using System.Linq;
using System.Text;

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
                .FirstOrDefault(source => source.Contains("class ModelInput") &&
                    source.Contains("PredictAllLabels") && source.Contains("ModelInput "));
            if (modelSource == null)
            {
                return new GeneratedAppModificationResult
                {
                    ProgramPath = programPath,
                    Message = "Не удалось найти сгенерированный тип ModelInput и метод предсказания модели."
                };
            }

            var namespaceName = ReadIdentifierAfter(modelSource, "namespace ", '.');
            var modelInputIndex = modelSource.IndexOf("class ModelInput", System.StringComparison.Ordinal);
            var modelOwnerIndex = modelInputIndex > 0
                ? modelSource.LastIndexOf("class ", modelInputIndex - 1, System.StringComparison.Ordinal)
                : -1;
            var modelClassName = modelOwnerIndex < 0 ? null : ReadIdentifierAfter(modelSource, "class ", ' ', modelOwnerIndex);
            var textProperty = FindTextProperty(modelSource, labelColumn);

            if (string.IsNullOrWhiteSpace(namespaceName) || string.IsNullOrWhiteSpace(modelClassName) || textProperty == null)
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

            var modelType = "global::" + namespaceName + "." + modelClassName;
            var modelInputType = modelType + ".ModelInput";
            var propertyName = textProperty;
            var generatedProgram = new StringBuilder();
            generatedProgram.AppendLine("using System;")
                .AppendLine("using System.Linq;")
                .AppendLine("internal static class Program")
                .AppendLine("{")
                .AppendLine("    private static int Main(string[] args)")
                .AppendLine("    {")
                .AppendLine("        if (args.Length == 0)")
                .AppendLine("        {")
                .AppendLine("            Console.Error.WriteLine(\"Ошибка: передайте текст для классификации первым аргументом.\");")
                .AppendLine("            return 2;")
                .AppendLine("        }")
                .Append("        var input = new ").Append(modelInputType).Append(" { ")
                .Append(propertyName).AppendLine(" = string.Join(\" \", args) };")
                .Append("        var best = ").Append(modelType)
                .AppendLine(".PredictAllLabels(input).FirstOrDefault();")
                .AppendLine("        if (best.Key == null)")
                .AppendLine("        {")
                .AppendLine("            Console.Error.WriteLine(\"Ошибка: модель не вернула предсказаний.\");")
                .AppendLine("            return 3;")
                .AppendLine("        }")
                .AppendLine("        Console.WriteLine(\"Класс: {0}, Оценка: {1}\", best.Key, best.Value);")
                .AppendLine("        return 0;")
                .AppendLine("    }")
                .AppendLine("}");

            File.WriteAllText(programPath, generatedProgram.ToString(), encoding);
            return new GeneratedAppModificationResult
            {
                Success = true,
                ProgramPath = programPath,
                Message = "Файл Program.cs изменён: теперь он принимает текст аргументом и выводит предсказанный класс и оценку."
            };
        }

        private static string ReadIdentifierAfter(string source, string marker, char endCharacter, int startIndex = 0)
        {
            var start = source.IndexOf(marker, startIndex, System.StringComparison.Ordinal);
            if (start < 0)
            {
                return null;
            }

            start += marker.Length;
            var end = start;
            while (end < source.Length && (char.IsLetterOrDigit(source[end]) || source[end] == '_' ||
                                           (endCharacter == '.' && source[end] == '.')))
            {
                end++;
            }

            return end == start ? null : source.Substring(start, end - start);
        }

        private static string FindTextProperty(string source, int labelColumn)
        {
            var marker = "[LoadColumn(";
            var position = 0;
            while ((position = source.IndexOf(marker, position, System.StringComparison.Ordinal)) >= 0)
            {
                var indexStart = position + marker.Length;
                var indexEnd = source.IndexOf(")]", indexStart, System.StringComparison.Ordinal);
                if (indexEnd < 0 || !int.TryParse(source.Substring(indexStart, indexEnd - indexStart), out var column))
                {
                    position = indexStart;
                    continue;
                }

                if (column != labelColumn)
                {
                    var propertyName = ReadStringPropertyAfter(source, indexEnd + 2, marker);
                    if (propertyName != null)
                    {
                        return propertyName;
                    }
                }

                position = indexEnd + 2;
            }

            return null;
        }

        private static string ReadStringPropertyAfter(string source, int start, string nextColumnMarker)
        {
            var propertyStart = source.IndexOf("public ", start, System.StringComparison.Ordinal);
            var nextColumnStart = source.IndexOf(nextColumnMarker, start, System.StringComparison.Ordinal);
            if (propertyStart < 0 || (nextColumnStart >= 0 && nextColumnStart < propertyStart))
            {
                return null;
            }

            var typeStart = propertyStart + "public ".Length;
            while (typeStart < source.Length && char.IsWhiteSpace(source[typeStart]))
            {
                typeStart++;
            }

            var typeEnd = typeStart;
            while (typeEnd < source.Length && (char.IsLetterOrDigit(source[typeEnd]) || source[typeEnd] == '_'))
            {
                typeEnd++;
            }

            if (typeEnd - typeStart != "string".Length ||
                string.Compare(source, typeStart, "string", 0, "string".Length, System.StringComparison.Ordinal) != 0)
            {
                return null;
            }

            if (typeEnd < source.Length && source[typeEnd] == '?')
            {
                typeEnd++;
            }

            while (typeEnd < source.Length && char.IsWhiteSpace(source[typeEnd]))
            {
                typeEnd++;
            }

            var nameEnd = typeEnd;
            while (nameEnd < source.Length && (char.IsLetterOrDigit(source[nameEnd]) || source[nameEnd] == '_'))
            {
                nameEnd++;
            }

            return nameEnd == typeEnd ? null : source.Substring(typeEnd, nameEnd - typeEnd);
        }
    }
}
