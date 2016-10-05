using QuoteHistoryGUI.HistoryTools;
using QuoteHistoryGUI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace QuoteHistoryGUI.Views
{
    /// <summary>
    /// Interaction logic for StorageExplorerView.xaml
    /// </summary>
    public partial class StorageExplorerView : UserControl
    {
        StorageInstance storage;
        public StorageExplorerView()
        {
            InitializeComponent();
        }
        private void treeView_Expanded(object sender, RoutedEventArgs e)
        {
            var tree = sender as TreeView;
            var tab = tree.DataContext as StorageInstance;
            var treeItem = e.OriginalSource as TreeViewItem;
            var folder = treeItem.DataContext as Folder;
            if (folder != null && tab != null)
            {
                tab.Expand(folder);
            }
        }

        private void OnDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var tree = sender as TreeView;
            if (tree != null)
            {
                var tab = tree.DataContext as StorageInstance;
                var chunk = tree.SelectedItem as ChunkFile;
                if (chunk != null && tab != null)
                {
                    var wind = Application.Current.MainWindow as MainWindowView;
                    wind.ShowLoading();
                    tab.OpenChunk(chunk);
                    Dispatcher.BeginInvoke(new Action(() => { wind.HideLoading(); tree.Focus(); }), DispatcherPriority.ContextIdle, null);
                }
                var meta = tree.SelectedItem as MetaFile;
                if (meta != null && tab != null)
                {
                    var wind = Application.Current.MainWindow as MainWindowView;
                    wind.ShowLoading();
                    tab.OpenMeta(meta);
                    Dispatcher.BeginInvoke(new Action(() => { wind.HideLoading(); tree.Focus(); }), DispatcherPriority.ContextIdle, null);
                }
            }
        }

        private void OnKey(object sender, KeyEventArgs e)
        {

            var tree = sender as TreeView;
            if (tree != null && e.Key==Key.Enter)
            {
                var tab = tree.DataContext as StorageInstance;
                var chunk = tree.SelectedItem as ChunkFile;
                if (chunk != null && tab != null)
                {
                    var wind = Application.Current.MainWindow as MainWindowView;
                    wind.ShowLoading();
                    tab.OpenChunk(chunk);
                    Dispatcher.BeginInvoke(new Action(() => { wind.HideLoading(); tree.Focus(); }), DispatcherPriority.ContextIdle, null);
                }
                var meta = tree.SelectedItem as MetaFile;
                if (meta != null && tab != null)
                {
                    var wind = Application.Current.MainWindow as MainWindowView;
                    wind.ShowLoading();
                    tab.OpenMeta(meta);
                    Dispatcher.BeginInvoke(new Action(() => { wind.HideLoading(); tree.Focus(); }), DispatcherPriority.ContextIdle, null);
                }
            }
        }

        
       

        List<TreeViewItem> selectedItems = new List<TreeViewItem>();
        object selectedItemLock = new object();
        private void treeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var source = e.OriginalSource;
        }

        private void treeView_Selected(object sender, RoutedEventArgs e)
        {
            if(HandleSelection)
            {
                return;
            }
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) || Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                lock (selectedItemLock)
                {
                    var treeItem = e.OriginalSource as TreeViewItem;
                    if (e.RoutedEvent == TreeViewItem.SelectedEvent)
                    {
                        if (selectedItems.Contains(treeItem))
                        {
                            HandleSelection = true;
                            treeItem.IsSelected = false;
                            selectedItems.Remove(treeItem);
                            HandleSelection = false;
                        }
                        else
                        {
                            HandleSelection = true;
                            treeItem.IsSelected = true;
                            selectedItems.Add(treeItem);
                            var wind = Application.Current.MainWindow as MainWindowView;
                            var model = wind.DataContext as MainWindowModel;
                            model.Interactor.AddToSelection(treeItem.DataContext as Folder);
                            HandleSelection = false;
                        }
                    }
                    else
                    {
                        HandleSelection = true;
                        if (selectedItems.Contains(treeItem))
                            treeItem.IsSelected = true;
                        else treeItem.IsSelected = false;
                        HandleSelection = false;
                    }
                }
            }
            else
            {
                lock (selectedItemLock)
                {
                    var treeItem = e.OriginalSource as TreeViewItem;
                    foreach (var t in selectedItems)
                    {
                        HandleSelection = true;
                        if (t!=treeItem)
                            t.IsSelected = false;
                        HandleSelection = false;
                    }
                    selectedItems.Clear();
                    var wind = Application.Current.MainWindow as MainWindowView;
                    var model = wind.DataContext as MainWindowModel;
                    model.Interactor.DiscardSelection();
                    selectedItems.Add(treeItem);
                    model.Interactor.AddToSelection(treeItem.DataContext as Folder);
                }
            }
        }

        bool HandleSelection = false;


        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var tree = sender as TreeView;
            if (tree != null)
            {
                var tab = tree.DataContext as StorageInstance;
                var wind = Application.Current.MainWindow as MainWindowView;
                wind.ShowLoading();
                tab.SaveChunk();
                wind.HideLoading();
            }
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            var inst = this.DataContext as StorageInstance;
            inst.CopyBtnClick.Execute(new object());
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var inst = this.DataContext as StorageInstance;
            inst.DeleteBtnClick.Execute(new object());
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            var inst = this.DataContext as StorageInstance;
            inst.Refresh();
        }
    }
}
