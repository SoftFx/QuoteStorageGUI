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
        public enum OpenMode
        {
            ReadWrite,
            ReadOnly
        }
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
        
        public StorageInstance(string path, HistoryInteractor inter = null, OpenMode mode = OpenMode.ReadWrite)
        {
            openMode = mode;
            StoragePath = path;
            OpenBase(path);
            Interactor = inter;
            Selection = new List<Folder>();
            SaveBtnClick = new SingleDelegateCommand(SaveDelegate);
            CopyBtnClick = new SingleDelegateCommand(CopyDelegate);
            DeleteBtnClick = new SingleDelegateCommand(DeleteDelegate);
            RefreshBtnClick = new SingleDelegateCommand(RefreshDelegate);
            CloseBtnClick = new SingleDelegateCommand(CloseDelegate);
            UpdateBtnClick = new SingleDelegateCommand(UpdateDelegate);
        }



        public OpenMode openMode;
        private DB _historyStoreDB;
        
        private HistoryFile _currentFile;
        private ObservableCollection<Folder> _folders;

        public DB HistoryStoreDB { get { return _historyStoreDB; } }
        public List<Folder> Selection;
        public HistoryInteractor Interactor;
        public ICommand SaveBtnClick { get; private set; }
        public ICommand CopyBtnClick { get; private set; }
        public ICommand DeleteBtnClick { get; private set; }
        public ICommand RefreshBtnClick { get; private set; }
        public ICommand CloseBtnClick { get; private set; }
        public ICommand UpdateBtnClick { get; private set; }

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
        private string _storagePath;
        public string StoragePath
        {
            get
            {
                return _storagePath;
            }
            set
            {
                if (_storagePath == value)
                    return;
                _storagePath = value;
                NotifyPropertyChanged("StoragePath");
            }
        }
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
                HistoryLoader hl = new HistoryLoader(Application.Current.Dispatcher, _historyStoreDB);
                hl.ReadSymbols(Folders); }
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
                var ha = new HistoryLoader(Application.Current.Dispatcher, HistoryStoreDB);
                ha.ReadDateTimesAsync(f, Editor);
            }
        }
        public void Refresh()
        {
            HistoryLoader hl = new HistoryLoader(Application.Current.Dispatcher, _historyStoreDB);
            hl.Refresh(Folders);
        }

        public void OpenChunk(ChunkFile f)
        {
            var wind = Application.Current.MainWindow as MainWindowView;
            wind.ShowLoading();
            _currentFile = f;
            var content = (Editor.ReadFromDB(_currentFile)).Value;
            FileContent = ASCIIEncoding.ASCII.GetString(content);
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
            FileContent = ASCIIEncoding.ASCII.GetString(content);
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
            if(openMode == OpenMode.ReadOnly)
            {
                MessageBox.Show("Unable to save in readonly mode", "Save", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            var wind = Application.Current.MainWindow as MainWindowView;
            wind.ShowLoading();
            if (_currentFile as ChunkFile != null)
                Editor.SaveToDB(ASCIIEncoding.ASCII.GetBytes(FileContent), _currentFile as ChunkFile);
            else MessageBox.Show("Meta file editing is not possible!", "hmm...",MessageBoxButton.OK,MessageBoxImage.Asterisk);
            wind.HideLoading();
            Application.Current.MainWindow.Activate();
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
            var MainModel = Application.Current.MainWindow.DataContext as MainWindowModel;

            if (isCheckOnly)
                return MainModel.StorageTabs.Count > 1;
            else
            {

                Interactor.DiscardSelection();
                Selection.ForEach(t => { Interactor.AddToSelection(t); });

                
                var dlg = new CopyDialog(this, MainModel.StorageTabs, Interactor)
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

                if (openMode == OpenMode.ReadOnly)
                {
                    MessageBox.Show("Unable to delete in readonly mode", "Delete", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return true;
                }

                var MainModel = Application.Current.MainWindow.DataContext as MainWindowModel;
                var MainView = Application.Current.MainWindow as MainWindowView;

                var result = MessageBox.Show("Are you sure?", "Delete", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.No) return true;

                Interactor.DiscardSelection();
                Selection.ForEach(t => { Interactor.AddToSelection(t); });
                Selection.Clear();

                Interactor.Source = this;
                MainView.ShowLoading();
                var res = Interactor.Delete();
                MainView.HideLoading();

                if (res ==1)
                MessageBox.Show("Deletion completed!", "Delete",MessageBoxButton.OK,MessageBoxImage.Information);

                Application.Current.MainWindow.Activate();
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

        private bool UpdateDelegate(object o, bool isCheckOnly)
        {
            if (isCheckOnly)
                return true;
            else
            {
                foreach(var sel in Selection)
                {
                    var chunk = sel as ChunkFile;
                    if (chunk != null)
                    {
                        
                        if(chunk.Period == "ticks")
                        {
                            tickToM1Update(chunk);
                        }
                        else if(chunk.Period == "ticks level2")
                        {
                            var content = Editor.ReadAllPart(chunk, HistoryEditor.hourReadMode.oneDate);
                            var items = HistorySerializer.Deserialize(chunk.Period, content);
                            var itemsList = new List<QHItem>();
                            var ticksLevel2 = items as IEnumerable<QHTickLevel2>;
                            var ticks = Editor.GetTicksFromLevel2(ticksLevel2);
                            var parent = chunk.Parent;
                            List<Folder> deleteList = new List<Folder>();
                            foreach (var f in parent.Folders) if (f.Name.Length >= 10 && (f.Name.Substring(0, 10) == "ticks file" || f.Name.Substring(0, 10) == "ticks meta")) deleteList.Add(f);
                            deleteList.ForEach(t => parent.Folders.Remove(t));
                            var tickChunk = new ChunkFile() { Name = "ticks file", Period = "ticks", Parent = parent };
                            var partCnt = Editor.SaveToDBParted(ticks, tickChunk);
                            for (int i = partCnt; i >= 0; i--)
                            {
                                var Chunk = new ChunkFile() { Name = "ticks file", Period = "ticks", Part = i, Parent = parent };
                                parent.Folders.Add(Chunk);
                            }
                            for (int i = partCnt; i>=0; i--)
                            {
                                var Meta = new MetaFile() { Name = "ticks meta", Period = "ticks", Part = i, Parent = parent };
                                parent.Folders.Add(Meta);
                            }
                            var res = MessageBox.Show("Ticks level2 to ticks upstream update was aplied.\n Make ticks to M1?", "Upstream update", MessageBoxButton.YesNo, MessageBoxImage.Question);
                            if(res == MessageBoxResult.Yes)
                            {
                                tickToM1Update(tickChunk);
                            }
                        }
                    }
                }

                return true;
            }
        }

        private void tickToM1Update(ChunkFile chunk)
        {
            var content = Editor.ReadAllPart(chunk, HistoryEditor.hourReadMode.allDate);
            var items = HistorySerializer.Deserialize(chunk.Period, content);
            var itemsList = new List<QHItem>();
            var ticks = items as IEnumerable<QHTick>;
            var bars = Editor.GetM1FromTicks(ticks);
            var parent = chunk.Parent.Parent;
            List<Folder> deleteList = new List<Folder>();
            foreach (var f in parent.Folders) if (f.Name.Length >= 2 && f.Name.Substring(0, 2) == "M1") deleteList.Add(f);
            deleteList.ForEach(t => parent.Folders.Remove(t));
            var bidChunk = new ChunkFile() { Name = "M1 bid file", Period = "M1 bid", Parent = parent };
            parent.Folders.Add(bidChunk);
            var bidMeta = new MetaFile() { Name = "M1 bid meta", Period = "M1 bid", Parent = parent };
            parent.Folders.Add(bidMeta);
            Editor.SaveToDBParted(bars.Key, bidChunk);
            var askChunk = new ChunkFile() { Name = "M1 ask file", Period = "M1 ask", Parent = parent };
            parent.Folders.Add(askChunk);
            var askMeta = new MetaFile() { Name = "M1 ask meta", Period = "M1 ask", Parent = parent };
            parent.Folders.Add(askMeta);
            Editor.SaveToDBParted(bars.Value, askChunk);
        }

        private bool CloseDelegate(object o, bool isCheckOnly)
        {
            if (isCheckOnly)
                return true;
            else
            {
                var MainModel = Application.Current.MainWindow.DataContext as MainWindowModel;
                MainModel.TryToRemoveStorage(this);
                _historyStoreDB.Dispose();
                return true;
            }
        }
    }
}
