using log4net;
using QuoteHistoryGUI.HistoryTools;
using QuoteHistoryGUI.Models;
using System;
using System.Collections.Generic;
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
using System.Windows.Threading;

namespace QuoteHistoryGUI.Dialogs
{
    /// <summary>
    /// Interaction logic for DeleteProgressDialog.xaml
    /// </summary>
    public partial class DeleteProgressDialog : Window
    {
        public DeleteProgressDialog()
        {
            InitializeComponent();
        }

        BackgroundWorker DeleteWorker = new BackgroundWorker();
        HistoryInteractor _interactor;
        Dispatcher _dispatcher;
        public static readonly ILog log = LogManager.GetLogger(typeof(StorageSelectionDialog));
        bool canceled = false;
        public DeleteProgressDialog(HistoryInteractor interactor, List<Folder> selection, Dispatcher dispatcher)
        {
            InitializeComponent();

            _interactor = interactor;
            _dispatcher = dispatcher;
            if (selection != null)
            {
                _interactor.DiscardSelection();
                selection.ForEach(t => { _interactor.AddToSelection(t); });
                selection.Clear();
            }

            this.Closing += Window_Closing;

            DeleteWorker.WorkerReportsProgress = true;
            DeleteWorker.WorkerSupportsCancellation = true;
            DeleteWorker.DoWork += worker_Delete;
            DeleteWorker.ProgressChanged += DeleteProgressChanged;
            DeleteWorker.RunWorkerCompleted += worker_Deleted;
            DeleteWorker.RunWorkerCompleted += QHAppWindowModel.throwExceptions;
            DeleteWorker.RunWorkerAsync(DeleteWorker);
        }

        private void worker_Delete(object sender, DoWorkEventArgs e)
        {
            try
            {
                BackgroundWorker worker = e.Argument as BackgroundWorker;
                _interactor.Delete(null, worker);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ",\nStackTrace: " + ex.StackTrace, "Upstream error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
        }
        private void DeleteProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            DeleteProgressBlock.Text = e.UserState as string;
            log.Info("Delete progress report: " + e.UserState as string);
        }
        private void worker_Deleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (!canceled)
            {
                _dispatcher.Invoke(delegate
                { MessageBox.Show("Delete update completed", "Result", MessageBoxButton.OK, MessageBoxImage.Asterisk); });
                log.Info("Delete performed");
            }
            else
            {
                _dispatcher.Invoke(delegate
                { MessageBox.Show("Canceled!", "Result", MessageBoxButton.OK, MessageBoxImage.Asterisk); });
                log.Info("Delete canceled");
            }
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DeleteWorker?.IsBusy==true)
            {
                canceled = true;
                DeleteWorker.CancelAsync();
                e.Cancel = true;
            }
            else
            {
                if (_interactor.Source != null)
                    _interactor.Source.Refresh();
            }

        }



    }
}
