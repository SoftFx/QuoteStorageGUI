using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace QuoteHistoryGUI
{
    public class Folder : INotifyPropertyChanged
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
        public bool Loaded = false;
        public Folder()
        {
            this.HasChild = false;
        }
        public Folder(string name)
        {
            this.Name = name;
            this.Folders = new ObservableCollection<Folder>() { new LoadingFolder()};
            this.HasChild = false;
        }

        public Folder(Folder parent)
        {
            this.Name = string.Empty;
            this.Folders = new ObservableCollection<Folder>();
            this.HasChild = false;
            this.Parent = parent;
        }

        public bool HasChild { get; set; }

        public string Name { get; set; }

        private ObservableCollection<Folder> _folders;
        public ObservableCollection<Folder> Folders {
            get { return _folders; }
            set
            {
                if (_folders == value)
                    return;
                _folders = value;
            }
        }

        public Folder Parent { get; set; }
    }

    public class LoadingFolder: Folder
    {
        public LoadingFolder(string name) : base()
        {
            Name = name;
        }

        public LoadingFolder(): base()
        {
            Name = "Loading";
        }

    }
}
