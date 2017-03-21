using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LevelDB;
using System.Windows.Threading;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;
using QuoteHistoryGUI.HistoryTools;
using static QuoteHistoryGUI.HistoryTools.HistoryDatabaseFuncs;
using QuoteHistoryGUI.Models;
using log4net;
using System.Threading;

namespace QuoteHistoryGUI
{
    public class HistoryLoader
    {
        public static readonly ILog log = LogManager.GetLogger(typeof(StorageInstanceModel));

        public static List<KeyValuePair<int, int>> MinMaxDateTime = new List<KeyValuePair<int, int>> { new KeyValuePair<int, int>(2000, 2030),
            new KeyValuePair<int, int>(1, 12),
            new KeyValuePair<int, int>(1, 31),
            new KeyValuePair<int, int>(0, 23),
            new KeyValuePair<int, int>(0, 0)};

        Dispatcher _dispatcher;
        DB _dbase;
        ObservableCollection<Folder> _folders;
        Folder _folder;
        HistoryEditor _editor;

        private object DBDisposeLock = new object();
        private int loading = 0;

        public int TryDisposeLoader()
        {
            lock (DBDisposeLock)
            {
                if (loading != 0)
                    return -1;
                loading = -1;
            }
            return 0;
        }

        public int RestoreLoader()
        {
            lock (DBDisposeLock)
            {
                if (loading == -1)
                    loading = 0;
            }
            return 0;
        }

        public HistoryLoader(Dispatcher dispatcher, DB dbase, HistoryEditor editor = null)
        {
            _dispatcher = dispatcher;
            _dbase = dbase;
            _editor = editor;
        }


        public void ReadSymbols(ObservableCollection<Folder> folders)
        {
            lock (DBDisposeLock)
            {
                if (loading == -1)
                    return;
                loading++;
                _folders = folders;
                var w = new BackgroundWorker();
                w.DoWork += ReadSymbolsWork;
                w.RunWorkerCompleted += QHAppWindowModel.throwExceptions;
                w.RunWorkerAsync();
            }
        }

        public void ReadSymbolsSync(ObservableCollection<Folder> folders)
        {
            _folders = folders;
            ReadSymbolsWork(new object(), new DoWorkEventArgs(new object()));
        }

        public IEnumerable<DBEntry> ReadMeta(string symbol, string period)
        {
            var key = HistoryDatabaseFuncs.SerealizeKey(symbol, "Meta", period, 2000, 1, 1, 0);
            var it = _dbase.CreateIterator();
            it.Seek(key);

            List<HistoryDatabaseFuncs.DBEntry> resl = new List<HistoryDatabaseFuncs.DBEntry>();
            while (it.Valid() && HistoryDatabaseFuncs.ValidateKeyByKey(it.Key(), key, true, 0, true, true))
            {
                yield return HistoryDatabaseFuncs.DeserealizeKey(it.Key());
                it.Next();
            }
            it.Dispose();
        }

        private void ReadSymbolsWork(object sender, DoWorkEventArgs e)
        {

            if (_dispatcher != null)
                _dispatcher.Invoke(delegate
                { _folders.Insert(0, new LoadingFolder()); _folders[0].Parent = null; });
            else { _folders.Insert(0, new LoadingFolder()); _folders[0].Parent = null; }
            Iterator it = null;
            it = _dbase.CreateIterator();
            it.SeekToFirst();
            while (it.Valid())
            {
                var key = it.Key();
                List<byte> nextKey = new List<byte>();
                for (int i = 0; i < key.Length; i++)
                {
                    if (key[i] > 1)
                        nextKey.Add(key[i]);
                    else break;
                }
                string sym = ASCIIEncoding.ASCII.GetString(nextKey.ToArray());
                if (_dispatcher != null)
                    _dispatcher.Invoke(delegate
                { _folders.Insert(_folders.Count - 1, new Folder(sym)); _folders[_folders.Count - 2].Parent = null; });
                else { _folders.Insert(_folders.Count - 1, new Folder(sym)); _folders[_folders.Count - 2].Parent = null; }

                nextKey.Add(3);

                it.Seek(nextKey.ToArray());
            }

            it.Dispose();
            if (_dispatcher != null)
                _dispatcher.Invoke(delegate { _folders.RemoveAt(_folders.Count - 1); });
            else { _folders.RemoveAt(_folders.Count - 1); }
            lock (DBDisposeLock)
            {
                loading--;
            }
        }

