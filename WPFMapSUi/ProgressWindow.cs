using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace WPFMapSUi
{
    public class ProgressWindow : Window
    {
        private ProgressBar progressBar;

        public ProgressWindow(string message)
        {
            Width = 300;
            Height = 100;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Title = "Progreso";

            var stackPanel = new StackPanel();
            stackPanel.Margin = new Thickness(10);

            stackPanel.Children.Add(new TextBlock { Text = message });

            progressBar = new ProgressBar();
            progressBar.Height = 20;
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            stackPanel.Children.Add(progressBar);

            Content = stackPanel;
        }

        public void UpdateProgress(double percent)
        {
            progressBar.Value = percent;
            Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        }
    }
}
