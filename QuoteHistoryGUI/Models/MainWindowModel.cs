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
    public class MainWindowModel : INotifyPropertyChanged
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

        private ObservableCollection<StorageInstance> _storageTabs;
        public ObservableCollection<StorageInstance> StorageTabs {
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


        public void TryToAddStorage(StorageInstance st)
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

        public void TryToRemoveStorage(StorageInstance st)
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

        private ObservableCollection<StorageInstance> _masterStorage;
        public ObservableCollection<StorageInstance> MasterStorage
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

        private ObservableCollection<StorageInstance> _slaveStorage;
        public ObservableCollection<StorageInstance> SlaveStorage
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
        public MainWindowModel() {
            Interactor = new HistoryInteractor();
            OpenBtnClick = new SingleDelegateCommand(OpenBaseDelegate);
            ImportBtnClick = new SingleDelegateCommand(ImportDelegate);
            StorageTabs = new ObservableCollection<StorageInstance>();
            MasterStorage = new ObservableCollection<StorageInstance>();
            SlaveStorage = new ObservableCollection<StorageInstance>();
            CopyContextBtnClick = new SingleDelegateCommand(CopyContextDelegate);
            try {
                StreamReader r = File.OpenText("version.txt");
                Version = "QuoteStorageGUI build: " + r.ReadLine();
            }
            catch { }
            
        }

       



        public ICommand OpenBtnClick { get; private set; }
        public ICommand ImportBtnClick { get; private set; }
        public ICommand CopyContextBtnClick { get; private set; }
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
                    var tab = new StorageInstance(dlg.StoragePath.Text, Interactor);
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
