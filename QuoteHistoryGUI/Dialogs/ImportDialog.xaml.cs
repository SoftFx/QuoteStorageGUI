﻿using LevelDB;
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

namespace QuoteHistoryGUI.Dialogs
{
    /// <summary>
    /// Interaction logic for ImportDialog.xaml
    /// </summary>
    public partial class ImportDialog : Window
    {

        HistoryInteractor Interactor;
        BackgroundWorker Worker;
        bool Replace;
        bool isUIVersion = true;
        public static readonly ILog log = LogManager.GetLogger(typeof(StorageSelectionDialog));
        public ImportDialog(StorageInstanceModel Destination = null, StorageInstanceModel Source = null)
        {
            try
            {
                log.Info("Import dialog initializing...");
                Worker = new BackgroundWorker();
                Interactor = new HistoryInteractor();
                InitializeComponent();

                Interactor.Destination = Destination;
                Interactor.Source = Source;
                if (Destination != null)
                {
                    DestinationPath.Text = Destination.StoragePath;
                    DestinationBut.IsEnabled = false;
                }
                if (Source != null)
                {
                    SourcePath.Text = Source.StoragePath;
                    SourceBut.IsEnabled = false;
                }

                if (Interactor.Source != null && Interactor.Destination != null)
                    ImportBtn.IsEnabled = true;
                else ImportBtn.IsEnabled = false;



                log.Info("Import dialog initialized");
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                throw ex;
            }

        }

        private void SourceBut_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            { };

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SourcePath.Text = dlg.SelectedPath;
                Interactor.Source = new StorageInstanceModel(dlg.SelectedPath, Owner.Dispatcher, Interactor);
            }
            if (Interactor.Source != null && Interactor.Source.Status != "Ok")
            {
                MessageBox.Show(Interactor.Source.Status, "Open Error", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
            if (Interactor.Source != null && Interactor.Destination != null)
                ImportBtn.IsEnabled = true;
            else ImportBtn.IsEnabled = false;
        }

        private void DestinationBut_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            { };

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DestinationPath.Text = dlg.SelectedPath;
                Interactor.Destination = new StorageInstanceModel(dlg.SelectedPath, Owner.Dispatcher, Interactor);
            }
            if (Interactor.Source != null && Interactor.Destination.Status != "Ok")
            {
                MessageBox.Show(Interactor.Destination.Status, "Open Error", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
            if (Interactor.Source != null && Interactor.Destination != null)
                ImportBtn.IsEnabled = true;
            else ImportBtn.IsEnabled = false;
        }

        private void ImportBtn_Click(object sender, RoutedEventArgs e)
        {
            DoImport();
        }

        public void DoImport(bool isUiVersion = true)
        {
            try
            {
                log.Info("Import calling...");
                

                if (Interactor.Destination.openMode == StorageInstanceModel.OpenMode.ReadOnly)
                {
                    throw (new Exception("Unable to import to storage opened in readonly mode"));
                }
                ReportBlock.Text = "Import starting...";
                ImportBtn.IsEnabled = false;

                if (Interactor.Source != null && Interactor.Destination != null)
                {
                    log.Info("Import source: " + Interactor.Source.StoragePath);
                    log.Info("Import destination: " + Interactor.Destination.StoragePath);
                    this.isUIVersion = isUiVersion;
                    Replace = (bool)ReplaceBox.IsChecked;
                    Worker.DoWork += ImportWork;
                    Worker.WorkerReportsProgress = true;
                    Worker.WorkerSupportsCancellation = true;
                    Worker.ProgressChanged += ImportProgressChanged;
                    Worker.RunWorkerCompleted += ImportCompleted;
                    Worker.RunWorkerCompleted += QHAppWindowModel.throwExceptions;
                    Worker.RunWorkerAsync();
                }
                else
                {
                    throw (new Exception("Source/Destination not specified"));
                }
            }
            catch (Exception e)
            {
                if (isUiVersion)
                    MessageBox.Show("Check pathes and close storages!\n\nError: "+e.Message, "Import", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                else Console.Out.WriteLine(e.Message);
                log.Warn(e.Message);
            }

        }

        private void ImportWork(object sender, DoWorkEventArgs e)
        {
            if (!isUIVersion) Console.Out.WriteLine("Import Started");
            if (!isUIVersion) Console.Out.WriteLine("...");
            Interactor.Import(Replace, Worker);
            if (isUIVersion) Interactor.Destination.Refresh();
        }

        private void ImportCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            ReportBlock.Text = "\nImport Completed";
            if (isUIVersion)
                MessageBox.Show("Import Completed", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
            else Console.Out.WriteLine("\n\nImport Completed!");
            log.Info("Import performed...");
            this.Close();
        }

        private void ImportProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            var message = e.UserState as string;
            ReportBlock.Text = message;
            
            if (!isUIVersion) {
                Console.Write("\r{0}", new string(' ', Console.BufferWidth - Console.CursorLeft));
                Console.Write("\r{0}", ReportBlock.Text); }
            log.Info("Import progresss report: " + message);
            
        }
    }
}
