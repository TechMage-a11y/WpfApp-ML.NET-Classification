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
    }
}
