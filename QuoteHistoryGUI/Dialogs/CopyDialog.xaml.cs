﻿using QuoteHistoryGUI.HistoryTools;
using QuoteHistoryGUI.HistoryTools.Interactor;
using QuoteHistoryGUI.Models;
using QuoteHistoryGUI.Views;
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
    /// Interaction logic for CopyDialog.xaml
    /// </summary>
    public partial class CopyDialog : Window
    {
        HistoryInteractor _interactor;
        ObservableCollection<StorageInstanceModel> _tabs;
        BackgroundWorker CopyWorker;
        bool isMetaMatching = true;
        StorageInstanceModel _source;
        StorageInstanceModel _destination;
        SelectTemplateWorker temW;
        string templateText;
        bool isMove = false;
        public CopyDialog(StorageInstanceModel source, ObservableCollection<StorageInstanceModel> tabs, HistoryInteractor interactor)
        {

            InitializeComponent();
            this.MetaMatching.IsChecked = false;
            Source.Text = source.StoragePath;
            foreach (var tab in tabs)
            {
                if (tab != source) _destination = tab;
            }
            Destination.Text = _destination.StoragePath;
            var win = Application.Current.MainWindow as QHAppWindowView;

            _interactor = interactor;

            TemplateBox.SetData(source.Folders.Select(f => f.Name), Enumerable.Range(2010, DateTime.Today.Year - 2009).Select(y => y.ToString()),
                HistoryInteractor.GetTemplates(interactor.Selection));
            _interactor.Selection.Clear();
            _tabs = tabs;
            _source = source;
        }

        public CopyDialog()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            isMove = OperationTypeBox.SelectedIndex == 1;
            CopyWorker = new BackgroundWorker();
            _interactor.Source = _source;
            _interactor.Destination = _destination;

            if (_interactor.Destination.openMode == StorageInstanceModel.OpenMode.ReadOnly)
            {
                MessageBox.Show(this, "Unable to modify storage opened in readonly mode", "Copy", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            temW = new SelectTemplateWorker(_interactor.Source.Folders, new HistoryLoader(Application.Current.MainWindow.Dispatcher, _interactor.Source.HistoryStoreDB));
            templateText = string.Join(";\n", TemplateBox.Templates.Source.Select(t => t.Value));

            isMetaMatching = this.MetaMatching.IsChecked.HasValue && this.MetaMatching.IsChecked.Value;
            CopyButton.IsEnabled = false;
            CopyWorker.WorkerReportsProgress = true;
            CopyWorker.WorkerSupportsCancellation = true;
            CopyWorker.DoWork += worker_Copy;
            CopyWorker.ProgressChanged += CopyProgressChanged;
            CopyWorker.RunWorkerCompleted += worker_Copied;
            CopyWorker.RunWorkerCompleted += QHAppWindowModel.throwExceptions;
            CopyWorker.RunWorkerAsync(CopyWorker);
        }

        private void worker_Copy(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = e.Argument as BackgroundWorker;
            var templates = templateText.Split(new[] { ";\n" }, StringSplitOptions.None);
            //if (isMetaMatching)
            //{
            //    var templList = new List<string>(templates);
            //    var matchEnum = temW.GetFromMetaByMatch(templList, _source, worker).ToArray();
            //    _interactor.Copy(matchEnum, worker);
            //    _interactor.Copy(matchEnum, worker, true);
            //}
            //else
            //{

            foreach (var templ in templates)
            {
                worker.ReportProgress(1, "Template: " + templ);
                var matched = temW.GetByMatch(templ, worker).ToArray();

                _interactor.Copy(worker, matched, null, (message) =>
            {

                worker.ReportProgress(1, message);
            });
                if (isMove)
                {
                    _interactor.Dispatcher = Dispatcher;
                    _interactor.Delete(matched);
                    _interactor.Dispatcher = null;
                }
            }
            //}
            _interactor.Destination.Refresh();
        }
        private void CopyProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            CopyStatusBlock.Text = e.UserState as string;
        }
        private void worker_Copied(object sender, RunWorkerCompletedEventArgs e)
        {
            MessageBox.Show(this, "Done!", "Result", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            Close();
            CopyButton.IsEnabled = true;
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (CopyWorker != null && CopyWorker.IsBusy)
            {
                CopyWorker.CancelAsync();
                MessageBox.Show(this, "Canceled!", "Closing message", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                _interactor.Destination.Refresh();
            }
        }

        private void templateHelpButton_Click(object sender, RoutedEventArgs e)
        {
            HelpDialog.ShowHelp("copy_templates");
        }
    }
}
