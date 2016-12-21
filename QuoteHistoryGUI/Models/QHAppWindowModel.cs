using LevelDB;
using QuoteHistoryGUI.Dialogs;
using QuoteHistoryGUI.HistoryTools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

        public event PropertyChangedEventHandler PropertyChanged;

        private string _version;
        public string Version {
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
        public ObservableCollection<StorageInstanceModel> StorageTabs {
            get { return _storageTabs; }
            set
            {
                if (_storageTabs == value)
                    return;
                _storageTabs = value;
                NotifyPropertyChanged("StorageTabs");
                
            }
        }

        public int SelMasterIndex
        {
            get { return MasterStorage.Count!=0?0:-1; }
            set { }
        }
        public int SelSlaveIndex
        {
            get { return SlaveStorage.Count != 0 ? 0 : -1; }
            set { }
        }

        public string LastSelected
        {
            get { return "Last selected:"; }
            set { }
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
            NotifyPropertyChanged("SelMasterIndex");
            NotifyPropertyChanged("SelSlaveIndex");
        }

        public void TryToRemoveStorage(StorageInstanceModel st)
        {
            StorageTabs.Remove(st);
            MasterStorage.Remove(st);
            SlaveStorage.Remove(st);
            
            if(SlaveStorage.Count==1 && MasterStorage.Count == 0)
            {
                MasterStorage.Add(SlaveStorage[0]);
                SlaveStorage.Clear();
            }
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




        HistoryEditor _historyReader;
        public HistoryInteractor Interactor;
        public QHAppWindowModel() {
            Interactor = new HistoryInteractor();
            OpenBtnClick = new SingleDelegateCommand(OpenBaseDelegate);
            ImportBtnClick = new SingleDelegateCommand(ImportDelegate);
            UpdateBtnClick = new SingleDelegateCommand(UpdateDelegate);
            CreateBtnClick = new SingleDelegateCommand(CreateDelegate);
            StorageTabs = new ObservableCollection<StorageInstanceModel>();
            MasterStorage = new ObservableCollection<StorageInstanceModel>();
            SlaveStorage = new ObservableCollection<StorageInstanceModel>();
            CopyContextBtnClick = new SingleDelegateCommand(CopyContextDelegate);
            try {
                StreamReader r = File.OpenText("version.txt");
                Version = "QuoteStorageGUI build: " + r.ReadLine();
            }
            catch { }
            
        }

       



        public ICommand OpenBtnClick { get; private set; }
        public ICommand ImportBtnClick { get; private set; }
        public ICommand CreateBtnClick { get; private set; }
        public ICommand CopyContextBtnClick { get; private set; }
        public ICommand UpdateBtnClick { get; private set; }
        private bool OpenBaseDelegate(object o, bool isCheckOnly)
        {

            if (isCheckOnly)
                return true;
            else
            {
                var dlg = new StorageSelectionDialog()
                {
                    Owner = Application.Current.MainWindow
                };
                dlg.ShowDialog();
                if (dlg.StoragePath.Text != "")
                {
                    var tab = new StorageInstanceModel(dlg.StoragePath.Text, Interactor,(bool)dlg.ReadOnlyBox.IsChecked?StorageInstanceModel.OpenMode.ReadOnly:StorageInstanceModel.OpenMode.ReadWrite);
                    if (tab.Status == "Ok")
                        TryToAddStorage(tab);
                    else MessageBox.Show("Can't open storage\n\nMessage: " + tab.Status, "Hmm...", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.None);
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
                var dlg = new ImportDialog(MasterStorage.Count>0? MasterStorage[0]:null, SlaveStorage.Count > 0 ? SlaveStorage[0] : null)
                {
                    Owner = Application.Current.MainWindow
                };
                dlg.ShowDialog();
                return true;
            }
        }

        private bool UpdateDelegate(object o, bool isCheckOnly)
        {
            if (isCheckOnly)
                return true;
            else
            {
                Interactor.DiscardSelection();
                MasterStorage[0].Selection.ForEach(t => { Interactor.AddToSelection(t); });
                
                var dlg = new UpstreamDialog(MasterStorage.Count > 0 ? MasterStorage[0] : null, Interactor)
                {
                    Owner = Application.Current.MainWindow
                };
                dlg.ShowDialog();
                return true;
            }
        }
        private bool CreateDelegate(object o, bool isCheckOnly)
        {

            if (isCheckOnly)
                return true;
            else
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
                    var tab = new StorageInstanceModel(path, Interactor, StorageInstanceModel.OpenMode.ReadWrite);
                    if (tab.Status == "Ok")
                        TryToAddStorage(tab);
                    else MessageBox.Show("Can't open storage\n\nMessage: " + tab.Status, "Hmm...", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.None);

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

       
    }
}
