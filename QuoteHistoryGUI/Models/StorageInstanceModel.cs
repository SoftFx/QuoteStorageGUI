using LevelDB;
using log4net;
using QuoteHistoryGUI.Dialogs;
using QuoteHistoryGUI.HistoryTools;
using QuoteHistoryGUI.HistoryTools.MetaStorage;
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
    public class StorageInstanceModel : INotifyPropertyChanged
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
        public static readonly ILog log = LogManager.GetLogger(typeof(StorageInstanceModel));
        public StorageInstanceModel(string path, Dispatcher dispatcher, HistoryInteractor inter = null, OpenMode mode = OpenMode.ReadWrite, bool syncLoading = false)
        {
            //log4net.Config.XmlConfigurator.Configure();
            try
            {
                _dispatcher = dispatcher;
                openMode = mode;
                StoragePath = path;
                _dispatcher = dispatcher;
                OpenBase(path, syncLoading);
                MetaStorage = new MetaStorage(new HistoryLoader(_dispatcher, _historyStoreDB));
                Interactor = inter;
                Selection = new List<Folder>();
                SaveBtnClick = new SingleDelegateCommand(SaveDelegate);
                CopyBtnClick = new SingleDelegateCommand(CopyDelegate);
                DeleteBtnClick = new SingleDelegateCommand(DeleteDelegate);
                RefreshBtnClick = new SingleDelegateCommand(RefreshDelegate);
                CloseBtnClick = new SingleDelegateCommand(CloseDelegate);
                UpdateBtnClick = new SingleDelegateCommand(UpdateDelegate);
                EditBtnClick = new SingleDelegateCommand(EditDelegate);
                RenameBtnClick = new SingleDelegateCommand(RenameDelegate);
                log.Info("StorageInstance initialized: " + path);
            }
            catch (Exception ex)
            { log.Error(ex.Message); throw ex; }
        }



        public OpenMode openMode;
        private DB _historyStoreDB;

        private HistoryFile _currentFile;
        private ObservableCollection<Folder> _folders;

        public MetaStorage MetaStorage;

        public DB HistoryStoreDB { get { return _historyStoreDB; } }
        public List<Folder> Selection;
        public HistoryInteractor Interactor;
        public ICommand SaveBtnClick { get; private set; }
        public ICommand CopyBtnClick { get; private set; }
        public ICommand DeleteBtnClick { get; private set; }
        public ICommand RefreshBtnClick { get; private set; }
        public ICommand CloseBtnClick { get; private set; }
        public ICommand UpdateBtnClick { get; private set; }
        public ICommand EditBtnClick { get; private set; }
        public ICommand RenameBtnClick { get; private set; }

        private Dispatcher _dispatcher;

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

        public class chunkLine : INotifyPropertyChanged
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

            private string _text;
            public string Text
            {
                get
                {
                    return _text;
                }
                set
                {
                    if (_text == value)
                        return;
                    _text = value;
                    NotifyPropertyChanged("Text");
                }
            }
            public chunkLine(string s)
            {
                Text = s;
            }
        }

        private ObservableCollection<chunkLine> _fileContent;
        public ObservableCollection<chunkLine> FileContent
        {
            get
            {
                return _fileContent;
            }
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

        private void OpenBase(string path, bool syncLoading = false)
        {
            Folders = new ObservableCollection<Folder>();
            try
            {
                log.Info("Opening database: " + path);
                if (!Directory.Exists(path + "\\HistoryDB"))
                    throw new Exception("Cant't find a history database folder (HistoryDB) in folder: " + path);

                _historyStoreDB = new DB(path + "\\HistoryDB",
                        new Options() { BloomFilter = new BloomFilterPolicy(10), CreateIfMissing = true });
                Editor = new HistoryEditor(_historyStoreDB);
                HistoryLoader hl = new HistoryLoader(_dispatcher, _historyStoreDB);
                if (!syncLoading)
                    hl.ReadSymbols(Folders);
                else hl.ReadSymbolsSync(Folders);
                log.Info("Database opened and initialized: " + path);
            }
            catch (Exception ex)
            {
                Status = ex.Message;
                log.Warn(Status);
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
                var ha = new HistoryLoader(_dispatcher, HistoryStoreDB);
                ha.ReadDateTimesAsync(f, Editor);
            }
        }
        public void Refresh()
        {
            HistoryLoader hl = new HistoryLoader(_dispatcher, _historyStoreDB);
            hl.Refresh(Folders);
        }

        public void OpenChunk(ChunkFile f)
        {
            try
            {
                log.Info("Chunk opening... " + f.Name);
                _currentFile = f;
                var content = (Editor.ReadFromDB(_currentFile)).Value;
                var strContent = ASCIIEncoding.ASCII.GetString(content);
                StringReader reader = new StringReader(strContent);
                var contentResult = new ObservableCollection<chunkLine>();
               
                while (true)
                {
                    var line = reader.ReadLine();
                    if (line == null)
                        break;
                    contentResult.Add(new chunkLine(line));
                }
                FileContent = contentResult;
                string path = f.Name;
                var par = f.Parent;
                while (par.Parent != null)
                {
                    path = par.Name + "/" + path;
                    par = par.Parent;
                }
                path = par.Name + "/" + path;
                FilePath = path;
                log.Info("Chunk opened: " + FilePath);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                throw ex;
            }

        }

        public void OpenMeta(MetaFile f)
        {
            try
            {
                log.Info("Meta opening... " + f.Name);
                var wind = Application.Current.MainWindow as QHAppWindowView;
                _currentFile = f;
                var content = Editor.ReadFromDB(_currentFile).Value;
                var strContent = ASCIIEncoding.ASCII.GetString(content);
                StringReader reader = new StringReader(strContent);
                var contentResult = new ObservableCollection<chunkLine>();

                while (true)
                {
                    var line = reader.ReadLine();
                    if (line == null)
                        break;
                    contentResult.Add(new chunkLine(line));
                }
                FileContent = contentResult;
                string path = f.Name;
                var par = f.Parent;
                while (par.Parent != null)
                {
                    path = par.Name + "/" + path;
                    par = par.Parent;
                }
                path = par.Name + "/" + path;
                FilePath = path;
                log.Info("Meta opened: " + FilePath);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                throw ex;
            }

        }


        public void SaveChunk()
        {
            try
            {
                log.Info("Chunk saving... ");
                if (openMode == OpenMode.ReadOnly)
                {
                    MessageBox.Show("Unable to save in readonly mode", "Save", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }
                if (_currentFile as ChunkFile != null)
                {
                    StringBuilder builder = new StringBuilder();
                    foreach(var line in FileContent)
                    {
                        builder.Append(line.Text+"\r\n");
                    }
                    Editor.SaveToDB(ASCIIEncoding.ASCII.GetBytes(builder.ToString()), _currentFile as ChunkFile);
                }
                else MessageBox.Show("Meta file editing is not possible!", "hmm...", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                Application.Current.MainWindow.Activate();
                log.Info("Chunk saved: " + FilePath);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                throw ex;
            }
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
            var MainModel = Application.Current.MainWindow.DataContext as QHAppWindowModel;

            if (isCheckOnly)
                return true;
            else
            {
                try
                {
                    log.Info("Copy context menu call... ");
                    Interactor.DiscardSelection();
                    Selection.ForEach(t => { Interactor.AddToSelection(t); });


                    var dlg = new ExportDialog(this, Interactor)
                    {
                        Owner = Application.Current.MainWindow
                    };
                    dlg.ShowDialog();
                    log.Info("Copy performed... ");
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                    throw ex;
                }
                return true;
            }
        }

        private bool DeleteDelegate(object o, bool isCheckOnly)
        {
            if (isCheckOnly)
                if (Selection.Count == 0)
                    return false;
                else
                    return true;
            else
            {
                try
                {
                    log.Info("Delete context menu call... ");
                    if (openMode == OpenMode.ReadOnly)
                    {
                        MessageBox.Show("Unable to delete in readonly mode", "Delete", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        return true;
                    }

                    var MainModel = Application.Current.MainWindow.DataContext as QHAppWindowModel;
                    var MainView = Application.Current.MainWindow as QHAppWindowView;

                    string DeleteMessage = "";

                    if (Selection.Count == 1)
                    {
                        var path = HistoryDatabaseFuncs.GetPath(Selection[0]);

                        for (int i = 0; i < path.Count; i++) DeleteMessage += ((DeleteMessage.Length == 0 ? "" : "/") + path[i].Name);

                        DeleteMessage = "\n\n" + DeleteMessage + " ?";
                    }
                    else
                        DeleteMessage = Selection.Count + " items ?";

                    var result = MessageBox.Show("Are you sure to delete " + DeleteMessage, "Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.No) return true;
                    Interactor.Source = this;
                    DeleteProgressDialog dlg = new DeleteProgressDialog(Interactor, Selection, _dispatcher)
                    {
                        Owner = MainView
                    };

                    dlg.ShowDialog();
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                    throw ex;
                }
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

        private bool EditDelegate(object o, bool isCheckOnly)
        {
            if (isCheckOnly)
                return _currentFile!=null;
            else
            {
                try
                {
                    if (_currentFile as ChunkFile != null)
                    {
                        StringBuilder builder = new StringBuilder();
                        foreach (var line in FileContent)
                        {
                            builder.Append(line.Text + "\r\n");
                        }
                        EditDialog dlg = new EditDialog { Owner = Application.Current.MainWindow };
                        dlg.Content.Text = builder.ToString();
                        dlg.ShowDialog();
                        StringReader reader = new StringReader(dlg.Content.Text);
                        ObservableCollection<chunkLine> contentResult = new ObservableCollection<chunkLine>();
                        if (dlg.CompleteEdit)
                        {
                            while (true)
                            {
                                var line = reader.ReadLine();
                                if (line == null)
                                    break;
                                contentResult.Add(new chunkLine(line));
                            }
                            FileContent = contentResult;
                        }

                    }
                    else MessageBox.Show("Meta file editing is not possible!", "hmm...", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                    MessageBox.Show(ex.Message, "Edit error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    foreach (var sel in Selection)
                    {
                        log.Info("context upstram for " + sel);
                        var chunk = sel as ChunkFile;
                        if (chunk != null)
                        {

                            if (chunk.Period == "ticks")
                            {
                                tickToM1Update(chunk);
                            }
                            else if (chunk.Period == "ticks level2")
                            {
                                tick2ToTickUpdate(chunk);
                                var res = MessageBox.Show("Ticks level2 to ticks upstream update was applied.\n Make ticks to M1?", "Upstream update", MessageBoxButton.YesNo, MessageBoxImage.Question);
                                if (res == MessageBoxResult.Yes)
                                {

                                    var tickChunk = new ChunkFile() { Name = "ticks file", Period = "ticks", Parent = chunk.Parent };
                                    tickToM1Update(tickChunk);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                    MessageBox.Show(ex.Message, "Upstream error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                return true;
            }
        }

        private bool RenameDelegate(object o, bool isCheckOnly)
        {
            if (isCheckOnly)
                return Selection.Count != 0 && Selection[0].Parent==null;
            else
            {
                try
                {
                    if (Selection.Count != 0)
                    {
                        var dlg = new RenameDialog(_dispatcher, this);
                        dlg.FromBox.Text = Selection[0].Name;
                        dlg.ToBox.Text = Selection[0].Name;
                        dlg.Owner = Application.Current.MainWindow;
                        dlg.ShowDialog();
                        this.Refresh();
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                    MessageBox.Show(ex.Message, "Rename error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                return true;
            }
        }

        public KeyValuePair<ChunkFile[], QHTick[]> tick2ToTickUpdate(ChunkFile chunk, bool showMessages = true)
        {

            var content = Editor.ReadAllPart(chunk, HistoryEditor.hourReadMode.oneDate);
            var items = HistorySerializer.Deserialize(chunk.Period, content);
            var itemsList = new List<QHItem>();
            var ticksLevel2 = items as IEnumerable<QHTickLevel2>;
            var ticks = Editor.GetTicksFromLevel2(ticksLevel2);
            var parent = chunk.Parent;
            List<Folder> deleteList = new List<Folder>();
            foreach (var f in parent.Folders) if (f.Name.Length >= 10 && (f.Name.Substring(0, 10) == "ticks file" || f.Name.Substring(0, 10) == "ticks meta")) deleteList.Add(f);

            var tickChunk = new ChunkFile() { Name = "ticks file", Period = "ticks", Parent = parent };
            var partCnt = Editor.SaveToDBParted(ticks, tickChunk, true, showMessages);
            List<ChunkFile> chunks = new List<ChunkFile>();
            deleteList.ForEach(t => _dispatcher.Invoke(() => parent.Folders.Remove(t)));
            for (int i = partCnt; i >= 0; i--)
            {
                var Chunk = new ChunkFile() { Name = "ticks file", Period = "ticks", Part = i, Parent = parent };
                chunks.Add(Chunk);
                _dispatcher.Invoke(() => parent.Folders.Add(Chunk));
            }
            for (int i = partCnt; i >= 0; i--)
            {
                var Meta = new MetaFile() { Name = "ticks meta", Period = "ticks", Part = i, Parent = parent };
                _dispatcher.Invoke(() => parent.Folders.Add(Meta));
            }
            return new KeyValuePair<ChunkFile[], QHTick[]>(chunks.ToArray(), ticks);
        }


        public KeyValuePair<QHBar[], QHBar[]> tickToM1Update(ChunkFile chunk, bool showMessages = true)
        {

            var content = Editor.ReadAllPart(chunk, HistoryEditor.hourReadMode.allDate);
            var items = HistorySerializer.Deserialize(chunk.Period, content);
            var itemsList = new List<QHItem>();
            var ticks = items as IEnumerable<QHTick>;
            var bars = Editor.GetM1FromTicks(ticks);
            var parent = chunk.Parent.Parent;
            List<Folder> deleteList = new List<Folder>();

            foreach (var f in parent.Folders) if (f.Name.Length >= 2 && f.Name.Substring(0, 2) == "M1") deleteList.Add(f);
            deleteList.ForEach(t => _dispatcher.Invoke(() => parent.Folders.Remove(t)));
            var bidChunk = new ChunkFile() { Name = "M1 bid file", Period = "M1 bid", Parent = parent };
            _dispatcher.Invoke(() => parent.Folders.Add(bidChunk));
            var bidMeta = new MetaFile() { Name = "M1 bid meta", Period = "M1 bid", Parent = parent };
            _dispatcher.Invoke(() => parent.Folders.Add(bidMeta));

            var askChunk = new ChunkFile() { Name = "M1 ask file", Period = "M1 ask", Parent = parent };
            _dispatcher.Invoke(() => parent.Folders.Add(askChunk));
            var askMeta = new MetaFile() { Name = "M1 ask meta", Period = "M1 ask", Parent = parent };
            _dispatcher.Invoke(() => parent.Folders.Add(askMeta));
            Editor.SaveToDBParted(bars.Key, bidChunk, true, showMessages);
            Editor.SaveToDBParted(bars.Value, askChunk, true, showMessages);
            return bars;



        }

        private bool CloseDelegate(object o, bool isCheckOnly)
        {
            if (isCheckOnly)
                return true;
            else
            {
                try
                {
                    log.Info("Storage instance closing: " + StoragePath);
                    var MainModel = Application.Current.MainWindow.DataContext as QHAppWindowModel;
                    MainModel.TryToRemoveStorage(this);
                    //_historyStoreDB.Dispose();
                    log.Info("Storage instance closed, database disposed: " + StoragePath);
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                    throw ex;
                }
                return true;
            }
        }
    }
}
