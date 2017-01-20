using log4net;
using QuoteHistoryGUI.HistoryTools;
using QuoteHistoryGUI.HistoryTools.Interactor;
using QuoteHistoryGUI.Models;
using QuoteHistoryGUI.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
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
using System.Windows.Threading;

namespace QuoteHistoryGUI.Dialogs
{
    /// <summary>
    /// Interaction logic for SmartImportDialog.xaml
    /// </summary>
    public partial class SmartImportDialog : Window
    {
        HistoryInteractor _interactor;
        ObservableCollection<StorageInstanceModel> _tabs;
        BackgroundWorker CopyWorker;
        bool isMetaMatching = false;
        StorageInstanceModel _source;
        StorageInstanceModel _destination;
        SelectTemplateWorker temW;
        string templateText;
        bool isMove = false;
        bool canceled = false;
        public static readonly ILog log = LogManager.GetLogger(typeof(StorageSelectionDialog));
        Dispatcher _dispatcher;
        public SmartImportDialog(StorageInstanceModel source, ObservableCollection<StorageInstanceModel> tabs, HistoryInteractor interactor)
        {
            try
            {
                log.Info("Import dialog initializing...");
                InitializeComponent();

                _dispatcher = this.Dispatcher;

                this.Closing += Window_Closing;

                OperationTypeBox.IsEnabled = false;
                Source.Text = source.StoragePath;
                CopyButton.IsEnabled = false;
                foreach (var tab in tabs)
                {
                    if (tab != source) _destination = tab;
                }

                var win = Application.Current.MainWindow as QHAppWindowView;

                _interactor = interactor;

                TemplateBox.SetData(source.Folders.Select(f => f.Name), Enumerable.Range(2010, DateTime.Today.Year - 2009).Select(y => y.ToString()),
                    HistoryInteractor.GetTemplates(interactor.Selection));

                SourceBox.ItemsSource = AppConfigManager.GetPathes();

                _interactor.Selection.Clear();
                _tabs = tabs;
                _source = source;
                log.Info("Import dialog initialized");
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                throw ex;
            }
        }

        public SmartImportDialog()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                log.Info("Import calling...");
                AppConfigManager.SavePathes(SourceBox.Text);

