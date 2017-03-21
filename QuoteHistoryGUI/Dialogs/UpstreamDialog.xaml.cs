using log4net;
using QuoteHistoryGUI.HistoryTools;
using QuoteHistoryGUI.HistoryTools.Interactor;
using QuoteHistoryGUI.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// Interaction logic for UpstreamDialog.xaml
    /// </summary>
    public partial class UpstreamDialog : Window
    {
        HistoryInteractor _interactor;
        BackgroundWorker UpstreamWorker;
        StorageInstanceModel _source;
        SelectTemplateWorker temW;
        string templateText;
        bool is2levelUpstream = false;
        public static readonly ILog log = LogManager.GetLogger(typeof(StorageSelectionDialog));
        bool canceled = false;
        Dispatcher _dispatcher;
        public UpstreamDialog(StorageInstanceModel source, HistoryInteractor interactor)
        {
            try
            {
                log.Info("Upstream dialog initializing...");
                InitializeComponent();

                _dispatcher = this.Dispatcher;

                //Level2Box.IsChecked = true;
                TemplateBox.SetData(source.Folders.Select(f => f.Name), Enumerable.Range(2010, DateTime.Today.Year - 2009).Select(y => y.ToString()),
                    interactor.Selection == null || interactor.Selection.Count == 0 ? new string[] { "*" } : HistoryInteractor.GetTemplates(interactor.Selection));
                _interactor = interactor;
                _interactor.Selection.Clear();
                _source = source;
                log.Info("Upstream dialog initialized");
                this.Closed += OnWindowClosing;
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                throw ex;
            }
        }
        private void UpstreamButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                canceled = false;
                log.Info("Upstream calling...");
                UpstreamWorker = new BackgroundWorker();
                _interactor.Source = _source;
                //is2levelUpstream = Level2Box.IsChecked.HasValue && Level2Box.IsChecked.Value;

                temW = new SelectTemplateWorker(_interactor.Source.Folders, new HistoryLoader(Application.Current.MainWindow.Dispatcher, _interactor.Source.HistoryStoreDB));
                templateText = string.Join(";\n", TemplateBox.Templates.Source.Select(t => t.Value));

                UpstreamButton.IsEnabled = false;
                UpstreamWorker.WorkerReportsProgress = true;
                UpstreamWorker.WorkerSupportsCancellation = true;
                UpstreamWorker.DoWork += worker_Upstream;
                UpstreamWorker.ProgressChanged += CopyProgressChanged;
                UpstreamWorker.RunWorkerCompleted += worker_Upstreamed;
                UpstreamWorker.RunWorkerCompleted += QHAppWindowModel.throwExceptions;
                UpstreamWorker.RunWorkerAsync(UpstreamWorker);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                throw ex;
            }
        }

        private void worker_Upstream(object sender, DoWorkEventArgs e)
        {
            try
            {
                var templates = templateText.Split(new[] { ";\n" }, StringSplitOptions.None);
                BackgroundWorker worker = e.Argument as BackgroundWorker;

                int degreeOfParallelism = 1;
                int upstreamType = 0;
                _dispatcher.Invoke(delegate
                {
                    var item = DegreeBox.SelectedItem as ComboBoxItem;
                    var textBlock = item.Content as TextBlock;
                    degreeOfParallelism = int.Parse(textBlock.Text);

                    upstreamType = TypeBox.SelectedIndex;
                });

                _interactor.Upstream(templates, worker, temW, t => { worker.ReportProgress(1, t); }, degreeOfParallelism, upstreamType);

            }
            catch (Exception ex)
            {
                _dispatcher.Invoke(delegate
                {
                    MessageBox.Show(this, ex.Message + ",\nStackTrace: " + ex.StackTrace, "Upstream error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            _interactor.Source.Refresh();
        }
        private void CopyProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            UpstreamStatusBlock.Text = e.UserState as string;
            log.Info("Upstream progress report: " + e.UserState as string);
        }
        private void worker_Upstreamed(object sender, RunWorkerCompletedEventArgs e)
        {
            if (!canceled)
            {
                log.Info("Upstream performed");
                _dispatcher.Invoke(delegate
                { MessageBox.Show(this, "Upstream update completed", "Result", MessageBoxButton.OK, MessageBoxImage.Asterisk); });
            }
            Close();
            UpstreamButton.IsEnabled = true;
        }


        private void templateHelpButton_Click(object sender, RoutedEventArgs e)
        {
            HelpDialog.ShowHelp("upstream");
        }

        public void OnWindowClosing(object sender, EventArgs e)
        {
            if (UpstreamWorker != null && UpstreamWorker.IsBusy)
            {
                canceled = true;
                UpstreamWorker.CancelAsync();
                _dispatcher.Invoke(delegate
                { MessageBox.Show(this, "Canceled!", "Close message", MessageBoxButton.OK, MessageBoxImage.Asterisk); });

                log.Info("Upstream canceled");
            }
        }

    }
}
