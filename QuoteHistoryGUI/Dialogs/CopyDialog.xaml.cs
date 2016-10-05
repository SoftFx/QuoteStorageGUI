using QuoteHistoryGUI.HistoryTools;
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
    /// Interaction logic for CopyDialog.xaml
    /// </summary>
    public partial class CopyDialog : Window
    {
        HistoryInteractor _interactor;
        ObservableCollection<StorageInstance> _tabs;
        BackgroundWorker CopyWorker;
        bool IsCopying = false;

        public CopyDialog(StorageInstance source, ObservableCollection<StorageInstance> tabs, HistoryInteractor interactor)
        {
            InitializeComponent();
            Source.ItemsSource = tabs.Select(t=>t.StoragePath);
            Source.SelectedIndex = tabs.IndexOf(source) ;
            Destination.ItemsSource = tabs.Select(t => t.StoragePath);
            Destination.SelectedIndex = 0;
            _interactor = interactor;
            _tabs = tabs;
        }

        public CopyDialog()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            CopyWorker = new BackgroundWorker();
            _interactor.Source = _tabs[Source.SelectedIndex];
            _interactor.Destination = _tabs[Destination.SelectedIndex];
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
            BackgroundWorker worker = e.Argument as BackgroundWorker;
            _interactor.Copy(worker);
            _interactor.Destination.Refresh();
        }
        private void CopyProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            switch (CopyStatusBlock.Text)
            {
                case "Copying":
                    CopyStatusBlock.Text = "Copying.";
                    break;
                case "Copying.":
                    CopyStatusBlock.Text = "Copying..";
                    break;
                case "Copying..":
                    CopyStatusBlock.Text = "Copying...";
                    break;
                default:
                    CopyStatusBlock.Text = "Copying";
                    break;
            }
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
    }
}
