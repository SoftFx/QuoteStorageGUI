using log4net;
using QuoteHistoryGUI.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace QuoteHistoryGUI.Dialogs
{
    /// <summary>
    /// Interaction logic for CompactDialog.xaml
    /// </summary>
    public partial class CompactDialog : Window
    {

        public static readonly ILog log = LogManager.GetLogger(typeof(StorageSelectionDialog));
        BackgroundWorker CompactWorker;
        Dispatcher _dispatcher;
        public CompactDialog(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            InitializeComponent();
            SourceBox.ItemsSource = AppConfigManager.GetPathes();
        }

        private void CompactButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                log.Info("Compact calling...");
                AppConfigManager.SavePathes(SourceBox.Text);
                CompactWorker = new BackgroundWorker();
                CompactButton.IsEnabled = false;
                CompactWorker.WorkerReportsProgress = true;
                CompactWorker.WorkerSupportsCancellation = true;
                CompactWorker.DoWork += worker_CompactReport;
                CompactWorker.ProgressChanged += CopyProgressChanged;
                CompactWorker.RunWorkerCompleted += worker_Copied;
                CompactWorker.RunWorkerCompleted += QHAppWindowModel.throwExceptions;
                CompactWorker.RunWorkerAsync(CompactWorker);
                string path = SourceBox.Text;
                Task.Run(delegate {
                    try {
                        LevelDB.DB.Compact(path + "\\HistoryDB", null, null);
                        CompactWorker.CancelAsync();
                    }
                    catch(Exception ex)
                    {
                        log.Error(ex.Message);
                    }
                });

            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                throw ex;
            }
        }

        private void worker_CompactReport(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = e.Argument as BackgroundWorker;
            DateTime time = DateTime.UtcNow;
            while (!worker.CancellationPending)
            {
                worker.ReportProgress(1, "Compacting. Time: " + (DateTime.UtcNow - time));
                Thread.Sleep(500);
            }
        }

        private void CopyProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            CompactStatusBlock.Text = e.UserState as string;
        }
        private void worker_Copied(object sender, RunWorkerCompletedEventArgs e)
        {
            _dispatcher.Invoke(delegate
            { MessageBox.Show(this, "Done!", "Result", MessageBoxButton.OK, MessageBoxImage.Asterisk); });
            Close();
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (CompactWorker != null)
            {
                if (CompactWorker.IsBusy)
                {
                    e.Cancel = true;
                    _dispatcher.Invoke(delegate
                    { MessageBox.Show(this, "Unable to abort compact operation", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning); });
                }
            }

        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            { };
            if (SourceBox.Text != "")
                dlg.SelectedPath = SourceBox.Text;
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SourceBox.Text = dlg.SelectedPath;
            }
        }

    }
}
