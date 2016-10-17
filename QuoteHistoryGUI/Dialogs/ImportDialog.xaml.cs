using LevelDB;
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

namespace QuoteHistoryGUI.Dialogs
{
    /// <summary>
    /// Interaction logic for ImportDialog.xaml
    /// </summary>
    public partial class ImportDialog : Window
    {
        DB Source;
        DB Destination;
        HistoryInteractor Interactor;
        BackgroundWorker Worker;
        bool Replace;

        public ImportDialog()
        {
            InitializeComponent();
        }
        public ImportDialog(StorageInstance Destination = null, StorageInstance Source = null)
        {
            
            Worker = new BackgroundWorker();
            Interactor = new HistoryInteractor();
            InitializeComponent();
            
            Interactor.Destination = Destination;
            Interactor.Source = Source;
            if (Destination != null) {
                DestinationPath.Text = Destination.StoragePath;
                DestinationBut.IsEnabled = false;
            }
            if (Source != null)
            {
                SourcePath.Text = Source.StoragePath;
                SourceBut.IsEnabled = false;
            }
        }

        private void SourceBut_Click(object sender, RoutedEventArgs e)
        {

        }

        private void DestinationBut_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ImportBtn_Click(object sender, RoutedEventArgs e)
        {
            ImportBtn.IsEnabled = false;
            if(Interactor.Source!=null && Interactor.Destination != null)
            {
                
                Replace = (bool)ReplaceBox.IsChecked;
                Worker.DoWork += ImportWork;
                Worker.WorkerReportsProgress = true;
                Worker.WorkerSupportsCancellation = true;
                Worker.ProgressChanged += ImportProgressChanged;
                Worker.RunWorkerCompleted+= ImportCompleted;
                Worker.RunWorkerAsync();

            }
            else
            {
                MessageBox.Show("Choose Source and Destination!", "Import unable", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportWork(object sender, DoWorkEventArgs e)
        {
            
            Interactor.Import(Replace, Worker);
        }

        private void ImportCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.Close();
        }

        private void ImportProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            var key = e.UserState as byte[];
            var entry = HistoryDatabaseFuncs.DeserealizeKey(key);
            ReportBlock.Text = entry.Symbol;
        }
    }
}
