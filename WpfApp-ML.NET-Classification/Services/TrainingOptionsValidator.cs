using System;
using System.Globalization;
using System.IO;

namespace WpfApp_ML.NET_Classification.Services
{
    public sealed class TrainingOptions
    {
        public string DatasetPath { get; set; }
        public int LabelColumn { get; set; }
        public bool HasHeader { get; set; }
        public string ModelName { get; set; }
        public int TrainTime { get; set; }
        public string OutputDirectory { get; set; }
        public string Verbosity { get; set; }
    }

    public static class TrainingOptionsValidator
    {
        public static bool TryCreate(string datasetPath, int datasetColumnCount, string labelColumnText,
            string hasHeaderText, string modelNameText, string trainTimeText, string outputDirectoryText,
            string verbosity, out TrainingOptions options, out string error)
        {
            options = null;
            error = null;

            if (!File.Exists(datasetPath))
            {
                error = "Выберите существующий файл датасета.";
                return false;
            }

            if (!int.TryParse(labelColumnText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var labelColumn) || labelColumn < 0)
            {
                error = "Индекс колонки метки должен быть неотрицательным целым числом.";
                return false;
            }

            if (datasetColumnCount < 2 || labelColumn >= datasetColumnCount)
            {
                error = "Индекс метки вне диапазона колонок датасета или в файле недостаточно колонок.";
                return false;
            }

            if (!bool.TryParse(hasHeaderText, out var hasHeader))
            {
                error = "Укажите, содержит ли датасет заголовок.";
                return false;
            }

            var modelName = (modelNameText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(modelName) || modelName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                error = "Укажите допустимое имя модели.";
                return false;
            }

            if (!int.TryParse(trainTimeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var trainTime) || trainTime <= 0)
            {
                error = "Время обучения должно быть положительным целым числом.";
                return false;
            }

            var outputDirectory = (outputDirectoryText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                error = "Укажите папку результатов.";
                return false;
            }

            options = new TrainingOptions
            {
                DatasetPath = datasetPath,
                LabelColumn = labelColumn,
                HasHeader = hasHeader,
                ModelName = modelName,
                TrainTime = trainTime,
                OutputDirectory = outputDirectory,
                Verbosity = verbosity
            };
            return true;
        }
    }
}
