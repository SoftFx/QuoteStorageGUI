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
    /// Interaction logic for RenameDialog.xaml
    /// </summary>
    public partial class RenameDialog : Window
    {
        public static readonly ILog log = LogManager.GetLogger(typeof(StorageSelectionDialog));

        BackgroundWorker RenameWorker;
        Dispatcher _dispatcher;
        bool canceled;
        StorageInstanceModel _model;
        string fromSym;
        string toSym;
        public RenameDialog(Dispatcher d, StorageInstanceModel model)
        {
            InitializeComponent();
            _dispatcher = d;
            this.Closing += Window_Closing;
            _model = model;
            log.Info("Rename dialog creating...");
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            fromSym = FromBox.Text;
            toSym = ToBox.Text;
            Rename_button.IsEnabled = false;
            RenameWorker = new BackgroundWorker();
            RenameWorker.WorkerSupportsCancellation = true;
            RenameWorker.WorkerReportsProgress = true;
            RenameWorker.DoWork += worker_Rename;
            RenameWorker.ProgressChanged += RenameProgressChanged;
            RenameWorker.RunWorkerCompleted += worker_Renamed;
            RenameWorker.RunWorkerAsync(RenameWorker);
        }

        private void worker_Rename(object sender, DoWorkEventArgs e)
        {
            DateTime ReportTime = DateTime.UtcNow.AddSeconds(-2);
            BackgroundWorker worker = e.Argument as BackgroundWorker;
            var db = _model.HistoryStoreDB;
            var startKey = ASCIIEncoding.ASCII.GetBytes(fromSym);
            var it = db.NewIterator(new LevelDB.ReadOptions());
            int renamedCnt = 0;
            it.Seek(startKey);
            while (it.Valid())
            {
                
                var entry = HistoryDatabaseFuncs.DeserealizeKey(it.Key().ToArray());
                if (worker != null && (DateTime.Now - ReportTime).Seconds > 0.25)
                {
                    worker.ReportProgress(1, "[" + renamedCnt + "] " + entry.Symbol + ": " + entry.Time + " - " + entry.Period);
                    ReportTime = DateTime.Now;
                }
                if (entry.Symbol != fromSym)
                    break;
                if (worker.CancellationPending)
                    break;
                var value = it.Value().ToArray();
                db.Delete(new LevelDB.WriteOptions(),it.Key());
                var newKey = HistoryDatabaseFuncs.SerealizeKey(toSym, entry.Type, entry.Period, entry.Time.Year, entry.Time.Month, entry.Time.Day, entry.Time.Hour,entry.Part, entry.FlushPart);
                db.Put(new LevelDB.WriteOptions(), newKey, value);
                renamedCnt++;
                it.Next();
            }
            worker.ReportProgress(1, "Renamed [" + renamedCnt + "] ");
        }

        private void RenameProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ReportBlock.Text = e.UserState as string;
            log.Info("Renaming: " + e.UserState as string);
        }

        private void worker_Renamed(object sender, RunWorkerCompletedEventArgs e)
        {
            if (canceled)
            {
                log.Info("Renaming canceled");
                _dispatcher.Invoke(delegate
                { MessageBox.Show(this, "Canceled!", "Close message", MessageBoxButton.OK, MessageBoxImage.Asterisk); });
            }
            else
            {
                log.Info("Renaming performed");
                _dispatcher.Invoke(delegate
                { MessageBox.Show(this, "Done!", "Result", MessageBoxButton.OK, MessageBoxImage.Asterisk); });
            }
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (RenameWorker != null)
            {
                if (RenameWorker.IsBusy)
                {
                    canceled = true;
                    RenameWorker.CancelAsync();
                    e.Cancel = true;
                }
                
                
            }

        }

       



    }
}
