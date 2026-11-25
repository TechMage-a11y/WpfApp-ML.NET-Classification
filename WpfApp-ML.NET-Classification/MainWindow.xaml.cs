using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace WpfApp_ML.NET_Classification
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainWindowViewModel(new DispatcherSynchronizationContext(Dispatcher));
            DataContext = _viewModel;
        }

        private void TrainingOutputTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            TrainingOutputTextBox.ScrollToEnd();
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
                _viewModel.SetDataset(dialog.FileName);
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