        public void ReadDateTimesAsync(Folder folder, HistoryEditor editor = null)
        {
            lock (DBDisposeLock)
            {
                if (loading == -1)
                    return;
                loading++;
                _editor = editor;
                _folder = folder;
                var w = new BackgroundWorker();
                w.DoWork += ReadFoldersAndFiles;
                w.RunWorkerCompleted += QHAppWindowModel.throwExceptions;
                w.RunWorkerAsync();
            }
        }

        public void ReadDateTimes(Folder folder, HistoryEditor editor = null)
        {
            _editor = editor;
            _folder = folder;

            LoadFolders();
            LoadFiles();
            if (_dispatcher != null)
                _dispatcher.Invoke(delegate { _folder.Folders?.RemoveAt(0); });
            else _folder.Folders?.RemoveAt(0);
        }
        public void Refresh(ObservableCollection<Folder> folders)
        {
            _folders = folders;
            var w = new BackgroundWorker();
            w.DoWork += Refresh;
            w.RunWorkerCompleted += QHAppWindowModel.throwExceptions;
            w.RunWorkerAsync();
        }
        private void LoadFolders()
        {
            var path = HistoryDatabaseFuncs.GetPath(_folder);
            if (path.Count < 5)
            {
                int[] dateTime = { 2000, 1, 1, 0 };
                for (int i = 1; i < path.Count; i++)
                {
                    //dateTime[i - 1] = int.Parse(path[i].Name);
                    int dt;
                    int.TryParse(path[i].Name, out dt);
                    dateTime[i - 1] = dt;
                }

                int curDateInd = path.Count - 1;

                for (int DT = MinMaxDateTime[curDateInd].Key; DT <= MinMaxDateTime[curDateInd].Value; DT++)
                {
                    dateTime[curDateInd] = DT;
                    List<byte[]> keys = new List<byte[]> {
                            HistoryDatabaseFuncs.SerealizeKey(path[0].Name,"Meta","ticks",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),
                            HistoryDatabaseFuncs.SerealizeKey(path[0].Name,"Meta","ticks level2",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),
                            HistoryDatabaseFuncs.SerealizeKey(path[0].Name,"Chunk","ticks",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),
                            HistoryDatabaseFuncs.SerealizeKey(path[0].Name,"Chunk","ticks level2",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),

                        };
                    if (path.Count < 4)
                    {
                        keys.Add(HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Meta", "M1 bid", dateTime[0], dateTime[1], dateTime[2], dateTime[3], 0));
                        keys.Add(HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Meta", "M1 ask", dateTime[0], dateTime[1], dateTime[2], dateTime[3], 0));
                        keys.Add(HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Chunk", "M1 bid", dateTime[0], dateTime[1], dateTime[2], dateTime[3], 0));
                        keys.Add(HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Chunk", "M1 ask", dateTime[0], dateTime[1], dateTime[2], dateTime[3], 0));
                    }

                    if (path.Count < 3)
                    {
                        keys.Add(HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Meta", "H1 bid", dateTime[0], dateTime[1], dateTime[2], dateTime[3], 0));
                        keys.Add(HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Meta", "H1 ask", dateTime[0], dateTime[1], dateTime[2], dateTime[3], 0));
                        keys.Add(HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Chunk", "H1 bid", dateTime[0], dateTime[1], dateTime[2], dateTime[3], 0));
                        keys.Add(HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Chunk", "H1 ask", dateTime[0], dateTime[1], dateTime[2], dateTime[3], 0));
                    }
                    var it = _dbase.CreateIterator();
                    foreach (var key in keys)
                    {
                        it.Seek(key);
                        if (!it.Valid())
                            break;
                        var getedKey = it.Key();
                        try
                        {
                            if (HistoryDatabaseFuncs.ValidateKeyByKey(getedKey, key, true, path.Count, false, true))
                            {
                                if (_dispatcher != null)
                                {
                                    _dispatcher.Invoke(delegate
                                    {
                                        if (_folder.Folders != null)
                                        {
                                            _folder.Folders.Add(new Folder(DT.ToString()));
                                            _folder.Folders[_folder.Folders.Count - 1].Parent = _folder;
                                        }
                                    });
                                }
                                else
                                {
                                    if (_folder.Folders != null)
                                    {
                                        _folder.Folders.Add(new Folder(DT.ToString()));
                                        _folder.Folders[_folder.Folders.Count - 1].Parent = _folder;
                                    }
                                }
                                break;
                            }
                        }
                        catch (Exception) { }
                    }
                    it.Dispose();
                }
            }
        }
        private void LoadFiles(bool ShowMessages = true)
        {
            var path = HistoryDatabaseFuncs.GetPath(_folder);
            if (path.Count >= 3)
            {
                int[] dateTime = { 2000, 1, 1, 0, 0 };
                for (int i = 1; i < path.Count; i++)
                {
                    //dateTime[i - 1] = int.Parse(path[i].Name);
                    int dt;
                    int.TryParse(path[i].Name, out dt);
                    dateTime[i - 1] = dt;
                }
                int curDateInd = path.Count - 1;
                string[] names = { };
                List<byte[]> keys = new List<byte[]>();
                switch (path.Count)
                {
                    case 3:
                        {
                            names = new string[] { "H1 bid", "H1 bid", "H1 ask", "H1 ask" };
                            keys = new List<byte[]> {
                                    HistoryDatabaseFuncs.SerealizeKey(path[0].Name,"Chunk","H1 bid",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),
                                    HistoryDatabaseFuncs.SerealizeKey(path[0].Name,"Meta","H1 bid",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),
                                    HistoryDatabaseFuncs.SerealizeKey(path[0].Name,"Chunk","H1 ask",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),
                                    HistoryDatabaseFuncs.SerealizeKey(path[0].Name,"Meta","H1 ask",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),
                                };
                            break;
                        }
                    case 4:
                        {
                            names = new string[] { "M1 bid", "M1 bid", "M1 ask", "M1 ask" };
                            keys = new List<byte[]> {
                                    HistoryDatabaseFuncs.SerealizeKey(path[0].Name,"Chunk","M1 bid",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),
                                    HistoryDatabaseFuncs.SerealizeKey(path[0].Name,"Meta","M1 bid",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),
                                    HistoryDatabaseFuncs.SerealizeKey(path[0].Name,"Chunk","M1 ask",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),
                                    HistoryDatabaseFuncs.SerealizeKey(path[0].Name,"Meta","M1 ask",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),
                                };
                            break;
                        }
                    case 5:
                        {
                            names = new string[] { "ticks level2", "ticks level2", "ticks", "ticks", };
                            keys = new List<byte[]> {
                                    HistoryDatabaseFuncs.SerealizeKey(path[0].Name,"Chunk","ticks level2",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),
                                    HistoryDatabaseFuncs.SerealizeKey(path[0].Name,"Meta","ticks level2",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),
                                    HistoryDatabaseFuncs.SerealizeKey(path[0].Name,"Chunk","ticks",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),
                                    HistoryDatabaseFuncs.SerealizeKey(path[0].Name,"Meta","ticks",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),
                                };
                            break;
                        }
                }
                var it = _dbase.CreateIterator();
                for (int i = 0; i < keys.Count; i++)
                {
                    it.Seek(keys[i]);
                    if (!it.Valid())
                        continue;
                    var getedKey = it.Key();
                    try
                    {
                        while (true)
                        {
                            var part = -1;
                            if (HistoryDatabaseFuncs.ValidateKeyByKey(getedKey, keys[i], true, path.Count - 1, true, true) && it.Valid())
                            {

                                if (i == 0 || i == 2)
                                {
                                    var chunk = new ChunkFile(names[i] + " file" + (getedKey[getedKey.Length - 2] > 0 ? ("." + getedKey[getedKey.Length - 2] + "") : ""), names[i], getedKey[getedKey.Length - 2]);
                                    chunk.Parent = _folder;
                                    if (_dispatcher != null)
                                        _dispatcher.Invoke(delegate { _folder.Folders?.Add(chunk); });
                                    else { _folder.Folders?.Add(chunk); }
                                    if (part == getedKey[getedKey.Length - 2])
                                        break;
                                    part = getedKey[getedKey.Length - 2];
                                    if (_editor != null)
                                    {
                                        _editor.RebuildMeta(chunk);
                                        it.Dispose();
                                        it = _dbase.CreateIterator();
                                        it.Seek(getedKey);
                                    }
                                    //_dispatcher.Invoke(delegate { Application.Current.MainWindow.Activate(); });
                                }
                                else
                                    if (_dispatcher != null)
                                {
                                    _dispatcher.Invoke(delegate
                               {
                                   if (_folder.Folders != null)
                                   {
                                       _folder.Folders.Add(new MetaFile(names[i] + " meta" + (getedKey[getedKey.Length - 2] > 0 ? ("." + getedKey[getedKey.Length - 2] + "") : ""), names[i], getedKey[getedKey.Length - 2]));
                                       _folder.Folders[_folder.Folders.Count - 1].Parent = _folder;
                                   }
                               });
                                }
                                else
                                {
                                    if (_folder.Folders != null)
                                    {
                                        _folder.Folders.Add(new MetaFile(names[i] + " meta" + (getedKey[getedKey.Length - 2] > 0 ? ("." + getedKey[getedKey.Length - 2] + "") : ""), names[i], getedKey[getedKey.Length - 2]));
                                        _folder.Folders[_folder.Folders.Count - 1].Parent = _folder;
                                    }
                                }
                                it.Next();
                                if (it.Valid())
                                {
                                    getedKey = it.Key();
                                }
                            }
                            else break;
                        }
                    }
                    catch (Exception) { }
                }
                it.Dispose();
            }
        }
        private void ReadFoldersAndFiles(object sender, DoWorkEventArgs e)
        {
            LoadFolders();
            LoadFiles();
            _dispatcher.Invoke(delegate { _folder.Folders?.RemoveAt(0); });
            lock (DBDisposeLock)
            {
                loading--;
            }
        }

        private void Refresh(object sender, DoWorkEventArgs e)
        {
            Folder[] oldFolders = new Folder[_folders.Count()];
            _folders.CopyTo(oldFolders, 0);
            _dispatcher.Invoke(delegate { _folders.Clear(); });
            if (oldFolders.Count() == 0 || oldFolders[0].Parent == null)
                ReadSymbols(_folders);
            else ReadDateTimes(oldFolders[0].Parent);
        }

        private void RefreshAll(object sender, DoWorkEventArgs e)
        {
            Folder[] oldFolders = new Folder[_folders.Count()];
            _folders.CopyTo(oldFolders, 0);
            _dispatcher.Invoke(delegate { _folders.Clear(); });
            if (oldFolders.Count() == 0) return;
            if (oldFolders[0].Parent == null)
                ReadSymbols(_folders);
            else ReadDateTimes(oldFolders[0].Parent);
        }
    }
}
