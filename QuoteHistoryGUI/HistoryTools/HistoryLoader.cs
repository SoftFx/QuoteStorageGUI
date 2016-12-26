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

namespace QuoteHistoryGUI
{
    public class HistoryLoader
    {

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

        public HistoryLoader(Dispatcher dispatcher, DB dbase)
        {
            _dispatcher = dispatcher;
            _dbase = dbase;
        }



        public void ReadSymbols(ObservableCollection<Folder> folders)
        {
            _folders = folders;
            var w = new BackgroundWorker();
            w.DoWork += ReadSymbolsWork;
            w.RunWorkerAsync();
        }

        public IEnumerable<DBEntry> ReadMeta(string symbol, string period)
        {
            var key = HistoryDatabaseFuncs.SerealizeKey(symbol, "Meta", period, 2000, 1, 1, 0);
            var it = _dbase.CreateIterator();
            it.Seek(key);
            var res = "";

            List<HistoryDatabaseFuncs.DBEntry> resl = new List<HistoryDatabaseFuncs.DBEntry>();
            while (it.IsValid() && HistoryDatabaseFuncs.ValidateKeyByKey(it.GetKey(), key, true, 0, true, true))
            {
                yield return HistoryDatabaseFuncs.DeserealizeKey(it.GetKey());
                it.Next();
            }
            it.Dispose();
        }

        private void ReadSymbolsWork(object sender, DoWorkEventArgs e)
        {
            _dispatcher.Invoke((Action)delegate () { _folders.Insert(0, new LoadingFolder()); _folders[0].Parent = null; });
            var it = _dbase.CreateIterator();
            it.SeekToFirst();
            while (it.IsValid())
            {
                var key = it.GetKey();
                List<byte> nextKey = new List<byte>();
                for (int i = 0; i < key.Length; i++)
                {
                    if (key[i] > 1)
                        nextKey.Add(key[i]);
                    else break;
                }
                string sym = ASCIIEncoding.ASCII.GetString(nextKey.ToArray());
                _dispatcher.Invoke((Action)delegate () { _folders.Insert(_folders.Count - 1, new Folder(sym)); _folders[_folders.Count - 2].Parent = null; });
                for (int i = 0; i < 10; i++)
                    nextKey.Add(255);
                it.Seek(nextKey.ToArray());
            }
            it.Dispose();
            _dispatcher.Invoke((Action)delegate () { _folders.RemoveAt(_folders.Count - 1); });
        }

        public void ReadDateTimesAsync(Folder folder, HistoryEditor editor = null)
        {
            _editor = editor;
            _folder = folder;
            var w = new BackgroundWorker();
            w.DoWork += ReadFoldersAndFiles;
            w.RunWorkerAsync();
        }

        public void ReadDateTimes(Folder folder, HistoryEditor editor = null)
        {
            _editor = editor;
            _folder = folder;

            var w = new BackgroundWorker();

            LoadFolders();
            LoadFiles();
            _dispatcher.Invoke((Action)delegate () { _folder.Folders.RemoveAt(0); });
        }
        public void Refresh(ObservableCollection<Folder> folders)
        {
            _folders = folders;
            var w = new BackgroundWorker();
            w.DoWork += Refresh;
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
                    dateTime[i - 1] = int.Parse(path[i].Name);
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
                    var it = _dbase.CreateIterator();
                    foreach (var key in keys)
                    {
                        it.Seek(key);
                        if (!it.IsValid())
                            break;
                        var getedKey = it.GetKey();
                        try
                        {
                            if (HistoryDatabaseFuncs.ValidateKeyByKey(getedKey, key, true, path.Count, false, true))
                            {
                                _dispatcher.Invoke((Action)delegate () { _folder.Folders.Add(new Folder(DT.ToString())); _folder.Folders[_folder.Folders.Count - 1].Parent = _folder; });
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
            if (path.Count >= 4)
            {
                int[] dateTime = { 2000, 1, 1, 0, 0 };
                for (int i = 1; i < path.Count; i++)
                {
                    dateTime[i - 1] = int.Parse(path[i].Name);
                }
                int curDateInd = path.Count - 1;
                string[] names = { };
                List<byte[]> keys = new List<byte[]>();
                switch (path.Count)
                {
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
                    if (!it.IsValid())
                        continue;
                    var getedKey = it.GetKey();
                    try
                    {
                        while (true)
                        {
                            if (HistoryDatabaseFuncs.ValidateKeyByKey(getedKey, keys[i], true, path.Count - 1, true, true) && it.IsValid())
                            {

                                if (i == 0 || i == 2)
                                {
                                    var chunk = new ChunkFile(names[i] + " file" + (getedKey.Last() > 0 ? ("." + getedKey.Last() + "") : ""), names[i], getedKey[getedKey.Length - 2]);
                                    chunk.Parent = _folder;
                                    _dispatcher.Invoke((Action)delegate () { _folder.Folders.Add(chunk); });

                                    if (_editor != null)
                                        _editor.RebuildMeta(chunk);

                                    it = _dbase.CreateIterator();
                                    it.Seek(getedKey);
                                    _dispatcher.Invoke((Action)delegate () { Application.Current.MainWindow.Activate(); });
                                }
                                else _dispatcher.Invoke((Action)delegate () { _folder.Folders.Add(new MetaFile(names[i] + " meta" + (getedKey.Last() > 0 ? ("." + getedKey.Last() + "") : ""), names[i], getedKey.Last())); _folder.Folders[_folder.Folders.Count - 1].Parent = _folder; });
                                it.Next();
                                if (it.IsValid())
                                {
                                    getedKey = it.GetKey();
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
            _dispatcher.Invoke((Action)delegate () { _folder.Folders.RemoveAt(0); });
        }

        private void Refresh(object sender, DoWorkEventArgs e)
        {
            Folder[] oldFolders = new Folder[_folders.Count()];
            _folders.CopyTo(oldFolders, 0);
            _dispatcher.Invoke((Action)delegate () { _folders.Clear(); });
            if (oldFolders.Count() == 0 || oldFolders[0].Parent == null)
                ReadSymbols(_folders);
            else ReadDateTimes(oldFolders[0].Parent);
        }

        private void RefreshAll(object sender, DoWorkEventArgs e)
        {
            Folder[] oldFolders = new Folder[_folders.Count()];
            _folders.CopyTo(oldFolders, 0);
            _dispatcher.Invoke((Action)delegate () { _folders.Clear(); });
            if (oldFolders.Count() == 0) return;
            if (oldFolders[0].Parent == null)
                ReadSymbols(_folders);
            else ReadDateTimes(oldFolders[0].Parent);
        }



        private void RefreshRecursiveExpand(Folder oldExpandedFolder, Folder newFolder, ObservableCollection<Folder> newFolderCollection)
        {

        }
    }
}
