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
        bool IsCopying = false;
        StorageInstanceModel _source;
        StorageInstanceModel _destination;
        SelectTemplateWorker temW;
        string templateText;
        bool isMove = false;
        public CopyDialog(StorageInstanceModel source, ObservableCollection<StorageInstanceModel> tabs, HistoryInteractor interactor)
        {
            InitializeComponent();
            Source.Text = source.StoragePath;
            foreach(var tab in tabs)
            {
                if (tab != source) _destination = tab; 
            }
            Destination.Text = _destination.StoragePath;
            var win = Application.Current.MainWindow as QHAppWindowView;
            
            _interactor = interactor;
            
            //TemplateBox.TemplateBox.Text = GetTemplates(interactor.Selection);
            TemplateBox.SetData(source.Folders.Select(f => f.Name), Enumerable.Range(2010, DateTime.Today.Year - 2009).Select(y => y.ToString()),
                GetTemplates(interactor.Selection));
            _interactor.Selection.Clear();
            _tabs = tabs;
            _source = source;
        }

        public CopyDialog()
        {
            InitializeComponent();
        }

        //string GetTemplates(IEnumerable<Folder> selection)
        //{
        //    string res = "";
        //    foreach(var sel in selection)
        //    {
        //        string path = "";
        //        var curSel = sel;
        //        while (curSel != null)
        //        {
        //            path = curSel.Name + "/" + path;
        //            curSel = curSel.Parent;
        //        }
        //        res += path.Substring(0, path.Length - 1);
        //        res += ";\n";
        //    }
        //    return res;
        //}

        IEnumerable<string> GetTemplates(IEnumerable<Folder> selection)
        {
            List<string> result = new List<string>();
            foreach (var sel in selection)
            {
                string res = "";
                string path = "";
                var curSel = sel;
                while (curSel != null)
                {
                    path = curSel.Name + "/" + path;
                    curSel = curSel.Parent;
                }
                res += path.Substring(0, path.Length - 1);
                result.Add(res);
            }
            return result;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            isMove = OperationTypeBox.SelectedIndex == 1;
            CopyWorker = new BackgroundWorker();
            _interactor.Source = _source;
            _interactor.Destination = _destination;

            if (_interactor.Destination.openMode == StorageInstanceModel.OpenMode.ReadOnly)
            {
                MessageBox.Show("Unable to modify storage opened in readonly mode", "Copy", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            temW = new SelectTemplateWorker(_interactor.Source.Folders, new HistoryLoader(Application.Current.MainWindow.Dispatcher, _interactor.Source.HistoryStoreDB));
            templateText = string.Join(";\n", TemplateBox.Templates.Source.Select(t => t.Value));

            IsCopying = true;
            CopyButton.IsEnabled = false;
            CopyWorker.WorkerReportsProgress=true;
            CopyWorker.WorkerSupportsCancellation = true;
            CopyWorker.DoWork += worker_Copy;
            CopyWorker.ProgressChanged += CopyProgressChanged;
            CopyWorker.RunWorkerCompleted += worker_Copied;
            CopyWorker.RunWorkerAsync(CopyWorker);
        }

        private void worker_Copy(object sender, DoWorkEventArgs e)
        {
            var templates = templateText.Split(new char[] { ';', ',', '\n', '\r' });
            var matched = new List<Folder>();
            BackgroundWorker worker = e.Argument as BackgroundWorker;
            foreach (var templ in templates)
                if (templ != "")
                    matched.AddRange(temW.GetByMatch(templ, worker));
            foreach (var match in matched)
            {
                _interactor.AddToSelection(match);
            }

            
            _interactor.Copy(worker);
            if (isMove)
            {
                _interactor.Dispatcher = Dispatcher;
                _interactor.Delete();
                _interactor.Dispatcher = null;
            }
            _interactor.Destination.Refresh();
        }
        private void CopyProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            CopyStatusBlock.Text = e.UserState as string;
        }
        private void worker_Copied(object sender, RunWorkerCompletedEventArgs e)
        {
            IsCopying = false;
            MessageBox.Show("Done!","Result",MessageBoxButton.OK,MessageBoxImage.Asterisk);
            Close();
            CopyButton.IsEnabled = true;
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (CopyWorker != null && CopyWorker.IsBusy)
            {
                CopyWorker.CancelAsync();
                MessageBox.Show("Canceled!", "Closing message", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                _interactor.Destination.Refresh();
            }
        }

        private void templateHelpButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Examples:\n\nAAABBB/2015/1/2/3;\nAAA*/*15/;\n*/*/*/1/M1*;\n*/2016/*/2*/*3/ticks file*;", "Template Help",MessageBoxButton.OK,MessageBoxImage.Question);
        }
    }
}
