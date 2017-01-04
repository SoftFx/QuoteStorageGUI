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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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

        public UpstreamDialog(StorageInstanceModel source, HistoryInteractor interactor)
        {
            try
            {
                log.Info("Upstream dialog initializing...");
                InitializeComponent();
                Level2Box.IsChecked = true;
                TemplateBox.SetData(source.Folders.Select(f => f.Name), Enumerable.Range(2010, DateTime.Today.Year - 2009).Select(y => y.ToString()),
                    HistoryInteractor.GetTemplates(interactor.Selection));
                _interactor = interactor;
                _interactor.Selection.Clear();
                _source = source;
                log.Info("Upstream dialog initialized");
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
                log.Info("Upstream calling...");
                UpstreamWorker = new BackgroundWorker();
                _interactor.Source = _source;
                is2levelUpstream = Level2Box.IsChecked.HasValue && Level2Box.IsChecked.Value;

                temW = new SelectTemplateWorker(_interactor.Source.Folders, new HistoryLoader(Application.Current.MainWindow.Dispatcher, _interactor.Source.HistoryStoreDB));
                templateText = string.Join(";\n", TemplateBox.Templates.Source.Select(t => t.Value));

                UpstreamButton.IsEnabled = false;
                UpstreamWorker.WorkerReportsProgress = true;
                UpstreamWorker.WorkerSupportsCancellation = true;
                UpstreamWorker.DoWork += worker_Upstream;
                UpstreamWorker.ProgressChanged += CopyProgressChanged;
                UpstreamWorker.RunWorkerCompleted += worker_Upstreamed;
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
                var templates = templateText.Split(new char[] { ';', ',', '\n', '\r' });
                BackgroundWorker worker = e.Argument as BackgroundWorker;
                var templNum = 0;
                foreach (var templ in templates)
                {
                    templNum++;
                    var matched = temW.GetByMatch(templ, worker, true);
                    DateTime lastReport = DateTime.UtcNow;
                    int upstramCnt = 0;
                    Folder lastDay = null;
                    ChunkFile lastTicksFile = null;
                    foreach (var sel in matched)
                    {
                        upstramCnt++;

                        if (worker != null && (DateTime.UtcNow - lastReport).TotalSeconds > 1)
                        {
                            worker.ReportProgress(1, "Upstreaming : " + upstramCnt + ", template processing: " + templNum);
                            lastReport = DateTime.UtcNow;
                        }

                        var chunk = sel as ChunkFile;
                        if (chunk != null)
                        {

                            if (chunk.Period == "ticks")
                            {
                                if (chunk.Parent.Parent != lastDay)
                                {
                                    _interactor.Source.tickToM1Update(chunk, false);
                                    lastDay = chunk.Parent.Parent;
                                }
                            }

                            if (chunk.Period == "ticks level2")
                            {
                                var res = _interactor.Source.tick2ToTickUpdate(chunk, false);
                                if (is2levelUpstream)
                                {
                                    if (res.Key.Length != 0)
                                    {
                                        if (res.Key[0].Parent.Parent != lastDay)
                                        {
                                            if (lastTicksFile != null)
                                                _interactor.Source.tickToM1Update(lastTicksFile, false);
                                            lastDay = res.Key[0].Parent.Parent;
                                        }
                                        lastTicksFile = res.Key[0];
                                    }
                                }
                            }
                        }

                    }
                    if (is2levelUpstream)
                    {
                        if (lastTicksFile != null)
                            _interactor.Source.tickToM1Update(lastTicksFile, false);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ",\nStackTrace: " + ex.StackTrace, "Upstream error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show("Upstream update completed", "Result", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            log.Info("Upstream performed");
            Close();
            UpstreamButton.IsEnabled = true;
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (UpstreamWorker != null && UpstreamWorker.IsBusy)
            {
                UpstreamWorker.CancelAsync();
                MessageBox.Show("Canceled!", "Closing message", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                log.Info("Upstream canceled");
            }
        }

        private void templateHelpButton_Click(object sender, RoutedEventArgs e)
        {
            HelpDialog.ShowHelp("upstream");
        }
    }
}
