using System.IO;
using System.Text;

namespace WpfApp_ML.NET_Classification.Services
{
    public static class DatasetPreviewReader
    {
        public static string Read(string filePath, int maxLines = 10)
        {
            var preview = new StringBuilder();
            var lineNumber = 0;

            using (var reader = new StreamReader(filePath, Encoding.UTF8, true))
            {
                string line;
                while (lineNumber < maxLines && (line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    preview.Append(lineNumber).Append(": ").AppendLine(line);
                }
            }

            return lineNumber == 0 ? "Файл пуст." : preview.ToString();
        }

        public static int CountColumns(string filePath)
        {
            using (var reader = new StreamReader(filePath, Encoding.UTF8, true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        return CountColumns(line, GetDelimiter(filePath, line));
                    }
                }
            }

            return 0;
        }

        private static char GetDelimiter(string filePath, string firstLine)
        {
            var extension = Path.GetExtension(filePath);
            if (string.Equals(extension, ".tsv", System.StringComparison.OrdinalIgnoreCase))
            {
                return '\t';
            }

            if (string.Equals(extension, ".csv", System.StringComparison.OrdinalIgnoreCase))
            {
                return ',';
            }

            return CountColumns(firstLine, '\t') > CountColumns(firstLine, ',') ? '\t' : ',';
        }

        private static int CountColumns(string line, char delimiter)
        {
            var columnCount = 1;
            var insideQuotes = false;

            for (var index = 0; index < line.Length; index++)
            {
                if (line[index] == '"')
                {
                    if (insideQuotes && index + 1 < line.Length && line[index + 1] == '"')
                    {
                        index++;
                    }
                    else
                    {
                        insideQuotes = !insideQuotes;
                    }
                }
                else if (line[index] == delimiter && !insideQuotes)
                {
                    columnCount++;
                }
            }

            return columnCount;
        }
    }
}
