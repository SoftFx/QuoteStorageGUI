﻿using log4net;
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
        StorageInstanceModel _source;
        StorageInstanceModel _destination;
        SelectTemplateWorker temW;
        string destinationStr;
        string templateText;
        string mappingText;
        int formatType = 0;
        bool isMove = false;
        bool canceled = false;
        public static readonly ILog log = LogManager.GetLogger(typeof(StorageSelectionDialog));
        Dispatcher _dispatcher;
        List<string> periods = null;
        public ExportDialog(StorageInstanceModel source, HistoryInteractor interactor)
        {
            try
            {
                log.Info("Export dialog initializing...");
                InitializeComponent();

                _dispatcher = interactor.Dispatcher;

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
                formatType = FormatBox.SelectedIndex;
                log.Info("Export calling...");
                AppConfigManager.SavePathes(DestinationBox.Text);

                destinationStr = DestinationBox.Text;

                if (_source.FilePath == DestinationBox.Text)
                {
                    MessageBox.Show(this, "Unavailable to export to the same storage ", "Import", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    log.Info("Export canceled");
                    return;
                }

                if (TemplateRadioButton.IsChecked.Value == true)
                {
                    isMove = OperationTypeBox.SelectedIndex == 1;
                    CopyWorker = new BackgroundWorker();
                    _interactor.Source = _source;
                    if (formatType == 0 || formatType == 2)
                    {
                        if (!Directory.Exists(DestinationBox.Text + "\\HistoryDB"))
                            Directory.CreateDirectory(DestinationBox.Text + "\\HistoryDB");

                        _destination = new StorageInstanceModel(DestinationBox.Text, _dispatcher, _interactor, StorageInstanceModel.OpenMode.ReadWrite, StorageInstanceModel.LoadingMode.None);
                        _interactor.Destination = _destination;



                        if (_interactor.Destination.openMode == StorageInstanceModel.OpenMode.ReadOnly)
                        {
                            MessageBox.Show(this, "Unable to modify storage opened in readonly mode", "Copy", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                            return;
                        }
                    }
                    temW = new SelectTemplateWorker(_interactor.Source.Folders, new HistoryLoader(Application.Current.MainWindow.Dispatcher, _interactor.Source.HistoryStoreDB));
                    templateText = string.Join(";\n", TemplateBox.Templates.Source.Select(t => t.Value));
                    mappingText = string.Join(";\n", TemplateBox.Mapping.Source.Select(t => t.Value));
                    switch (FileTypeBox.SelectedIndex)
                    {
                        case 1:
                            periods = new List<string>() { "ticks level2" };
                            break;
                        case 2:
                            periods = new List<string>() { "ticks" };
                            break;
                        case 3:
                            periods = new List<string>() { "M1 ask", "M1 bid" };
                            break;
                        case 4:
                            periods = new List<string>() { "H1 ask", "H1 bid" };
                            break;
                        default: break;
                    }


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
                    _interactor.Source = _source;


                    if (formatType == 0 || formatType == 2)
                    {
                        if (!Directory.Exists(DestinationBox.Text + "\\HistoryDB"))
                            Directory.CreateDirectory(DestinationBox.Text + "\\HistoryDB");
                        _destination = new StorageInstanceModel(DestinationBox.Text, _dispatcher, _interactor, StorageInstanceModel.OpenMode.ReadWrite, StorageInstanceModel.LoadingMode.None);
                        _interactor.Destination = _destination;
                        if (_interactor.Destination.openMode == StorageInstanceModel.OpenMode.ReadOnly)
                            _destination = new StorageInstanceModel(DestinationBox.Text, _interactor.Dispatcher);

                       
                        _interactor.Destination = _destination;

                        if (_interactor.Destination.openMode == StorageInstanceModel.OpenMode.ReadOnly)
                        {
                            MessageBox.Show("Unable to modify storage opened in readonly mode", "Copy", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                            return;
                        }
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
            var templates = templateText.Split(new[] { ";\n" }, StringSplitOptions.None);
            IEnumerable<KeyValuePair<string, string>> mapping = null;
            if(mappingText!=null&& mappingText!="")
                mapping = mappingText.Split(new[] { ";\n" }, StringSplitOptions.None).Select(t=>new KeyValuePair<string, string>(t.Split(' ')[0], t.Split(' ')[2]));
            if (formatType == 0)
            {
                foreach (var templ in templates)
                {
                    worker.ReportProgress(1, "Template: " + templ);
                    var matched = temW.GetByMatch(templ, worker);
                    _interactor.Copy(worker, matched, periods, (message) =>
                    {
                        worker.ReportProgress(1, message);
                    }, mapping);
                    if (isMove)
                    {
                        _interactor.Dispatcher = Dispatcher;
                        _interactor.Delete(matched, worker, true);
                        _interactor.Dispatcher = null;
                    }
                }
            }
            if (formatType == 1)
            {
                foreach (var templ in templates)
                {
                    worker.ReportProgress(1, "Template: " + templ);
                    var matched = temW.GetByMatch(templ, worker);
                    _interactor.NtfsExport(worker, matched, destinationStr, periods, (message) =>
                    {
                        worker.ReportProgress(1, message);
                    });
                    if (isMove)
                    {
                        _interactor.Dispatcher = Dispatcher;
                        _interactor.Delete(matched, worker, true);
                        _interactor.Dispatcher = null;
                    }
                }
            }
            if (formatType == 2)
            {
                foreach (var templ in templates)
                {
                    worker.ReportProgress(1, "Template: " + templ);
                    var matched = temW.GetByMatch(templ, worker);
                    _interactor.BinaryExport(worker, matched, destinationStr, periods, (message) =>
                    {
                        worker.ReportProgress(1, message);
                    });
                    if (isMove)
                    {
                        _interactor.Dispatcher = Dispatcher;
                        _interactor.Delete(matched, worker, true);
                        _interactor.Dispatcher = null;
                    }
                }
            }

            _interactor.Source.Refresh();

        }

        private void worker_Export(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = e.Argument as BackgroundWorker;
            if (formatType == 0)
            {
                _interactor.Import(true, worker, (key, copiedCnt) =>
                {
                    var dbentry = HistoryDatabaseFuncs.DeserealizeKey(key);
                    worker.ReportProgress(1, "[" + copiedCnt + "] " + dbentry.Symbol + ": " + dbentry.Time + " - " + dbentry.Period);
                });
            }
            if (formatType == 1)
            {
                _interactor.ExportAllNtfs(true, destinationStr, worker, (key, copiedCnt) =>
                {
                    var dbentry = HistoryDatabaseFuncs.DeserealizeKey(key);
                    worker.ReportProgress(1, "[" + copiedCnt + "] " + dbentry.Symbol + ": " + dbentry.Time + " - " + dbentry.Period);
                });
            }
            if (formatType == 2)
            {
                _interactor.ExportAllBinary(true, worker, (key, copiedCnt) =>
                {
                    var dbentry = HistoryDatabaseFuncs.DeserealizeKey(key);
                    worker.ReportProgress(1, "[" + copiedCnt + "] " + dbentry.Symbol + ": " + dbentry.Time + " - " + dbentry.Period);
                });
            }
        }
        private void CopyProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            CopyStatusBlock.Text = e.UserState as string;
            log.Info("Export progress report: " + e.UserState as string);
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
                    if (_interactor.Destination != null)
                        _interactor.Destination.HistoryStoreDB.Dispose();
                    if (_interactor.Source != null)
                        _interactor.Source.Refresh();
                }
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
            FileTypeBox.IsEnabled = false;
        }

        private void TemplateRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (TemplateExpander != null)
                TemplateExpander.IsEnabled = true;
            OperationTypeBox.IsEnabled = true;
            FileTypeBox.IsEnabled = true;
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

        private void DestinationBox_TextChanged(object sender, RoutedEventArgs e)
        {
            CopyButton.IsEnabled = true;
        }

        private void OperationTypeBox_Selected(object sender, RoutedEventArgs e)
        {
            if (OperationTypeBox.SelectedIndex == 1)
                MessageBox.Show(this, "The move option will cause the execution of delete operations. It can lead to poor performance. It is recommended to do a compact operation after delete operations.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