                if (_source.FilePath == SourceBox.Text)
                {
                    MessageBox.Show(this, "Unavailable to import from the same storage ", "Import", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    log.Info("Import canceled");
                    return;
                }

                if (TemplateRadioButton.IsChecked.Value == true)
                {
                    isMove = OperationTypeBox.SelectedIndex == 1;
                    CopyWorker = new BackgroundWorker();
                    

                    if (!Directory.Exists(SourceBox.Text + "\\HistoryDB"))
                        Directory.CreateDirectory(SourceBox.Text + "\\HistoryDB");
                    _destination = new StorageInstanceModel(SourceBox.Text, _interactor.Dispatcher, null, StorageInstanceModel.OpenMode.ReadWrite, true);
                    _interactor.Source = _destination;
                    _interactor.Destination = _source;



                    if (_interactor.Destination.openMode == StorageInstanceModel.OpenMode.ReadOnly)
                    {
                        MessageBox.Show(this, "Unable to modify storage opened in readonly mode", "Copy", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        return;
                    }

                    temW = new SelectTemplateWorker(_interactor.Source.Folders, new HistoryLoader(Application.Current.MainWindow.Dispatcher, _interactor.Source.HistoryStoreDB));
                    templateText = string.Join(";\n", TemplateBox.Templates.Source.Select(t => t.Value));

                    CopyButton.IsEnabled = false;
                    CopyWorker.WorkerReportsProgress = true;
                    CopyWorker.WorkerSupportsCancellation = true;
                    CopyWorker.DoWork += worker_Copy;
                    CopyWorker.ProgressChanged += CopyProgressChanged;
                    CopyWorker.RunWorkerCompleted += worker_Copied;
                    CopyWorker.RunWorkerCompleted += QHAppWindowModel.throwExceptions;
                    CopyWorker.RunWorkerAsync(CopyWorker);
                }
                else
                {

                    

                    isMove = OperationTypeBox.SelectedIndex == 1;
                    CopyWorker = new BackgroundWorker();


                    if (!Directory.Exists(SourceBox.Text + "\\HistoryDB"))
                        Directory.CreateDirectory(SourceBox.Text + "\\HistoryDB");
                    _destination = new StorageInstanceModel(SourceBox.Text, _interactor.Dispatcher);
                    _interactor.Source = _destination;
                    _interactor.Destination = _source;

                    if (_interactor.Destination.openMode == StorageInstanceModel.OpenMode.ReadOnly)
                    {
                        MessageBox.Show(this, "Unable to modify storage opened in readonly mode", "Import", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        return;
                    }

                    CopyButton.IsEnabled = false;
                    CopyWorker.WorkerReportsProgress = true;
                    CopyWorker.WorkerSupportsCancellation = true;
                    CopyWorker.DoWork += worker_Import;
                    CopyWorker.ProgressChanged += CopyProgressChanged;
                    CopyWorker.RunWorkerCompleted += worker_Copied;
                    CopyWorker.RunWorkerCompleted += QHAppWindowModel.throwExceptions;
                    CopyWorker.RunWorkerAsync(CopyWorker);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                throw ex;
            }
        }

        private void worker_Copy(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = e.Argument as BackgroundWorker;
            var templates = templateText.Split(new[] { ";\n" }, StringSplitOptions.None);
            if (isMetaMatching)
            {
                var templList = new List<string>(templates);
                var matchEnum = temW.GetFromMetaByMatch(templList, _source, worker);
                _interactor.Copy(matchEnum, worker);
                _interactor.Copy(matchEnum, worker, true);
            }
            else
            {



                foreach (var templ in templates)
                {
                    worker.ReportProgress(1, "Template: " + templ);
                    var matched = temW.GetByMatch(templ, worker);

                    _interactor.Copy(worker, matched);
                    if (isMove)
                    {
                        _interactor.Dispatcher = Dispatcher;
                        _interactor.Delete(matched);
                        _interactor.Dispatcher = null;
                    }


                }

            }
            _interactor.Destination.Refresh();
        }

        private void worker_Import(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = e.Argument as BackgroundWorker;
            _interactor.Import(true, worker);
            _interactor.Destination.Refresh();
        }
        private void CopyProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            CopyStatusBlock.Text = e.UserState as string;
            log.Info("Import progress report: " + e.UserState as string);
        }
        private void worker_Copied(object sender, RunWorkerCompletedEventArgs e)
        {
            if (canceled)
            {
                _dispatcher.Invoke(delegate
                { MessageBox.Show(this, "Canceled!", "Close message", MessageBoxButton.OK, MessageBoxImage.Asterisk); });
            }
            else
            {
                _dispatcher.Invoke(delegate
                { MessageBox.Show(this, "Done!", "Result", MessageBoxButton.OK, MessageBoxImage.Asterisk); });
            }
            Close();
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (CopyWorker != null)
            {
                if (CopyWorker.IsBusy)
                {
                    canceled = true;
                    CopyWorker.CancelAsync();
                    e.Cancel = true;
                }
                else
                {
                    if (_interactor.Source != null)
                        _interactor.Source.HistoryStoreDB.Dispose();
                    if (_interactor.Destination != null)
                        _interactor.Destination.Refresh();
                }
            }
            
        }
        private void templateHelpButton_Click(object sender, RoutedEventArgs e)
        {
            HelpDialog.ShowHelp("import");
            //MessageBox.Show("Examples:\n\nAAABBB/2015/1/2/3;\nAAA*/*15/;\n*/*/*/1/M1*;\n*/2016/*/2*/*3/ticks file*;", "Template Help", MessageBoxButton.OK, MessageBoxImage.Question);
        }

        private void AllRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (TemplateExpander != null)
                TemplateExpander.IsEnabled = false;
            OperationTypeBox.IsEnabled = false;
        }

        private void TemplateRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (TemplateExpander != null)
                TemplateExpander.IsEnabled = true;
            OperationTypeBox.IsEnabled = true;
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
            CopyButton.IsEnabled = true;
        }

        private void SourceBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CopyButton.IsEnabled = true;
        }
    }
}
