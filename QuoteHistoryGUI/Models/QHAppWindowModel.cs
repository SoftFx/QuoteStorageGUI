﻿using LevelDB;
using log4net;
using QuoteHistoryGUI.Dialogs;
using QuoteHistoryGUI.HistoryTools;
using QuoteHistoryGUI.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace QuoteHistoryGUI.Models
{
    public class QHAppWindowModel : INotifyPropertyChanged
    {

        #region INotifyPropertyChanged
        public static void throwExceptions(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                log.Error(e.Error.Message);
                throw new Exception(e.Error.Message + ";\t\nStack trace: " + e.Error.StackTrace);
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public Dispatcher Dispatcher;

        private string _version;
        public string Version
        {
            get { return _version; }
            set
            {
                if (_version == value)
                    return;
                _version = value;
                NotifyPropertyChanged("Version");
            }
        }

        protected void NotifyPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        private ObservableCollection<StorageInstanceModel> _storageTabs;
        public ObservableCollection<StorageInstanceModel> StorageTabs
        {
            get { return _storageTabs; }
            set
            {
                if (_storageTabs == value)
                    return;
                _storageTabs = value;
                NotifyPropertyChanged("StorageTabs");

            }
        }

        private bool isOpenedStorage;
        public bool IsOpenedStorage
        {
            get { return isOpenedStorage; }
            set { isOpenedStorage = value; NotifyPropertyChanged("IsOpenedStorage"); }
        }

        public int SelMasterIndex
        {
            get { return MasterStorage.Count != 0 ? 0 : -1; }
            set { }
        }
        public int SelSlaveIndex
        {
            get { return SlaveStorage.Count != 0 ? 0 : -1; }
            set { }
        }

        string lastSelected;
        public string LastSelected
        {
            get { return lastSelected; }
            set { lastSelected = value; NotifyPropertyChanged("LastSelected"); }
        }


        public void TryToAddStorage(StorageInstanceModel st)
        {
            if (StorageTabs.Count > 1)
            {
                MessageBox.Show("Can't open more than 2 storages!", "Hmm...", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.None);
            }
            else
            {
                StorageTabs.Add(st);
                if (StorageTabs.Count == 1)
                    MasterStorage.Add(st);
                if (StorageTabs.Count == 2)
                    SlaveStorage.Add(st);
            }
            IsOpenedStorage = true;

            NotifyPropertyChanged("SelMasterIndex");
            NotifyPropertyChanged("SelSlaveIndex");
        }

        public void TryToRemoveStorage(StorageInstanceModel st)
        {
            StorageTabs.Remove(st);
            MasterStorage.Remove(st);
            SlaveStorage.Remove(st);

            if (SlaveStorage.Count == 1 && MasterStorage.Count == 0)
            {
                MasterStorage.Add(SlaveStorage[0]);
                SlaveStorage.Clear();
            }

            IsOpenedStorage = false;

            NotifyPropertyChanged("SelMasterIndex");
            NotifyPropertyChanged("SelSlaveIndex");
        }

        private ObservableCollection<StorageInstanceModel> _masterStorage;
        public ObservableCollection<StorageInstanceModel> MasterStorage
        {
            get { return _masterStorage; }
            set
            {
                if (_masterStorage == value)
                    return;
                _masterStorage = value;
                NotifyPropertyChanged("MasterStorage");
            }
        }

        private ObservableCollection<StorageInstanceModel> _slaveStorage;
        public ObservableCollection<StorageInstanceModel> SlaveStorage
        {
            get { return _slaveStorage; }
            set
            {
                if (_slaveStorage == value)
                    return;
                _slaveStorage = value;
                NotifyPropertyChanged("SlaveStorage");
            }
        }
        public static readonly ILog log = LogManager.GetLogger(typeof(QHAppWindowModel));

        public HistoryInteractor Interactor;
        public QHAppWindowView MainWindow;
        public QHAppWindowModel(Dispatcher dispatcher)
        {

            
            Interactor = new HistoryInteractor(dispatcher);
            OpenBtnClick = new SingleDelegateCommand(OpenBaseDelegate);
            ImportBtnClick = new SingleDelegateCommand(ImportDelegate);
            UpdateBtnClick = new SingleDelegateCommand(UpdateDelegate);
            CreateBtnClick = new SingleDelegateCommand(CreateDelegate);
            ExportBtnClick = new SingleDelegateCommand(ExportDelegate);
            AboutBtnClick = new SingleDelegateCommand(AboutDelegate);
            StorageTabs = new ObservableCollection<StorageInstanceModel>();
            MasterStorage = new ObservableCollection<StorageInstanceModel>();
            SlaveStorage = new ObservableCollection<StorageInstanceModel>();
            CopyContextBtnClick = new SingleDelegateCommand(CopyContextDelegate);
            CompactBtnClick = new SingleDelegateCommand(CompactDelegate);
            Dispatcher = dispatcher;
            IsOpenedStorage = false;
            try
            {
                StreamReader r = File.OpenText("version.txt");
                Version = "QuoteStorageGUI build: " + r.ReadLine();
            }
            catch
            {
                Version = "QuoteStorageGUI";
            }
            log.Info("QH GUI initialized");
        }

        public ICommand OpenBtnClick { get; private set; }
        public ICommand ImportBtnClick { get; private set; }
        public ICommand CreateBtnClick { get; private set; }
        public ICommand CopyContextBtnClick { get; private set; }
        public ICommand UpdateBtnClick { get; private set; }
        public ICommand ExportBtnClick { get; private set; }
        public ICommand AboutBtnClick { get; private set; }
        public ICommand CompactBtnClick { get; private set; }
        private bool OpenBaseDelegate(object o, bool isCheckOnly)
        {

            if (isCheckOnly)
                return true;
            else
            {
                try
                {
                    log.Info("Open storage");
                    var dlg = new StorageSelectionDialog()
                    {
                        Owner = Application.Current.MainWindow
                    };
                    dlg.ShowDialog();

                    var path = dlg.StoragePath.Text;
                    StorageInstanceModel.OpenMode mode = /*(bool)dlg.ReadOnlyBox.IsChecked ? StorageInstanceModel.OpenMode.ReadOnly : */StorageInstanceModel.OpenMode.ReadWrite;
                    StorageInstanceModel tab = null;
                    if(path!="")
                    Task.Run(() =>
                    {
                        try
                        {
                            if (MasterStorage.Count > 0)
                            {
                                var storageInstance = MasterStorage[0];
                                var code = storageInstance.Loader.TryDisposeLoader();
                                if (code != 0)
                                {
                                    MessageBox.Show("Unable to close: " + storageInstance.FilePath + ". Storage is loading!", "Close", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                                    log.Warn("Storage is loading. Unable to close.");
                                    return;
                                }
                            }
                            tab = new StorageInstanceModel(path, this.Dispatcher, this.Interactor, mode);

                            this.Dispatcher.BeginInvoke(new Action(() => { MainWindow.IsEnabled = true; MainWindow.HideLoading(); }), DispatcherPriority.ContextIdle, null);
                            if (tab != null && tab.Status == "Ok")
                            {
                                if (MasterStorage.Count > 0)
                                {
                                    var storageInstance = MasterStorage[0];


                                    this.Dispatcher.Invoke(delegate { TryToRemoveStorage(storageInstance); });
                                    if (storageInstance.HistoryStoreDB != null)
                                        storageInstance.HistoryStoreDB.Dispose();
                                    log.Info("Previous storage removed");
                                }
                                this.Dispatcher.Invoke(delegate
                                {
                                    this.TryToAddStorage(tab);
                                });
                            }
                            else {
                                if (MasterStorage.Count > 0)
                                {
                                    var storageInstance = MasterStorage[0];
                                    storageInstance.Loader.RestoreLoader();
                                }
                                    this.Dispatcher.Invoke(delegate { MessageBox.Show(MainWindow, "Can't open storage\n\nMessage: " + tab.Status, "Hmm...", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.None); });
                            }
                        }
                        catch(Exception ex)
                        {
                            this.Dispatcher.Invoke(delegate { MessageBox.Show("Can't open storage\n\n" + ex, "Hmm...", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.None); });
                            log.Error(ex.Message);
                        }
                    });

                }
                catch (Exception ex)
                {
                    MainWindow.IsEnabled = true;
                    this.Dispatcher.BeginInvoke(new Action(() => { MainWindow.HideLoading(); }), DispatcherPriority.ContextIdle, null);
                    log.Error(ex.Message);
                    throw ex;
                }
                return true;
            }
        }

        private bool ImportDelegate(object o, bool isCheckOnly)
        {

            if (isCheckOnly)
                return true;
            else
            {
                try
                {
                    log.Info("Import Dialog calling");
                    if (MasterStorage.Count == 0)
                    {
                        var dlg = new ImportDialog(MasterStorage.Count > 0 ? MasterStorage[0] : null, SlaveStorage.Count > 0 ? SlaveStorage[0] : null)
                        {
                            Owner = Application.Current.MainWindow
                        };
                        dlg.ShowDialog();
                    }
                    else
                    {
                        var dlg = new SmartImportDialog(MasterStorage[0], StorageTabs, this.Interactor)
                        {
                            Owner = Application.Current.MainWindow
                        };
                        dlg.ShowDialog();
                    }
                    log.Info("Import Dialog closed");
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                    throw ex;
                }
                return true;
            }
        }

        private bool ExportDelegate(object o, bool isCheckOnly)
        {

            if (isCheckOnly)
                return true;
            else
            {
                try
                {
                    log.Info("Export Dialog calling");
                    var dlg = new ExportDialog(MasterStorage[0], this.Interactor)
                    {
                        Owner = Application.Current.MainWindow
                    };
                    dlg.ShowDialog();
                    log.Info("Export Dialog closed");
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                    throw ex;
                }
                return true;
            }
        }

        private bool UpdateDelegate(object o, bool isCheckOnly)
        {
            if (isCheckOnly)
                return true;
            else
            {
                try
                {
                    log.Info("Upstream Dialog closed");

                    Interactor.DiscardSelection();
                    MasterStorage[0].Selection.ForEach(t => { Interactor.AddToSelection(t); });

                    var dlg = new UpstreamDialog(MasterStorage.Count > 0 ? MasterStorage[0] : null, Interactor)
                    {
                        Owner = Application.Current.MainWindow
                    };
                    dlg.ShowDialog();
                    log.Info("Upstream Dialog closed");
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                    throw ex;
                }
                return true;
            }
        }
        private bool CreateDelegate(object o, bool isCheckOnly)
        {

            if (isCheckOnly)
                return true;
            else
            {
                try
                {
                    var dlg = new System.Windows.Forms.FolderBrowserDialog
                    { };

                    if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        var path = dlg.SelectedPath;
                        Directory.CreateDirectory(path + "\\HistoryDB");
                        var historyStoreDB = new DB(path + "\\HistoryDB",
                                new Options() { BloomFilter = new BloomFilterPolicy(10), CreateIfMissing = true });
                        historyStoreDB.Dispose();
                        var tab = new StorageInstanceModel(path, this.Dispatcher, Interactor, StorageInstanceModel.OpenMode.ReadWrite);
                        if (tab.Status == "Ok")
                            TryToAddStorage(tab);
                        else MessageBox.Show("Can't open storage\n\nMessage: " + tab.Status, "Hmm...", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.None);

                    }

                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                    throw ex;
                }
                return true;
            }
        }

        private bool CopyContextDelegate(object o, bool isCheckOnly)
        {

            if (isCheckOnly)
                return true;
            else
            {

                var dlg = new CopyDialog()
                {
                    Owner = Application.Current.MainWindow
                };
                dlg.ShowDialog();
                return true;
            }
        }

        private bool CompactDelegate(object o, bool isCheckOnly)
        {

            if (isCheckOnly)
                return true;
            else
            {

                var dlg = new CompactDialog(Dispatcher)
                {
                    Owner = Application.Current.MainWindow
                };
                dlg.ShowDialog();
                return true;
            }
        }

        private bool AboutDelegate(object o, bool isCheckOnly)
        {
            if (isCheckOnly)
                return true;

            HelpDialog.ShowHelp();
            return false;
        }
    }
}
