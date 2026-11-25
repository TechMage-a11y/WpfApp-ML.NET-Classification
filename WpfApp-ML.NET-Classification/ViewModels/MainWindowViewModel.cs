using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using WpfApp_ML.NET_Classification.Services;

namespace WpfApp_ML.NET_Classification
{
    public sealed class MainWindowViewModel : ViewModelBase
    {
        private readonly MlnetProcessRunner _processRunner = new MlnetProcessRunner();
        private readonly StringBuilder _trainingLog = new StringBuilder();
        private readonly object _trainingLogLock = new object();
        private readonly RelayCommand _trainCommand;
        private readonly RelayCommand _stopCommand;
        private readonly RelayCommand _predictCommand;
        private readonly SynchronizationContext _uiContext;
        private bool _stopRequested;
        private string _datasetPath;
        private string _datasetPreview;
        private int _datasetColumnCount;
        private string _outputDirectory;
        private string _trainingOutput;
        private string _trainingStatus = "Ожидание обучения";
        private string _metricsStatus = "Метрики появятся после обучения.";
        private string _modelPath;
        private string _generatedProjectPath;
        private string _predictionResult = "Сначала обучите модель.";
        private string _predictionText;
        private bool _isTraining;
        private bool _isPredicting;
        private bool _canPredict;

        public MainWindowViewModel(SynchronizationContext uiContext)
        {
            _uiContext = uiContext ?? SynchronizationContext.Current;
            Metrics = new ObservableCollection<MetricItem>();
            _trainCommand = new RelayCommand(async () => await TrainAsync(), () => !IsTraining);
            _stopCommand = new RelayCommand(StopTraining, () => IsTraining);
            _predictCommand = new RelayCommand(async () => await PredictAsync(),
                () => !IsPredicting && CanPredict && !string.IsNullOrWhiteSpace(GeneratedProjectPath) &&
                      File.Exists(GeneratedProjectPath) && !string.IsNullOrWhiteSpace(PredictionText));
            _processRunner.OutputReceived += ProcessRunner_OutputReceived;
        }

        public ObservableCollection<MetricItem> Metrics { get; private set; }
        public ICommand TrainCommand { get { return _trainCommand; } }
        public ICommand StopCommand { get { return _stopCommand; } }
        public ICommand PredictCommand { get { return _predictCommand; } }

        public string DatasetPath { get { return _datasetPath; } private set { SetProperty(ref _datasetPath, value); } }
        public string DatasetPreview { get { return _datasetPreview; } private set { SetProperty(ref _datasetPreview, value); } }
        public int DatasetColumnCount { get { return _datasetColumnCount; } private set { SetProperty(ref _datasetColumnCount, value); } }
        public string LabelColumn { get; set; } = "0";
        public string HasHeader { get; set; } = "false";
        public string ModelName { get; set; } = "SentimentModel";
        public string TrainTime { get; set; } = "60";
        public string Verbosity { get; set; } = "minimal";
        public string OutputDirectory { get { return _outputDirectory; } set { SetProperty(ref _outputDirectory, value); } }
        public string TrainingOutput { get { return _trainingOutput; } private set { SetProperty(ref _trainingOutput, value); } }
        public string TrainingStatus { get { return _trainingStatus; } private set { SetProperty(ref _trainingStatus, value); } }
        public string MetricsStatus { get { return _metricsStatus; } private set { SetProperty(ref _metricsStatus, value); } }
        public string ModelPath { get { return _modelPath; } private set { SetProperty(ref _modelPath, value); } }
        public string GeneratedProjectPath { get { return _generatedProjectPath; } private set { SetProperty(ref _generatedProjectPath, value); } }
        public string PredictionText
        {
            get { return _predictionText; }
            set
            {
                if (SetProperty(ref _predictionText, value)) _predictCommand.RaiseCanExecuteChanged();
            }
        }
        public string PredictionResult { get { return _predictionResult; } private set { SetProperty(ref _predictionResult, value); } }
        public bool IsTraining
        {
            get { return _isTraining; }
            private set
            {
                if (SetProperty(ref _isTraining, value))
                {
                    _trainCommand.RaiseCanExecuteChanged();
                    _stopCommand.RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(CanChooseDataset));
                }
            }
        }
        public bool IsPredicting
        {
            get { return _isPredicting; }
            private set
            {
                if (SetProperty(ref _isPredicting, value)) _predictCommand.RaiseCanExecuteChanged();
            }
        }
        public bool CanPredict { get { return _canPredict; } private set { SetProperty(ref _canPredict, value); _predictCommand.RaiseCanExecuteChanged(); } }
        public bool CanChooseDataset { get { return !IsTraining; } }

        public void SetDataset(string path)
        {
            var preview = DatasetPreviewReader.Read(path, 20);
            var columnCount = DatasetPreviewReader.CountColumns(path);
            DatasetPreview = preview;
            DatasetColumnCount = columnCount;
            DatasetPath = path;
            OutputDirectory = Path.GetDirectoryName(path);
            ResetResults("Метрики появятся после обучения.", "Сначала обучите модель.", false);
        }

