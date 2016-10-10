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

namespace QuoteHistoryGUI
{
    class HistoryLoader
    {
        public static byte[] SerealizeKey(string sym, string type, string period, int year, int month, int day, int hour, int partNum = 0)
        {
            var _prefix = new byte[sym.Length+2];
            ASCIIEncoding.ASCII.GetBytes(sym).CopyTo(_prefix, 0);
            _prefix[sym.Length] = (byte)(type == "Chunk" ? 1 : 0);
            _prefix[sym.Length + 1] = periodicityDict[period];
            var prefOffset = _prefix.Length;
            byte[] resKey = new byte[prefOffset + 5];
            _prefix.CopyTo(resKey, 0);
            UInt32 date = (uint)year;
            date = date * 100 + (uint)month;
            date = date * 100 + (uint)day;
            date = date * 100 + (uint)hour;
            var bd = BitConverter.GetBytes(date);
            resKey[prefOffset] = bd[3];
            resKey[prefOffset + 1] = bd[2];
            resKey[prefOffset + 2] = bd[1];
            resKey[prefOffset + 3] = bd[0];
            resKey[prefOffset + 4] = (byte)partNum;
            return resKey;
        }

        public KeyValuePair<DateTime, int> GetDateAndPart(byte[] dbKey)
        {
            int i = 0;
            while (dbKey[i] > 1)
                i++;
            i += 2;
            byte part = (byte)(dbKey[i + 4]);
            byte[] dateByte = new byte[4];
            dateByte[0] = dbKey[i + 3];
            dateByte[1] = dbKey[i + 2];
            dateByte[2] = dbKey[i + 1];
            dateByte[3] = dbKey[i];
            UInt32 date = BitConverter.ToUInt32(dateByte, 0);
            int hour = (int)(date % 100);
            date = date / 100;
            int day = (int)(date % 100);
            date = date / 100;
            int month = (int)(date % 100);
            date = date / 100;
            int year = (int)date;

            return new KeyValuePair<DateTime, int>(new DateTime(year, month, day, hour, 0, 0), part);
        }


        static public readonly Dictionary<string, byte> periodicityDict = new Dictionary<string, byte>()
        {
            {"ticks",0 },
            {"ticks level2",1 },
            {"M1 ask",2 },
            {"M1 bid",3 },
        };
        private static List<string> StoredPeriodicities = new List<string>() {"ticks", "ticks level2", "M1 bid", "M1 ask" };
        private static List<KeyValuePair<int, int>> MinMaxDateTime = new List<KeyValuePair<int, int>> { new KeyValuePair<int, int>(2000, 2030),
            new KeyValuePair<int, int>(1, 12),
            new KeyValuePair<int, int>(1, 31),
            new KeyValuePair<int, int>(0, 23),
        new KeyValuePair<int, int>(0, 0)};

        Dispatcher _dispatcher;
        DB _dbase;
        ObservableCollection<Folder> _folders;
        Folder parent;
        List<Folder> path;
        HistoryEditor _editor;

