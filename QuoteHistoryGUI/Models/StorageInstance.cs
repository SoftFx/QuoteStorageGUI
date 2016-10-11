using LevelDB;
using QuoteHistoryGUI.Dialogs;
using QuoteHistoryGUI.HistoryTools;
using QuoteHistoryGUI.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace QuoteHistoryGUI.Models
{
    public class StorageInstance : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
        public string Status = "";
        public HistoryEditor Editor;
        public StorageInstance(string path, HistoryInteractor inter = null)
        {
            StoragePath = path;
            OpenBase(path);
            Interactor = inter;
            SaveBtnClick = new SingleDelegateCommand(SaveDelegate);
            CopyBtnClick = new SingleDelegateCommand(CopyDelegate);
            DeleteBtnClick = new SingleDelegateCommand(DeleteDelegate);
            RefreshBtnClick = new SingleDelegateCommand(RefreshDelegate);
        }

        private DB _historyStoreDB;
        public DB HistoryStoreDB { get { return _historyStoreDB; } }
        private HistoryFile _currentFile;
        private ObservableCollection<Folder> _folders;
        public HistoryInteractor Interactor;
        public ICommand SaveBtnClick { get; private set; }
        public ICommand CopyBtnClick { get; private set; }
        public ICommand DeleteBtnClick { get; private set; }
        public ICommand RefreshBtnClick { get; private set; }
        public ObservableCollection<Folder> Folders
        {
            get { return _folders; }
            set
            {
                if (_folders == value)
                    return;
                _folders = value;
                NotifyPropertyChanged("Folders");
            }
        }
        public string StoragePath;
        private string _fileContent;
        public string FileContent
        {
            get {
                return _fileContent; }
            set
            {
                if (_fileContent == value)
                    return;
                _fileContent = value;
                NotifyPropertyChanged("FileContent");
            }
        }

        private string _filePath;

        public string FilePath
        {
            get { return _filePath; }
            set
            {
                if (_filePath == value)
                    return;
                _filePath = value;
                NotifyPropertyChanged("FilePath");
            }
        }

        private int _foldersCnt;
        public int FoldersCnt
        {
            get { return _foldersCnt; }
            set
            {
                if (_foldersCnt == value)
                    return;
                _foldersCnt = value;
                NotifyPropertyChanged("Folders");
            }
        }

        private void OpenBase(string path)
        {
            Folders = new ObservableCollection<Folder>();
            try {
                if (!Directory.Exists(path + "\\HistoryDB"))
                    throw new Exception("Cant't find a history database folder (HistoryDB) in folder: " + path);
                _historyStoreDB = new DB(path + "\\HistoryDB",
                        new Options() { BloomFilter = new BloomFilterPolicy(10) });
                Editor = new HistoryEditor(_historyStoreDB);
                HistoryLoader hl = new HistoryLoader(Application.Current.Dispatcher, _historyStoreDB, Folders);
                hl.ReadSymbols(); }
            catch (Exception ex)
            {
                Status = ex.Message;
                return;
            }
            FilePath = path;
            Status = "Ok";
        }

        public void Expand(Folder f)
        {

            if (!f.Loaded)
            {
                f.Loaded = true;
                var ha = new HistoryLoader(Application.Current.Dispatcher, HistoryStoreDB, f.Folders, f);
                ha.ReadDateTimes(f, Editor);
            }
        }
        public void Refresh()
        {

            HistoryLoader hl = new HistoryLoader(Application.Current.Dispatcher, _historyStoreDB, Folders);
            hl.Refresh();

        }

        public void OpenChunk(ChunkFile f)
        {
            var wind = Application.Current.MainWindow as MainWindowView;
            wind.ShowLoading();
            _currentFile = f;
            var content = (Editor.ReadFromDB(_currentFile)).Value;
            FileContent = content;
            string path = f.Name;
            var par = f.Parent;
            while (par.Parent != null)
            {
                path = par.Name + "/" + path;
                par = par.Parent;
            }
            path = par.Name + "/" + path;
            FilePath = path;   

        }

        public void OpenMeta(MetaFile f)
        {
            var wind = Application.Current.MainWindow as MainWindowView;
            _currentFile = f;
            var content = Editor.ReadFromDB(_currentFile).Value;
            FileContent = content;
            string path = f.Name;
            var par = f.Parent;
            while (par.Parent != null)
            {
                path = par.Name + "/" + path;
                par = par.Parent;
            }
            path = par.Name + "/" + path;
            FilePath = path;

        }


        public void SaveChunk()
        {
            var wind = Application.Current.MainWindow as MainWindowView;
            wind.ShowLoading();
            if (_currentFile as ChunkFile != null)
                Editor.SaveToDB(FileContent, _currentFile as ChunkFile);
            else MessageBox.Show("Meta file editing is not possible!", "hmm...",MessageBoxButton.OK,MessageBoxImage.Asterisk);
            wind.HideLoading();
        }

        private bool SaveDelegate(object o, bool isCheckOnly)
        {
            if (isCheckOnly)
                return true;
            else
            {
                SaveChunk();
                return true;
            }
        }

        private bool CopyDelegate(object o, bool isCheckOnly)
        {
            if (isCheckOnly)
                return true;
            else
            {
                var MainModel = Application.Current.MainWindow.DataContext as MainWindowModel;
                var dlg = new CopyDialog(this, MainModel.StorageTabs, MainModel.Interactor)
                {
                    Owner = Application.Current.MainWindow
                };
                dlg.ShowDialog();
                return true;
            }
        }

        private bool DeleteDelegate(object o, bool isCheckOnly)
        {
            if (isCheckOnly)
                return true;
            else
            {
                var MainModel = Application.Current.MainWindow.DataContext as MainWindowModel;
                MainModel.Interactor.Source = this;
                MainModel.Interactor.Delete();
                //Refresh();
                return true;
            }
        }

        private bool RefreshDelegate(object o, bool isCheckOnly)
        {
            if (isCheckOnly)
                return true;
            else
            {
                Refresh();
                return true;
            }
        }

        public void Close()
        {
            var MainModel = Application.Current.MainWindow.DataContext as MainWindowModel;
            MainModel.StorageTabs.Remove(this);
            _historyStoreDB.Dispose();
        }

    }
}
