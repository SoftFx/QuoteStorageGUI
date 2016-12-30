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
        StorageInstanceModel storage;
        public StorageExplorerView()
        {
            InitializeComponent();
            var a = this.DataContext;
            this.treeView.AddHandler(TreeViewItem.KeyDownEvent, new KeyEventHandler(treeView_KeyDown), true);
            //this.treeView.AddHandler(TreeViewItem.MouseLeftButtonDownEvent, new MouseButtonEventHandler(treeView_MouseLeftButtonDown), true);
        }
        private void treeView_Expanded(object sender, RoutedEventArgs e)
        {
            treeColumn.Width = new GridLength(1, GridUnitType.Auto);
            var tree = sender as TreeView;
            var tab = tree.DataContext as StorageInstanceModel;
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
                var tab = tree.DataContext as StorageInstanceModel;
                var chunk = tree.SelectedItem as ChunkFile;
                if (chunk != null && tab != null)
                {
                    var wind = Application.Current.MainWindow as QHAppWindowView;
                    wind.ShowLoading();
                    tab.OpenChunk(chunk);
                    Dispatcher.BeginInvoke(new Action(() => { wind.HideLoading(); tree.Focus(); }), DispatcherPriority.ContextIdle, null);
                }
                var meta = tree.SelectedItem as MetaFile;
                if (meta != null && tab != null)
                {
                    var wind = Application.Current.MainWindow as QHAppWindowView;
                    wind.ShowLoading();
                    tab.OpenMeta(meta);
                    Dispatcher.BeginInvoke(new Action(() => { wind.HideLoading(); tree.Focus(); }), DispatcherPriority.ContextIdle, null);
                }
            }
        }

        private void OnKey(object sender, KeyEventArgs e)
        {

            var tree = sender as TreeView;
            if (tree != null && e.Key == Key.Enter)
            {
                var tab = tree.DataContext as StorageInstanceModel;
                var chunk = tree.SelectedItem as ChunkFile;
                if (chunk != null && tab != null)
                {
                    var wind = Application.Current.MainWindow as QHAppWindowView;
                    wind.ShowLoading();
                    tab.OpenChunk(chunk);
                    Dispatcher.BeginInvoke(new Action(() => { wind.HideLoading(); tree.Focus(); }), DispatcherPriority.ContextIdle, null);
                }
                var meta = tree.SelectedItem as MetaFile;
                if (meta != null && tab != null)
                {
                    var wind = Application.Current.MainWindow as QHAppWindowView;
                    wind.ShowLoading();
                    tab.OpenMeta(meta);
                    Dispatcher.BeginInvoke(new Action(() => { wind.HideLoading(); tree.Focus(); }), DispatcherPriority.ContextIdle, null);
                }
            }
        }




        List<TreeViewItem> selectedItems = new List<TreeViewItem>();
        object selectedItemLock = new object();
        bool isShiftSelect = false;






        private void UpStream_Click(object sender, RoutedEventArgs e)
        {
            var inst = this.DataContext as StorageInstanceModel;
            var wind = Application.Current.MainWindow as QHAppWindowView;

        }

        private void treeView_KeyDown(object sender, RoutedEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.Down) || Keyboard.IsKeyDown(Key.Up) || Keyboard.IsKeyDown(Key.Left) || Keyboard.IsKeyDown(Key.Right))
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) || Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    lock (selectedItemLock)
                    {
                        var treeItem = e.OriginalSource as TreeViewItem;

                        if (selectedItems.Contains(treeItem) && isShiftSelect)
                        {
                            treeItem.Background = SystemColors.WindowBrush;
                            selectedItems.Remove(treeItem);
                        }
                        else
                        {
                            treeItem.Background = SystemColors.ActiveCaptionBrush;
                            selectedItems.Add(treeItem);
                            isShiftSelect = true;
                        }
                        if (selectedItems.Select(t => { return t.Header as Folder; }).Contains(treeView.SelectedItem as Folder))
                        {
                            var resources = treeView.Resources as ResourceDictionary;
                            resources[SystemColors.HighlightBrushKey] = SystemColors.ActiveCaptionBrush;
                        }
                        else
                        {
                            var resources = treeView.Resources as ResourceDictionary;
                            resources[SystemColors.HighlightBrushKey] = new LinearGradientBrush(new Color() { A = 255, R = 217, G = 244, B = 255 }, new Color() { A = 255, R = 155, G = 221, B = 251 }, new Point(0, 0), new Point(0, 1));
                        }
                    }
                }
                else
                {
                    lock (selectedItemLock)
                    {

                        isShiftSelect = false;
                        var treeItem = e.OriginalSource as TreeViewItem;
                        foreach (var t in selectedItems)
                        {
                            t.Background = SystemColors.WindowBrush;
                        }
                        selectedItems.Clear();
                        var resources = treeView.Resources as ResourceDictionary;
                        resources[SystemColors.HighlightBrushKey] = new LinearGradientBrush(new Color() { A = 255, R = 217, G = 244, B = 255 }, new Color() { A = 255, R = 155, G = 221, B = 251 }, new Point(0, 0), new Point(0, 1));
                        //selectedItems.Add(treeItem);
                    }
                }
            (this.DataContext as StorageInstanceModel).Selection = new List<Folder>(selectedItems.Select(t => { return t.DataContext as Folder; }));
            /*
                        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) || Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                        {
                            var item = e.OriginalSource as TreeViewItem;
                            item.Background = SystemColors.ActiveCaptionBrush;
                            e.Handled = true;
                            var resources = treeView.Resources as ResourceDictionary;
                            resources[SystemColors.HighlightBrushKey] = new LinearGradientBrush(new Color() { A = 255, R = 217, G = 244, B = 255 }, new Color() { A = 255, R = 155, G = 221, B = 251 }, new Point(0, 0), new Point(0, 1));
                        }
            */
        }

        private void treeView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) || Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                lock (selectedItemLock)
                {
                    var treeItem = e.OriginalSource as TreeViewItem;

                    if (selectedItems.Contains(treeItem) && isShiftSelect)
                    {
                        treeItem.Background = SystemColors.WindowBrush;
                        selectedItems.Remove(treeItem);
                    }
                    else
                    {
                        treeItem.Background = SystemColors.ActiveCaptionBrush;
                        selectedItems.Add(treeItem);
                        isShiftSelect = true;
                    }
                    if (selectedItems.Select(t => { return t.Header as Folder; }).Contains(treeView.SelectedItem as Folder))
                    {
                        var resources = treeView.Resources as ResourceDictionary;
                        resources[SystemColors.HighlightBrushKey] = SystemColors.ActiveCaptionBrush;
                    }
                    else
                    {
                        var resources = treeView.Resources as ResourceDictionary;
                        resources[SystemColors.HighlightBrushKey] = new LinearGradientBrush(new Color() { A = 255, R = 217, G = 244, B = 255 }, new Color() { A = 255, R = 155, G = 221, B = 251 }, new Point(0, 0), new Point(0, 1));
                    }
                }
            }
            else
            {
                lock (selectedItemLock)
                {

                    isShiftSelect = false;
                    var treeItem = e.OriginalSource as TreeViewItem;
                    foreach (var t in selectedItems)
                    {
                        t.Background = SystemColors.WindowBrush;
                    }
                    selectedItems.Clear();
                    var resources = treeView.Resources as ResourceDictionary;
                    resources[SystemColors.HighlightBrushKey] = new LinearGradientBrush(new Color() { A = 255, R = 217, G = 244, B = 255 }, new Color() { A = 255, R = 155, G = 221, B = 251 }, new Point(0, 0), new Point(0, 1));
                    //selectedItems.Add(treeItem);
                }
            }
            (this.DataContext as StorageInstanceModel).Selection = new List<Folder>(selectedItems.Select(t => { return t.DataContext as Folder; }));
        }


        private void treeViewItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) || Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                lock (selectedItemLock)
                {
                    var treeItem = sender as TreeViewItem;

                    if (selectedItems.Contains(treeItem) && isShiftSelect)
                    {
                        treeItem.Background = SystemColors.WindowBrush;
                        selectedItems.Remove(treeItem);
                    }
                    else
                    {
                        treeItem.Background = SystemColors.ActiveCaptionBrush;
                        selectedItems.Add(treeItem);
                        isShiftSelect = true;
                    }
                    if (selectedItems.Select(t => { return t.Header as Folder; }).Contains(treeView.SelectedItem as Folder))
                    {
                        var resources = treeView.Resources as ResourceDictionary;
                        resources[SystemColors.HighlightBrushKey] = SystemColors.ActiveCaptionBrush;
                        MessageBox.Show("SystemColors.ActiveCaptionBrush");
                    }
                    else
                    {
                        var resources = treeView.Resources as ResourceDictionary;
                        resources[SystemColors.HighlightBrushKey] = new LinearGradientBrush(new Color() { A = 255, R = 217, G = 244, B = 255 }, new Color() { A = 255, R = 155, G = 221, B = 251 }, new Point(0, 0), new Point(0, 1));
                    }
                }
            }
            else
            {
                lock (selectedItemLock)
                {

                    isShiftSelect = false;
                    var treeItem = e.OriginalSource as TreeViewItem;
                    foreach (var t in selectedItems)
                    {
                        t.Background = SystemColors.WindowBrush;
                    }
                    selectedItems.Clear();
                    var resources = treeView.Resources as ResourceDictionary;
                    //resources[SystemColors.HighlightBrushKey] = new LinearGradientBrush(new Color() { A = 255, R = 217, G = 244, B = 255 }, new Color() { A = 255, R = 155, G = 221, B = 251 }, new Point(0, 0), new Point(0, 1));
                    //selectedItems.Add(treeItem);
                }
            }
            (this.DataContext as StorageInstanceModel).Selection = new List<Folder>(selectedItems.Select(t => { return t.DataContext as Folder; }));
        }
    }
}
