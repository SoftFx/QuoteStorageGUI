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
    /// Interaction logic for ExportDialog.xaml
    /// </summary>
    public partial class ExportDialog : Window
    {
        HistoryInteractor _interactor;
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
        public ExportDialog(StorageInstanceModel source, HistoryInteractor interactor)
        {
            try
            {
                log.Info("Export dialog initializing...");
                InitializeComponent();

                _dispatcher = this.Dispatcher;

                this.Closing += Window_Closing;
                OperationTypeBox.IsEnabled = false;
                Source.Text = source.StoragePath;
                CopyButton.IsEnabled = false;

                var win = Application.Current.MainWindow as QHAppWindowView;

                _interactor = interactor;

                TemplateBox.SetData(source.Folders.Select(f => f.Name), Enumerable.Range(2010, DateTime.Today.Year - 2009).Select(y => y.ToString()),
                    HistoryInteractor.GetTemplates(source.Selection));
                _interactor.Selection.Clear();
                _source = source;

                DestinationBox.ItemsSource = AppConfigManager.GetPathes();

                log.Info("Export dialog initialized");
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                throw ex;
            }
        }

        public ExportDialog()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                log.Info("Export calling...");
                AppConfigManager.SavePathes(DestinationBox.Text);

                if (_source.FilePath == DestinationBox.Text)
                {
                    MessageBox.Show("Unavailable to export to the same storage ", "Import", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    log.Info("Export canceled");
                    return;
                }

                if (TemplateRadioButton.IsChecked.Value == true)
                {
                    isMove = OperationTypeBox.SelectedIndex == 1;
                    CopyWorker = new BackgroundWorker();
                    _interactor.Source = _source;

                    if (!Directory.Exists(DestinationBox.Text + "\\HistoryDB"))
                        Directory.CreateDirectory(DestinationBox.Text + "\\HistoryDB");
                    _destination = new StorageInstanceModel(DestinationBox.Text, _interactor.Dispatcher);
                    _interactor.Destination = _destination;



                    if (_interactor.Destination.openMode == StorageInstanceModel.OpenMode.ReadOnly)
                    {
                        MessageBox.Show("Unable to modify storage opened in readonly mode", "Copy", MessageBoxButton.OK, MessageBoxImage.Exclamation);
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


                    if (!Directory.Exists(DestinationBox.Text + "\\HistoryDB"))
                        Directory.CreateDirectory(DestinationBox.Text + "\\HistoryDB");
                    _destination = new StorageInstanceModel(DestinationBox.Text, _interactor.Dispatcher);
                    _interactor.Source = _source;
                    _interactor.Destination = _destination;

                    if (_interactor.Destination.openMode == StorageInstanceModel.OpenMode.ReadOnly)
                    {
                        MessageBox.Show("Unable to modify storage opened in readonly mode", "Copy", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        return;
                    }

                    CopyButton.IsEnabled = false;
                    CopyWorker.WorkerReportsProgress = true;
                    CopyWorker.WorkerSupportsCancellation = true;
                    CopyWorker.DoWork += worker_Export;
                    CopyWorker.ProgressChanged += CopyProgressChanged;
                    CopyWorker.RunWorkerCompleted += worker_Copied;
                    CopyWorker.RunWorkerCompleted += QHAppWindowModel.throwExceptions;
                    CopyWorker.RunWorkerAsync(CopyWorker);
                }
                log.Info("Export performed");
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
            var templates = templateText.Split(new[] { ";\n"}, StringSplitOptions.None);
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

        private void worker_Export(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = e.Argument as BackgroundWorker;
            _interactor.Import(true, worker);
        }
        private void CopyProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            CopyStatusBlock.Text = e.UserState as string;
            log.Info("Export progress report: "+ e.UserState as string);
        }



        private void worker_Copied(object sender, RunWorkerCompletedEventArgs e)
        {
            if (canceled)
            {
                _dispatcher.Invoke(delegate
                { MessageBox.Show("Canceled!", "Close message", MessageBoxButton.OK, MessageBoxImage.Asterisk); });
            }
            else
            {
                _dispatcher.Invoke(delegate
                { MessageBox.Show("Done!", "Result", MessageBoxButton.OK, MessageBoxImage.Asterisk); });
            }
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (CopyWorker != null && CopyWorker.IsBusy)
            {
                canceled = true;
                CopyWorker.CancelAsync();
                e.Cancel = true;
            }
            else
            {
                if (_interactor.Destination != null)
                    _interactor.Destination.HistoryStoreDB.Dispose();
                if (_interactor.Source != null)
                    _interactor.Source.Refresh();
            }

        }



        private void templateHelpButton_Click(object sender, RoutedEventArgs e)
        {
            HelpDialog.ShowHelp("export");
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
            if (DestinationBox.Text != "")
                dlg.SelectedPath = DestinationBox.Text;
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DestinationBox.Text = dlg.SelectedPath;
            }
            CopyButton.IsEnabled = true;
        }

        private void DestinationBox_Selected(object sender, RoutedEventArgs e)
        {
            CopyButton.IsEnabled = true;
        }
    }
}