        private async Task TrainAsync()
        {
            if (!TrainingOptionsValidator.TryCreate(DatasetPath, DatasetColumnCount, LabelColumn,
                    HasHeader, ModelName, TrainTime, OutputDirectory, Verbosity,
                    out var options, out var validationError))
            {
                TrainingStatus = validationError;
                return;
            }

            var labelColumn = options.LabelColumn;
            var modelName = options.ModelName;
            var outputDirectory = options.OutputDirectory;
            var hasHeader = options.HasHeader;
            var trainTime = options.TrainTime;

            try
            {
                outputDirectory = Path.GetFullPath(outputDirectory);
                OutputDirectory = outputDirectory;
                Directory.CreateDirectory(outputDirectory);
            }
            catch (IOException ex)
            {
                ReportTrainingError("Не удалось подготовить папку результатов.", "Ошибка доступа к папке: ", ex);
                return;
            }
            catch (UnauthorizedAccessException ex)
            {
                ReportTrainingError("Нет доступа к папке результатов.", "Нет доступа к папке: ", ex);
                return;
            }
            catch (ArgumentException ex)
            {
                ReportTrainingError("Указан некорректный путь к папке результатов.", "Некорректный путь: ", ex);
                return;
            }
            catch (NotSupportedException ex)
            {
                ReportTrainingError("Указан неподдерживаемый путь к папке результатов.", "Неподдерживаемый путь: ", ex);
                return;
            }

            var startInfo = MlnetCommandBuilder.CreateStartInfo(
                DatasetPath, labelColumn, hasHeader, modelName, trainTime, outputDirectory, Verbosity);
            ResetResults("Ожидание метрик обучения...", "Ожидание завершения обучения.", true);
            lock (_trainingLogLock)
            {
                _trainingLog.Clear();
            }

            TrainingOutput = string.Empty;
            _stopRequested = false;
            IsTraining = true;
            TrainingStatus = "Обучение выполняется...";
            AppendLog("Запуск: mlnet " + startInfo.Arguments, false);
            var trainingStartedUtc = DateTime.UtcNow;

            try
            {
                var exitCode = await _processRunner.RunAsync(startInfo);
                AppendLog("mlnet завершился с кодом " + exitCode + ".", exitCode != 0);
                if (_stopRequested)
                {
                    TrainingStatus = "Обучение остановлено пользователем.";
                    return;
                }

                if (exitCode != 0)
                {
                    TrainingStatus = "Обучение завершилось с ошибкой. Проверьте вывод mlnet.";
                    return;
                }

                ShowTrainingResults(outputDirectory, modelName, labelColumn, trainingStartedUtc);
            }
            catch (Win32Exception ex)
            {
                ReportTrainingError("Не удалось запустить mlnet.", "Проверьте, установлен ли ML.NET CLI и доступна ли команда mlnet в PATH: ", ex);
            }
            catch (InvalidOperationException ex)
            {
                ReportTrainingError("Ошибка запуска обучения.", "Ошибка процесса mlnet: ", ex);
            }
            catch (IOException ex)
            {
                ReportTrainingError("Ошибка чтения результатов обучения.", "Ошибка артефактов: ", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                ReportTrainingError("Нет доступа к результатам обучения.", "Ошибка доступа: ", ex);
            }
            finally
            {
                IsTraining = false;
            }
        }

        private void ResetResults(string metricsStatus, string predictionStatus, bool clearInput)
        {
            Metrics.Clear();
            MetricsStatus = metricsStatus;
            ModelPath = null;
            GeneratedProjectPath = null;
            CanPredict = false;
            PredictionResult = predictionStatus;
            if (clearInput) PredictionText = null;
        }

        private void ReportTrainingError(string status, string message, Exception exception)
        {
            TrainingStatus = status;
            AppendLog(message + exception.Message, true);
        }

        private static string GetMetricDisplayName(string name)
        {
            switch (name)
            {
                case "MacroAccuracy": return "Макроусреднённая точность";
                case "MicroAccuracy": return "Микроусреднённая точность";
                case "Accuracy": return "Точность";
                case "F1Score": return "F1-мера";
                case "AUC": return "Площадь под ROC-кривой (AUC)";
                case "LogLoss": return "Логарифмическая потеря";
                default: return name;
            }
        }

        private static string GetPredictionErrorMessage(int exitCode)
        {
            switch (exitCode)
            {
                case 2: return "Не передан текст для классификации (код завершения 2).";
                case 3: return "Модель не вернула предсказаний (код завершения 3).";
                default: return "Ошибка классификации (код завершения " + exitCode + ").";
            }
        }

        private void ShowTrainingResults(string outputDirectory, string modelName,
            int labelColumn, DateTime trainingStartedUtc)
        {
            string log;
            lock (_trainingLogLock)
            {
                log = _trainingLog.ToString();
            }

            var parsedMetrics = MlnetResultParser.ParseMetrics(log);
            foreach (var metric in parsedMetrics)
            {
                Metrics.Add(new MetricItem { Name = GetMetricDisplayName(metric.Key), Value = metric.Value });
            }
            MetricsStatus = parsedMetrics.Count == 0
                ? "Метрики не обнаружены в выводе mlnet."
                : "Лучшие найденные значения метрик:";

            var artifacts = MlnetResultParser.FindArtifacts(outputDirectory, modelName, trainingStartedUtc);
            if (artifacts.ModelPath == null)
            {
                throw new InvalidDataException("После обучения не найден созданный файл модели.");
            }

            ModelPath = artifacts.ModelPath;
            if (artifacts.ProjectPath == null)
            {
                TrainingStatus = "Модель создана, но консольный проект не найден.";
                AppendLog("В папке результатов не найден сгенерированный консольный проект.", true);
                return;
            }

            GeneratedProjectPath = artifacts.ProjectPath;
            var modification = GeneratedAppModifier.Modify(artifacts.ProjectPath, labelColumn);
            AppendLog(modification.Message, !modification.Success);
            if (!modification.Success)
            {
                TrainingStatus = "Модель создана; не удалось подготовить консольный проект для классификации.";
                PredictionResult = "Проверьте сообщение об ошибке в журнале.";
                return;
            }

            CanPredict = true;
            TrainingStatus = "Обучение завершено успешно.";
            PredictionResult = "Введите текст и нажмите «Классифицировать».";
        }

        private async Task PredictAsync()
        {
            if (string.IsNullOrWhiteSpace(PredictionText))
            {
                PredictionResult = "Введите текст для классификации.";
                return;
            }

            var projectPath = GeneratedProjectPath;
            if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
            {
                PredictionResult = "Сгенерированный проект не найден. Повторите обучение.";
                return;
            }

            IsPredicting = true;
            PredictionResult = "Классификация выполняется...";
            try
            {
                var result = await PredictionClient.PredictAsync(projectPath, PredictionText);
                var output = result.StandardOutput + Environment.NewLine + result.StandardError;
                if (result.ExitCode != 0)
                {
                    PredictionResult = GetPredictionErrorMessage(result.ExitCode);
                    AppendLog("Ошибка предсказания: " + output.Trim(), true);
                    return;
                }

                var prediction = PredictionResultParser.Parse(result.StandardOutput);
                if (prediction == null)
                {
                    PredictionResult = "Не удалось разобрать метку в ответе приложения.";
                    AppendLog("Не удалось распознать ответ консольного приложения: " + output.Trim(), true);
                    return;
                }

                PredictionResult = "Класс: " + prediction.Label +
                    (string.IsNullOrWhiteSpace(prediction.Score) ? string.Empty : "    Оценка: " + prediction.Score);
            }
            catch (Win32Exception ex)
            {
                ReportPredictionError("Не удалось запустить .NET SDK (команду dotnet).", ex);
            }
            catch (InvalidOperationException ex)
            {
                ReportPredictionError("Не удалось выполнить классификацию.", ex);
            }
            catch (IOException ex)
            {
                ReportPredictionError("Ошибка доступа к сгенерированному проекту.", ex);
            }
            finally
            {
                IsPredicting = false;
            }
        }

        private void StopTraining()
        {
            try
            {
                _stopRequested = true;
                _processRunner.Stop();
                TrainingStatus = "Остановка обучения...";
                AppendLog("Запрошена остановка процесса mlnet.", false);
            }
            catch (InvalidOperationException ex)
            {
                AppendLog("Не удалось остановить mlnet: " + ex.Message, true);
            }
            catch (Win32Exception ex)
            {
                AppendLog("Не удалось остановить mlnet: " + ex.Message, true);
            }
        }

        private void ReportPredictionError(string status, Exception exception)
        {
            PredictionResult = status;
            AppendLog(status + " " + exception.Message, true);
        }

        private void ProcessRunner_OutputReceived(object sender, ProcessOutputEventArgs e)
        {
            AppendLog(e.Line, e.IsError);
        }

        private void AppendLog(string message, bool isError)
        {
            if (message == null)
            {
                return;
            }

            lock (_trainingLogLock)
            {
                _trainingLog.AppendLine(message);
            }

            PostToUi(() =>
            {
                TrainingOutput = (TrainingOutput ?? string.Empty) +
                    (isError ? "[ошибка] " : string.Empty) + message + Environment.NewLine;
            });
        }

        private void PostToUi(Action action)
        {
            if (_uiContext == null || SynchronizationContext.Current == _uiContext)
            {
                action();
            }
            else
            {
                _uiContext.Post(state => action(), null);
            }
        }

    }
}
