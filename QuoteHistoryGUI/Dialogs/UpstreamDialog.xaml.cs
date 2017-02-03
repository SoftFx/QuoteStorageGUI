using log4net;
using QuoteHistoryGUI.HistoryTools;
using QuoteHistoryGUI.HistoryTools.Interactor;
using QuoteHistoryGUI.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace QuoteHistoryGUI.Dialogs
{
    /// <summary>
    /// Interaction logic for UpstreamDialog.xaml
    /// </summary>
    public partial class UpstreamDialog : Window
    {
        HistoryInteractor _interactor;
        BackgroundWorker UpstreamWorker;
        StorageInstanceModel _source;
        SelectTemplateWorker temW;
        string templateText;
        bool is2levelUpstream = false;
        public static readonly ILog log = LogManager.GetLogger(typeof(StorageSelectionDialog));
        bool canceled = false;
        Dispatcher _dispatcher;
        public UpstreamDialog(StorageInstanceModel source, HistoryInteractor interactor)
        {
            try
            {
                log.Info("Upstream dialog initializing...");
                InitializeComponent();

                _dispatcher = this.Dispatcher;

                Level2Box.IsChecked = true;
                TemplateBox.SetData(source.Folders.Select(f => f.Name), Enumerable.Range(2010, DateTime.Today.Year - 2009).Select(y => y.ToString()),
                    HistoryInteractor.GetTemplates(interactor.Selection));
                _interactor = interactor;
                _interactor.Selection.Clear();
                _source = source;
                log.Info("Upstream dialog initialized");
                this.Closed += OnWindowClosing;
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                throw ex;
            }
        }
        private void UpstreamButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                canceled = false;
                log.Info("Upstream calling...");
                UpstreamWorker = new BackgroundWorker();
                _interactor.Source = _source;
                is2levelUpstream = Level2Box.IsChecked.HasValue && Level2Box.IsChecked.Value;

                temW = new SelectTemplateWorker(_interactor.Source.Folders, new HistoryLoader(Application.Current.MainWindow.Dispatcher, _interactor.Source.HistoryStoreDB));
                templateText = string.Join(";\n", TemplateBox.Templates.Source.Select(t => t.Value));

                UpstreamButton.IsEnabled = false;
                UpstreamWorker.WorkerReportsProgress = true;
                UpstreamWorker.WorkerSupportsCancellation = true;
                UpstreamWorker.DoWork += worker_Upstream;
                UpstreamWorker.ProgressChanged += CopyProgressChanged;
                UpstreamWorker.RunWorkerCompleted += worker_Upstreamed;
                UpstreamWorker.RunWorkerCompleted += QHAppWindowModel.throwExceptions;
                UpstreamWorker.RunWorkerAsync(UpstreamWorker);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                throw ex;
            }
        }

        void FlushWork(BackgroundWorker worker, List<KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>>> saveList, ref int flushCnt, ref DateTime lastReport)
        {
            foreach (var pairForChunk in saveList)
            {
                if (worker?.CancellationPending == true)
                {
                    canceled = true;
                    return;
                }
                flushCnt++;
                _interactor.Source.HistoryStoreDB.Put(new LevelDB.WriteOptions(), pairForChunk.Key.Key, pairForChunk.Key.Value);
                if (worker != null && (DateTime.UtcNow - lastReport).TotalSeconds > 0.25)
                {
                    worker.ReportProgress(1, "[" + flushCnt + "] " + "Flushing");
                    lastReport = DateTime.UtcNow;
                }
            }
            foreach (var pairForMeta in saveList)
            {
                if (worker?.CancellationPending == true)
                {
                    canceled = true;
                    return;
                }
                flushCnt++;
                _interactor.Source.HistoryStoreDB.Put(new LevelDB.WriteOptions(), pairForMeta.Value.Key, pairForMeta.Value.Value);
                if (worker != null && (DateTime.UtcNow - lastReport).TotalSeconds > 0.25)
                {
                    worker.ReportProgress(1, "[" + flushCnt + "] " + "Flushing");
                    lastReport = DateTime.UtcNow;
                }
            }
            saveList.Clear();
        }

        object listAddLock = new object();
        void level2ToTicksWork(BackgroundWorker worker, IEnumerable<KeyValuePair<byte[], byte[]>> files, ref int upstramCnt, ref int flushCnt, ref DateTime lastReport,
            List<KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>>> saveListTicks, List<HistoryDatabaseFuncs.DBEntry> entriesForM1Update, int degreeOfParallelism = 4)
        {
            foreach (var file in files)
            {
                if (worker?.CancellationPending == true)
                {
                    canceled = true;
                    return;
                }
                Interlocked.Increment(ref upstramCnt);
                var entry = HistoryDatabaseFuncs.DeserealizeKey(file.Key);
                if (worker != null && (DateTime.UtcNow - lastReport).TotalSeconds > 0.5)
                {
                    worker.ReportProgress(1, "[" + upstramCnt + "] " + entry.Symbol + "/" + entry.Time.Year + "/" + entry.Time.Month+ "/" + entry.Time.Day + "/" + entry.Time.Hour + "/" + entry.Period + "." + entry.Part);
                    lastReport = DateTime.UtcNow;
                }

                var items = HistorySerializer.Deserialize("ticks level2", _interactor.Source.Editor.GetOrUnzip(file.Value), degreeOfParallelism);
                var itemsList = new List<QHItem>();
                var ticksLevel2 = items as IEnumerable<QHTickLevel2>;
                var ticks = _interactor.Source.Editor.GetTicksFromLevel2(ticksLevel2);
                var content = HistorySerializer.Serialize((IEnumerable<QHItem>)(ticks));

                entry.Period = "ticks";

                if (entriesForM1Update.Count == 0 || entriesForM1Update.Last().Time.Year != entry.Time.Year ||
                    entriesForM1Update.Last().Time.Month != entry.Time.Month || entriesForM1Update.Last().Time.Day != entry.Time.Day)
                {
                    entriesForM1Update.Add(new HistoryDatabaseFuncs.DBEntry(entry.Symbol, new DateTime(entry.Time.Year, entry.Time.Month, entry.Time.Day), entry.Period, "chunk", 0));
                }

                saveListTicks.Add(_interactor.Source.Editor.GetChunkMetaForDB(content, entry));
                if (saveListTicks.Count > 1024)
                {
                    FlushWork(worker, saveListTicks, ref flushCnt, ref lastReport);
                }
            }
        }

        void ticksToM1Work(BackgroundWorker worker, IEnumerable<HistoryDatabaseFuncs.DBEntry> entriesForM1Update, ref int upstramCnt, ref int flushCnt, ref DateTime lastReport,
            List<KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>>> saveListBids, List<KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>>> saveListAsks)
        {

            foreach (var entry in entriesForM1Update)
            {

                if (worker != null && (DateTime.UtcNow - lastReport).TotalSeconds > 0.5)
                {
                    worker.ReportProgress(1, "[" + upstramCnt + "] " + entry.Symbol + "/" + entry.Time.Year + "/"+entry.Time.Month+"/" + entry.Time.Day + "/" + entry.Time.Hour + "/" + entry.Period + "." + entry.Part);
                    lastReport = DateTime.UtcNow;
                }
                if (worker?.CancellationPending == true)
                {
                    canceled = true;
                    return;
                }
                upstramCnt++;
                var file = _interactor.Source.Editor.ReadAllPart(entry, HistoryEditor.hourReadMode.allDate);
                var items = HistorySerializer.Deserialize("ticks", file);
                var itemsList = new List<QHItem>();
                var ticks = items as IEnumerable<QHTick>;
                var bars = _interactor.Source.Editor.GetM1FromTicks(ticks);
                var contentBid = HistorySerializer.Serialize(bars.Key);
                var contentAsk = HistorySerializer.Serialize(bars.Value);
                var bidEntry = new HistoryDatabaseFuncs.DBEntry(entry.Symbol, entry.Time, "M1 bid", "Chunk", 0);
                var askEntry = new HistoryDatabaseFuncs.DBEntry(entry.Symbol, entry.Time, "M1 ask", "Chunk", 0);
                saveListBids.Add(_interactor.Source.Editor.GetChunkMetaForDB(contentBid, bidEntry));
                saveListAsks.Add(_interactor.Source.Editor.GetChunkMetaForDB(contentAsk, askEntry));

                if (saveListBids.Count > 1024)
                {
                    FlushWork(worker, saveListBids, ref flushCnt, ref lastReport);
                }

                if (saveListAsks.Count > 1024)
                {
                    FlushWork(worker, saveListAsks, ref flushCnt, ref lastReport);
                }
            }
        }

        private void worker_Upstream(object sender, DoWorkEventArgs e)
        {
            try
            {
                var templates = templateText.Split(new[] { ";\n" }, StringSplitOptions.None);
                BackgroundWorker worker = e.Argument as BackgroundWorker;
                var templNum = 0;
                int flushCnt = 0;
                List<KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>>> saveListTicks = new List<KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>>>();
                List<KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>>> saveListBids = new List<KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>>>();
                List<KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>>> saveListAsks = new List<KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>>>();
                int degreeOfParallelism = 1;
                _dispatcher.Invoke(delegate
                {
                    var item = DegreeBox.SelectedItem as ComboBoxItem;
                    var textBlock = item.Content as TextBlock;
                    degreeOfParallelism = int.Parse(textBlock.Text); });
                

                List<HistoryDatabaseFuncs.DBEntry> entriesForM1Update = new List<HistoryDatabaseFuncs.DBEntry>(); 
                foreach (var templ in templates)
                {
                    templNum++;
                    var matched = temW.GetByMatch(templ, worker);
                    DateTime lastReport = DateTime.UtcNow.AddSeconds(-2);
                    int upstramCnt = 0;
                    foreach (var sel in matched)
                    {
                        var files = _interactor.Source.Editor.EnumerateFilesInFolder(sel, new List<string>() { "ticks level2" }, new List<string>() { "Chunk" });


                        level2ToTicksWork(worker, files, ref upstramCnt, ref flushCnt, ref lastReport, saveListTicks, entriesForM1Update, degreeOfParallelism);

                        FlushWork(worker, saveListTicks, ref flushCnt, ref lastReport);

                        if (!is2levelUpstream)
                        {
                            entriesForM1Update.Clear();
                            files = _interactor.Source.Editor.EnumerateFilesInFolder(sel, new List<string>() { "ticks" }, new List<string>() { "Chunk" });
                            foreach (var file in files)
                            {
                                var entry = HistoryDatabaseFuncs.DeserealizeKey(file.Key);
                                if (entriesForM1Update.Count == 0 || entriesForM1Update.Last().Time.Year != entry.Time.Year ||
                                    entriesForM1Update.Last().Time.Month != entry.Time.Month || entriesForM1Update.Last().Time.Day != entry.Time.Day)
                                {
                                    entriesForM1Update.Add(new HistoryDatabaseFuncs.DBEntry(entry.Symbol, new DateTime(entry.Time.Year, entry.Time.Month, entry.Time.Day), entry.Period, "chunk", 0));
                                }
                            }
                        }
                        ticksToM1Work(worker, entriesForM1Update, ref upstramCnt, ref flushCnt, ref lastReport, saveListBids, saveListAsks);
                        entriesForM1Update.Clear();
                    }

                    FlushWork(worker, saveListBids, ref flushCnt, ref lastReport);
                    FlushWork(worker, saveListAsks, ref flushCnt, ref lastReport);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message + ",\nStackTrace: " + ex.StackTrace, "Upstream error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            _interactor.Source.Refresh();
        }
        private void CopyProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            UpstreamStatusBlock.Text = e.UserState as string;
            log.Info("Upstream progress report: " + e.UserState as string);
        }
        private void worker_Upstreamed(object sender, RunWorkerCompletedEventArgs e)
        {
            if (!canceled)
            {
                _dispatcher.Invoke(delegate
                { MessageBox.Show(this, "Upstream update completed", "Result", MessageBoxButton.OK, MessageBoxImage.Asterisk); });
                log.Info("Upstream performed");
            }
            Close();
            UpstreamButton.IsEnabled = true;
        }


        private void templateHelpButton_Click(object sender, RoutedEventArgs e)
        {
            HelpDialog.ShowHelp("upstream");
        }

        public void OnWindowClosing(object sender, EventArgs e)
        {
            if (UpstreamWorker != null && UpstreamWorker.IsBusy)
            {
                canceled = true;
                UpstreamWorker.CancelAsync();
                _dispatcher.Invoke(delegate
                { MessageBox.Show(this, "Canceled!", "Close message", MessageBoxButton.OK, MessageBoxImage.Asterisk); });

                log.Info("Upstream canceled");
            }
        }

    }
}