        public HistoryLoader(Dispatcher dispatcher, DB dbase, ObservableCollection<Folder> folders, Folder par = null)
        {
            _dispatcher = dispatcher;
            _dbase = dbase;
            _folders = folders;
            parent = par;
        }
        public void ReadSymbols()
        {
            var w = new BackgroundWorker();
            w.DoWork += ReadSymbolsWork;
            w.RunWorkerAsync();
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
        bool ValidateKeyByKey(byte[] key1, byte[] key2, bool validateSymbol = true, int validationDateLevel = 1, bool validateType = false, bool validatePeriod = false, bool validatePart = false)
        {
            List<byte> sym1Bytes = new List<byte>();
            for (int i = 0; i < key1.Length; i++)
            {
                if (key1[i] > 1)
                    sym1Bytes.Add(key1[i]);
                else break;
            }
            List<byte> sym2Bytes = new List<byte>();
            for (int i = 0; i < key2.Length; i++)
            {
                if (key2[i] > 1)
                    sym2Bytes.Add(key2[i]);
                else break;
            }
            string sym1 = ASCIIEncoding.ASCII.GetString(sym1Bytes.ToArray());
            string sym2 = ASCIIEncoding.ASCII.GetString(sym2Bytes.ToArray());
            if (sym1 != sym2 && validateSymbol)
                return false;
            var dp1 = GetDateAndPart(key1);
            var dp2 = GetDateAndPart(key2);

            if (dp1.Key.Year != dp2.Key.Year && validationDateLevel > 0)
                return false;
            if (dp1.Key.Month != dp2.Key.Month && validationDateLevel > 1)
                return false;
            if (dp1.Key.Day != dp2.Key.Day && validationDateLevel > 2)
                return false;
            if (dp1.Key.Hour != dp2.Key.Hour && validationDateLevel > 3)
                return false;
            if (dp1.Value != dp2.Value && validatePart)
                return false;
            if (key1[sym1.Length] != key2[sym2.Length] && validateType)
                return false;
            if (key1[sym1.Length+1] != key2[sym2.Length+1] && validatePeriod)
                return false;
            return true;
        }
        public void ReadDateTimes(Folder folder, HistoryEditor editor = null)
        {
            _editor = editor;
            path = new List<Folder>();
            path.Add(folder);
            while (path.Last().Parent != null)
            {
                path.Add(path.Last().Parent);
            }
            path.Reverse();
            var w = new BackgroundWorker();
            w.DoWork += ReadFoldersAndFiles;
            w.RunWorkerAsync();
        }
        public void Refresh()
        {
            var w = new BackgroundWorker();
            w.DoWork += Refresh;
            w.RunWorkerAsync();
        }
        private void LoadFolders()
        {
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
                            SerealizeKey(path[0].Name,"Meta","ticks",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),
                            SerealizeKey(path[0].Name,"Meta","ticks level2",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),
                            SerealizeKey(path[0].Name,"Chunk","ticks",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),
                            SerealizeKey(path[0].Name,"Chunk","ticks level2",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),

                        };
                    if (path.Count < 4)
                    {
                        keys.Add(SerealizeKey(path[0].Name, "Meta", "M1 bid", dateTime[0], dateTime[1], dateTime[2], dateTime[3], 0));
                        keys.Add(SerealizeKey(path[0].Name, "Meta", "M1 ask", dateTime[0], dateTime[1], dateTime[2], dateTime[3], 0));
                        keys.Add(SerealizeKey(path[0].Name, "Chunk", "M1 bid", dateTime[0], dateTime[1], dateTime[2], dateTime[3], 0));
                        keys.Add(SerealizeKey(path[0].Name, "Chunk", "M1 ask", dateTime[0], dateTime[1], dateTime[2], dateTime[3], 0));
                    }
                    var it = _dbase.CreateIterator();
                    foreach (var key in keys)
                    {
                        it.Seek(key);
                        if (!it.IsValid())
                            continue;
                        var getedKey = it.GetKey();
                        try
                        {
                            if (ValidateKeyByKey(getedKey, key, true, path.Count, false, true))
                            {
                                _dispatcher.Invoke((Action)delegate () { _folders.Add(new Folder(DT.ToString())); _folders[_folders.Count - 1].Parent = parent; });
                                break;
                            }
                        }
                        catch (Exception) { }

                    }
                    it.Dispose();
                }
            }
        }
        private void LoadFiles()
        {
            if (path.Count >=4)
            {
                int[] dateTime = { 2000, 1, 1, 0, 0};
                for (int i = 1; i < path.Count; i++)
                {
                    dateTime[i - 1] = int.Parse(path[i].Name);
                }
                int curDateInd = path.Count - 1;
                string[] names = { };
                    List<byte[]> keys = new List<byte[]>();
                    switch(path.Count)
                    {
                        case 4:
                            {
                                names = new string[]{ "M1 bid", "M1 bid", "M1 ask", "M1 ask" };
                                keys = new List<byte[]> {
                                    SerealizeKey(path[0].Name,"Chunk","M1 bid",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),
                                    SerealizeKey(path[0].Name,"Meta","M1 bid",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),
                                    SerealizeKey(path[0].Name,"Chunk","M1 ask",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),
                                    SerealizeKey(path[0].Name,"Meta","M1 ask",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),
                                };
                                break;
                            }
                        case 5:
                            {
                                names = new string[] { "ticks level2", "ticks level2", "ticks", "ticks",  };
                                keys = new List<byte[]> {
                                    SerealizeKey(path[0].Name,"Chunk","ticks level2",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),
                                    SerealizeKey(path[0].Name,"Meta","ticks level2",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),
                                    SerealizeKey(path[0].Name,"Chunk","ticks",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),
                                    SerealizeKey(path[0].Name,"Meta","ticks",dateTime[0],dateTime[1],dateTime[2],dateTime[3],0),

                                };
                                break;
                            }
                    }
                    var it = _dbase.CreateIterator();
                    for(int i = 0; i<keys.Count; i++)
                    {
                        it.Seek(keys[i]);
                        if (!it.IsValid())
                            break;
                        var getedKey = it.GetKey();
                        try
                        {
                            while (true)
                            {
                                if (ValidateKeyByKey(getedKey, keys[i], true, path.Count - 1, true, true) && it.IsValid())
                                {
                                   
                                    if (i==0 || i ==2)
                                    {
                                        var chunk = new ChunkFile(names[i] + " file" + (getedKey.Last() > 0 ? ("(" + getedKey.Last() + ")") : ""), names[i], getedKey.Last());
                                        chunk.Parent = parent;
                                        _dispatcher.Invoke((Action)delegate () { _folders.Add(chunk);});
                                        var editor = new HistoryEditor(_dbase); 
                                        editor.RebuildMeta(chunk);
                                        it = _dbase.CreateIterator();
                                        it.Seek(keys[i]);
                                        _dispatcher.Invoke((Action)delegate () { Application.Current.MainWindow.Activate(); });
                                    }
                                    else _dispatcher.Invoke((Action)delegate () { _folders.Add(new MetaFile(names[i] + " meta" + (getedKey.Last() > 0 ? ("(" + getedKey.Last() + ")") : ""), names[i], getedKey.Last())); _folders[_folders.Count - 1].Parent = parent; });
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
            _dispatcher.Invoke((Action)delegate () {  _folders.RemoveAt(0); });
        }

        private void Refresh(object sender, DoWorkEventArgs e)
        {
            Folder[] oldFolders = new Folder[_folders.Count()];
            _folders.CopyTo(oldFolders, 0);
            _dispatcher.Invoke((Action)delegate () { _folders.Clear(); });
            ReadSymbols();
        }

        private void RefreshRecursiveExpand(Folder oldExpandedFolder, Folder newFolder, ObservableCollection<Folder> newFolderCollection)
        {

        }
    }
}
