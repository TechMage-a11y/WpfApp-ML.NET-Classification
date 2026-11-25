using System;
using System.Windows;

namespace WpfApp_ML.NET_Classification
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            var application = new Application();
            application.Run(new MainWindow());
        }
    }
}
