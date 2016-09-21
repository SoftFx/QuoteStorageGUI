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

namespace QuoteHistoryGUI
{
    class HistoryLoader
    {
        public byte[] SerealizeKey(string sym, string type, string period, int year, int month, int day, int hour, int partNum = 0)
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
     //   private static List<KeyValuePair<int, int>> MinMaxDateTime = new List<KeyValuePair<int, int>> { {1970, }, { }, { } };

        Dispatcher _dispatcher;
        DB _dbase;
        ObservableCollection<Folder> _folders;
        Folder parent;
        public HistoryLoader(Dispatcher dispatcher, DB dbase, ObservableCollection<Folder> folders)
        {
            _dispatcher = dispatcher;
            _dbase = dbase;
            _folders = folders;
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
            _dispatcher.Invoke((Action)delegate () { _folders.RemoveAt(_folders.Count - 1); });
        }


        //Validate only date and SymbolName
        bool ValidateKey(byte[] dbKey, DateTime Date, int validationDateLevel = 1) // 1 -year, 2 -month, ...
        {
            var dp = GetDateAndPart(dbKey);
            List<byte> nextKey = new List<byte>();
            for (int i = 0; i < dbKey.Length; i++)
            {
                if (dbKey[i] > 1)
                    nextKey.Add(dbKey[i]);
                else break;
            }
            string sym = ASCIIEncoding.ASCII.GetString(nextKey.ToArray());

            if (sym != path[0].Name)
                return false;
            if (dp.Key.Year != Date.Year && validationDateLevel > 0)
                return false;

            return true;
        }

        List<Folder> path;
        public void ReadDateTimes(Folder folder)
        {
            _folders = folder.Folders;
            path = new List<Folder>();
            path.Add(folder);
            while (path.Last().Parent != null)
            {
                path.Add(path.Last().Parent);
            }
            path.Reverse();
            var w = new BackgroundWorker();
            w.DoWork += ReadDateTimesWork;
            w.RunWorkerAsync();
        }
        private void ReadDateTimesWork(object sender, DoWorkEventArgs e)
        {
            
            switch (path.Count)
            {
                case 1:
                    {
                        for (int year = 2000; year < 2030; year++) {
                            List<byte[]> keys = new List<byte[]> {
                            SerealizeKey(path[0].Name,"Chunk","ticks",year,1,1,1,0),
                            SerealizeKey(path[0].Name,"Chunk","ticks level2",year,1,1,1,0),
                            SerealizeKey(path[0].Name,"Chunk","M1 bid",year,1,1,1,0),
                            SerealizeKey(path[0].Name,"Chunk","M1 ask",year,1,1,1,0),
                        };
                            var it = _dbase.CreateIterator();
                            foreach (var key in keys)
                            {
                                it.Seek(key);
                                if (!it.IsValid())
                                    break;
                                var getedKey = it.GetKey();
                                if(ValidateKey(getedKey, new DateTime(year,1,1), 1))
                                {
                                    _dispatcher.Invoke((Action)delegate () { _folders.Insert(_folders.Count - 1, new Folder(year.ToString())); _folders[_folders.Count - 2].Parent = parent; });
                                    break;
                                }
                            }
                        }
                        break;
                    }
                case 2:
                case 3:
                case 4:
                case 5:
                default:
                    break;
            }
            _dispatcher.Invoke((Action)delegate () { _folders.RemoveAt(_folders.Count - 1); });
        }

    }
}
