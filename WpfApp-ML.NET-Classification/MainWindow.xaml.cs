using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WpfApp_ML.NET_Classification.Services;

namespace WpfApp_ML.NET_Classification
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MlnetProcessRunner _processRunner = new MlnetProcessRunner();

        public MainWindow()
        {
            InitializeComponent();
            _processRunner.OutputReceived += ProcessRunner_OutputReceived;
        }

        private async void TrainButton_Click(object sender, RoutedEventArgs e)
        {
            int labelColumn;
            int trainTime;
            var modelName = ModelNameTextBox.Text.Trim();
            var headerItem = HasHeaderComboBox.SelectedItem as ComboBoxItem;

            if (!File.Exists(DatasetPathTextBox.Text))
            {
                MessageBox.Show(this, "Сначала выберите существующий файл датасета.",
                    "Датасет не выбран", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(LabelColumnTextBox.Text, out labelColumn) || labelColumn < 0)
            {
                MessageBox.Show(this, "Индекс колонки метки должен быть целым неотрицательным числом.",
                    "Некорректный индекс метки", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (headerItem == null || string.IsNullOrWhiteSpace(modelName) ||
                !int.TryParse(TrainTimeTextBox.Text, out trainTime) || trainTime <= 0)
            {
                MessageBox.Show(this, "Укажите наличие заголовка, имя модели и положительное время обучения.",
                    "Некорректные параметры", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var hasHeader = headerItem.Content.ToString() == "true";
            var startInfo = MlnetCommandBuilder.CreateStartInfo(
                DatasetPathTextBox.Text, labelColumn, hasHeader, modelName, trainTime);

            TrainButton.IsEnabled = false;
            ChooseDatasetButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            TrainingOutputTextBox.Clear();
            AppendProcessOutput("Запуск: mlnet " + startInfo.Arguments, false);

            try
            {
                var exitCode = await _processRunner.RunAsync(startInfo);
                AppendProcessOutput("mlnet завершился с кодом " + exitCode + ".", false);
            }
            catch (Win32Exception ex)
            {
                AppendProcessOutput("Не удалось запустить mlnet. Убедитесь, что ML.NET CLI установлен и доступен в PATH. " + ex.Message, true);
            }
            catch (InvalidOperationException ex)
            {
                AppendProcessOutput("Ошибка запуска или выполнения mlnet: " + ex.Message, true);
            }
            finally
            {
                TrainButton.IsEnabled = true;
                ChooseDatasetButton.IsEnabled = true;
                StopButton.IsEnabled = false;
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _processRunner.Stop();
                AppendProcessOutput("Запрошена остановка процесса mlnet.", false);
            }
            catch (InvalidOperationException ex)
            {
                AppendProcessOutput("Не удалось остановить mlnet: " + ex.Message, true);
            }
            catch (Win32Exception ex)
            {
                AppendProcessOutput("Не удалось остановить mlnet: " + ex.Message, true);
            }
        }

        private void ProcessRunner_OutputReceived(object sender, ProcessOutputEventArgs e)
        {
            AppendProcessOutput(e.Line, e.IsError);
        }

        private void AppendProcessOutput(string line, bool isError)
        {
            if (line == null)
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                TrainingOutputTextBox.AppendText((isError ? "[stderr] " : string.Empty) + line + Environment.NewLine);
                TrainingOutputTextBox.ScrollToEnd();
            }));
        }

        private void ChooseDatasetButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Выберите файл датасета",
                Filter = "Файлы датасетов (*.txt;*.csv;*.tsv)|*.txt;*.csv;*.tsv",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                var preview = DatasetPreviewReader.Read(dialog.FileName);
                DatasetPathTextBox.Text = dialog.FileName;
                PreviewTextBox.Text = preview;
            }
            catch (IOException ex)
            {
                MessageBox.Show(this, "Не удалось прочитать файл датасета.\n" + ex.Message,
                    "Ошибка чтения файла", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show(this, "Нет доступа для чтения файла датасета.\n" + ex.Message,
                    "Нет доступа", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
