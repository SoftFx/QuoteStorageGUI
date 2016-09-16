using LevelDB;
using QuoteHistoryGUI.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;


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
        private List<Folder> _folders;
        public List<Folder> Folders {
            get { return _folders; }
            set
            {
                if (_folders == value)
                    return;
                _folders = value;
                NotifyPropertyChanged("Folders");
            }
        }

        public MainWindowModel() {
            Folders = new List<Folder> { new Folder("1"), new Folder("2") };
            Folders[0].Folders = new List<Folder> { new Folder("1"), new Folder("2") };
            Folders[0].Folders[0].Folders = new List<Folder> { new Folder("1"), new Folder("2") };
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
                NotifyPropertyChanged("StoragePath");
                if(value.Length!=0)
                OpenBase(value);
            }
        }


        public ICommand OpenBtnClick { get; private set; }
        private bool OpenBaseDelegate(object o, bool isCheckOnly)
        {
            if(isCheckOnly)
                return true;
            else
            {
                var dlg = new StorageSelectionDialog()
                {
                    Owner = Application.Current.MainWindow
                };
                dlg.ShowDialog();
                StoragePath = dlg.StoragePath.Text;
                return true;
            }
        }

        private void OpenBase(string path)
        {
            Folders = new List<Folder>();
            var _historyStoreDB = new DB(path + "\\HistoryDB",
                    new Options() { BloomFilter = new BloomFilterPolicy(10) });
            SortedSet<string> s = new SortedSet<string>();
            Dictionary<string, Folder> d = new Dictionary<string, Folder>();
            foreach(var entry in _historyStoreDB)
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
                    if (f.Name == year.ToString()) found = true;
                    curFold = f;
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
                    if (f.Name == month.ToString()) found = true;
                    curFold = f;
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
                    if (f.Name == day.ToString()) found = true;
                    curFold = f;
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
                    if (f.Name == hour.ToString()) found = true;
                    curFold = f;
                }
                if (!found)
                {
                    var f = new Folder(hour.ToString());
                    curFold.Folders.Add(f);
                    curFold = f;
                }


            }
            List<Folder> a = new List<Folder>();
            foreach(var folder in d) { a.Add(folder.Value); }
            Folders = a;
            

        }

    }
}
