using QuoteHistoryGUI.HistoryTools;
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
        ObservableCollection<StorageInstance> _tabs;
        BackgroundWorker CopyWorker;
        bool IsCopying = false;
        StorageInstance _source;
        SelectTemplateWorker temW;
        string templateText;
        public CopyDialog(StorageInstance source, ObservableCollection<StorageInstance> tabs, HistoryInteractor interactor)
        {
            InitializeComponent();
            Source.Text = source.StoragePath;
            Destination.ItemsSource = tabs.Select(t => t.StoragePath);
            var win = Application.Current.MainWindow as MainWindowView;
            var leftStorage = win.left_control.SelectedContent as StorageInstance;
            var rightStorage = win.right_control.SelectedContent as StorageInstance;

            if (source == leftStorage)
            {
                Destination.SelectedIndex = tabs.IndexOf(rightStorage);
            }
            else Destination.SelectedIndex = tabs.IndexOf(leftStorage);
            
            _interactor = interactor;
            
            TemplatesBox.Text = GetTemplates(interactor.Selection);
            _interactor.Selection.Clear();
            _tabs = tabs;
            _source = source;
        }

        public CopyDialog()
        {
            InitializeComponent();
        }
        string GetTemplates(IEnumerable<Folder> selection)
        {
            string res = "";
            foreach(var sel in selection)
            {
                string path = "";
                var curSel = sel;
                while (curSel != null)
                {
                    path = curSel.Name + "/" + path;
                    curSel = curSel.Parent;
                }
                res += path.Substring(0, path.Length - 1);
                res += ";\n";
            }
            return res;
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            CopyWorker = new BackgroundWorker();
            _interactor.Source = _source;
            _interactor.Destination = _tabs[Destination.SelectedIndex];


            temW = new SelectTemplateWorker(_interactor.Source.Folders, new HistoryLoader(Application.Current.MainWindow.Dispatcher, _interactor.Source.HistoryStoreDB));
            templateText = TemplatesBox.Text;

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
            _interactor.Destination.Refresh();
        }
        private void CopyProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            CopyStatusBlock.Text = e.UserState as string;
        }
        private void worker_Copied(object sender, RunWorkerCompletedEventArgs e)
        {
            IsCopying = false;
            MessageBox.Show("Copied!","Copy Result",MessageBoxButton.OK,MessageBoxImage.Asterisk);
            CopyButton.IsEnabled = true;
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (CopyWorker != null && CopyWorker.IsBusy)
            {
                CopyWorker.CancelAsync();
                MessageBox.Show("Copying canceled!", "Closing message", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                _interactor.Destination.Refresh();
            }
        }

        private void templateHelpButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Examples:\n\nAAABBB/2015/1/2/3;\nAAA*/*15/;\n*/*/*/1/M1*;\n*/2016/*/2*/*3/ticks file*;", "Template Help",MessageBoxButton.OK,MessageBoxImage.Question);
        }
    }
}
