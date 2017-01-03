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
        public SmartImportDialog(StorageInstanceModel source, ObservableCollection<StorageInstanceModel> tabs, HistoryInteractor interactor)
        {

            InitializeComponent();

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
            _interactor.Selection.Clear();
            _tabs = tabs;
            _source = source;
        }

        public SmartImportDialog()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (TemplateRadioButton.IsChecked.Value == true)
            {
                isMove = OperationTypeBox.SelectedIndex == 1;
                CopyWorker = new BackgroundWorker();
                _interactor.Source = _source;

                if (!Directory.Exists(SourceBox.Text + "\\HistoryDB"))
                    Directory.CreateDirectory(SourceBox.Text + "\\HistoryDB");
                _destination = new StorageInstanceModel(SourceBox.Text, _interactor.Dispatcher);
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
                    MessageBox.Show("Unable to modify storage opened in readonly mode", "Copy", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }

                CopyButton.IsEnabled = false;
                CopyWorker.WorkerReportsProgress = true;
                CopyWorker.WorkerSupportsCancellation = true;
                CopyWorker.DoWork += worker_Export;
                CopyWorker.ProgressChanged += CopyProgressChanged;
                CopyWorker.RunWorkerCompleted += worker_Copied;
                CopyWorker.RunWorkerAsync(CopyWorker);
            }
        }

        private void worker_Copy(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = e.Argument as BackgroundWorker;
            var templates = templateText.Split(new char[] { ';', ',', '\n', '\r' });
            if (isMetaMatching)
            {
                var templList = new List<string>(templates);
                var matchEnum = temW.GetFromMetaByMatch(templList, _source, worker).ToArray();
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
            _interactor.Source.Refresh();
        }

        private void worker_Export(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = e.Argument as BackgroundWorker;
            _interactor.Import(true, worker);
        }
        private void CopyProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            CopyStatusBlock.Text = e.UserState as string;
        }
        private void worker_Copied(object sender, RunWorkerCompletedEventArgs e)
        {
            MessageBox.Show("Done!", "Result", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            Close();
            CopyButton.IsEnabled = true;
            _interactor.Source.HistoryStoreDB.Dispose();
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (CopyWorker != null && CopyWorker.IsBusy)
            {
                CopyWorker.CancelAsync();
                _interactor.Source.HistoryStoreDB.Dispose();
                MessageBox.Show("Canceled!", "Closing message", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                _interactor.Destination.Refresh();
            }
        }
        private void templateHelpButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Examples:\n\nAAABBB/2015/1/2/3;\nAAA*/*15/;\n*/*/*/1/M1*;\n*/2016/*/2*/*3/ticks file*;", "Template Help", MessageBoxButton.OK, MessageBoxImage.Question);
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
    }
}
