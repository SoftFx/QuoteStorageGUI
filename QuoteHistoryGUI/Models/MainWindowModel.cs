using LevelDB;
using QuoteHistoryGUI.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    class MainWindowModel : INotifyPropertyChanged
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
        private ObservableCollection<Folder> _folders;
        public ObservableCollection<Folder> Folders {
            get { return _folders; }
            set
            {
                if (_folders == value)
                    return;
                _folders = value;
                NotifyPropertyChanged("Folders");
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

        public MainWindowModel() {
            Folders = new ObservableCollection<Folder> { new Folder("1"), new Folder("2") };
            Folders[0].Folders = new ObservableCollection<Folder> { new Folder("1"), new LoadingFolder() };
            Folders[0].Folders[0].Folders = new ObservableCollection<Folder> { new Folder("1"), new LoadingFolder() };
            OpenBtnClick = new SingleDelegateCommand(OpenBaseDelegate);
        }

        private string _storagePath;
        public string StoragePath
        {
            get { return _storagePath; }
            set
            {
                if (_storagePath == value)
                    return;
                _storagePath = value;
                OpenBase(_storagePath);

            }
        }
        public void Expand(object sender, RoutedEventArgs e)
        {
            var it = (TreeViewItem)(e.OriginalSource);
            Folder f = it.Header as Folder;
            if (!f.Loaded)
            {
                f.Loaded = true;
                var ha = new HistoryLoader(Application.Current.Dispatcher, _historyStoreDB, Folders);
                ha.ReadDateTimes(f);
            }
        }

        DB _historyStoreDB;
        public ICommand OpenBtnClick { get; private set; }
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
                StoragePath = dlg.StoragePath.Text;
                int t = Folders.Count;
               

                return true;
            }
        }

        private void OpenBase(string path)
        {
            Folders = new ObservableCollection<Folder>();
            _historyStoreDB = new DB(path + "\\HistoryDB",
                    new Options() { BloomFilter = new BloomFilterPolicy(10) });
            SortedSet<string> s = new SortedSet<string>();
            Dictionary<string, Folder> d = new Dictionary<string, Folder>();
            HistoryLoader hl = new HistoryLoader(Application.Current.Dispatcher, _historyStoreDB, Folders);
            hl.ReadSymbols(); 
            /*
                foreach (var entry in _historyStoreDB)
            {
                int i = 0;
                while (entry.Key[i] > 2)
                    i++;
                string sym = ASCIIEncoding.ASCII.GetString(entry.Key).Substring(0, i);
                Folder fold;
                if (d.ContainsKey(sym)) fold = d[sym];
                else
                {
                    fold = new Folder(sym);
                    d[sym] = fold;
                }



                var prefOffset = sym.Length + 2;
                byte part = (byte)(entry.Key[prefOffset + 4]);
                byte[] dateByte = new byte[4];
                dateByte[0] = entry.Key[prefOffset + 3];
                dateByte[1] = entry.Key[prefOffset + 2];
                dateByte[2] = entry.Key[prefOffset + 1];
                dateByte[3] = entry.Key[prefOffset];
                UInt32 date = BitConverter.ToUInt32(dateByte, 0);
                int hour = (int)(date % 100);
                date = date / 100;
                int day = (int)(date % 100);
                date = date / 100;
                int month = (int)(date % 100);
                date = date / 100;
                int year = (int)date;
                bool found;

                Folder curFold = fold;

                found = false;
                foreach(var f in curFold.Folders)
                {
                    if (f.Name == year.ToString())
                    {
                        found = true;
                        curFold = f;
                    }
                }
                if(!found)
                {
                    var f = new Folder(year.ToString());
                    curFold.Folders.Add(f);
                    curFold = f;
                }

                found = false;
                foreach (var f in curFold.Folders)
                {
                    if (f.Name == month.ToString())
                    {
                        found = true;
                        curFold = f;
                    }
                }
                if (!found)
                {
                    var f = new Folder(month.ToString());
                    curFold.Folders.Add(f);
                    curFold = f;
                }

                found = false;
                foreach (var f in curFold.Folders)
                {
                    if (f.Name == day.ToString())
                    {
                        found = true;
                        curFold = f;
                    }
                }
                if (!found)
                {
                    var f = new Folder(day.ToString());
                    curFold.Folders.Add(f);
                    curFold = f;
                }
                found = false;
                foreach (var f in curFold.Folders)
                {
                    if (f.Name == hour.ToString())
                    {
                        found = true;
                        curFold = f;
                    }
                }
                if (!found)
                {
                    var f = new Folder(hour.ToString());
                    curFold.Folders.Add(f);
                    curFold = f;
                }


            }
            ObservableCollection<Folder> a = new ObservableCollection<Folder>();
            foreach(var folder in d) { a.Add(folder.Value); }
            Folders = a;
            */

        }
        public event EventHandler ProgressUpdate;
        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;


            Folders = new ObservableCollection<Folder>();
            //Folders.Add(new LoadingFolder());

            Application.Current.Dispatcher.Invoke((Action)delegate () {  });
            for (int i = 0; i < 30; i++)
            {
                if (ProgressUpdate != null)
                    ProgressUpdate(this, e);
                Thread.Sleep(200);
                Application.Current.Dispatcher.Invoke((Action)delegate () { Folders.Insert(Folders.Count-1, new Folder(i.ToString())); });
                
                FoldersCnt++;

            }

            Application.Current.Dispatcher.Invoke((Action)delegate () { Folders.RemoveAt( Folders.Count-1); });
            Application.Current.Dispatcher.Invoke((Action)delegate () { Folders[0].Folders.Insert(0, new LoadingFolder()); });
            for (int i = 0; i < 30; i++)
            {
                if (ProgressUpdate != null)
                    ProgressUpdate(this, e);
                Thread.Sleep(500);
                Application.Current.Dispatcher.Invoke((Action)delegate () { Folders[0].Folders.Insert(Folders[0].Folders.Count - 1, new Folder(i.ToString())); });

                FoldersCnt++;

            }
            Application.Current.Dispatcher.Invoke((Action)delegate () { Folders[0].Folders.RemoveAt(Folders[0].Folders.Count - 1); });
        }

        void TestInit()
        {
        }

    }
}
