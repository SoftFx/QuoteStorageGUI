using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Checksums;
using LevelDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.Windows;
using System.Diagnostics;
using System.ComponentModel;
using QuoteHistoryGUI.HistoryTools.Interactor;
using System.Threading;
using static QuoteHistoryGUI.HistoryTools.HistorySerializer;

namespace QuoteHistoryGUI.HistoryTools
{
    public class HistoryEditor
    {

        public static int MaxCountPerChunk = 131072;

        private DB _dbase;
        public HistoryEditor(DB db)
        {
            _dbase = db;
        }

        public static byte[] GetOrUnzip(byte[] content)
        {
            SerializationMethod serilizer = SerializationMethod.Text;
            if (content == null || content.Count() == 0) return new byte[] { };
            if (content[0] == 'P' && content[1] == 'K')
                serilizer = SerializationMethod.Zip;
            else if (content[0] == 'B' && content[1] == 'Z')
                serilizer = SerializationMethod.BZip;
            else if (content[0] == 'B' && content[1] == 'Q' && content[2] == 'H')
                serilizer = SerializationMethod.Binary;


            if (serilizer == SerializationMethod.Zip)
            {
                MemoryStream data = new MemoryStream(content);
                ZipFile zip = new ZipFile(data);

                foreach (ZipEntry zipEntry in zip)
                {
                    Stream zipStream = zip.GetInputStream(zipEntry);
                    byte[] buffer = new byte[4 * 1024];
                    using (MemoryStream ms = new MemoryStream())
                    {
                        int read;
                        while ((read = zipStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            ms.Write(buffer, 0, read);
                        }
                        content = ms.ToArray();
                    }
                }
                return content;
            }

            if (serilizer == SerializationMethod.BZip)
            {
                using (var source = new MemoryStream(content))
                {

                    using (Bzip2.BZip2InputStream bzipInputStream = new Bzip2.BZip2InputStream(source, false))
                    {

                        byte[] outContent;

                        using (var binaryReader = new BinaryReader(bzipInputStream))
                        {
                            const int bufferSize = 4096;
                            using (var ms = new MemoryStream())
                            {
                                byte[] buffer = new byte[bufferSize];
                                int count;
                                binaryReader.Read(buffer, 0, 3);    //skip header
                                while ((count = binaryReader.Read(buffer, 0, buffer.Length)) != 0)
                                    ms.Write(buffer, 0, count);
                                outContent = ms.ToArray();
                            }
                        }
                        return outContent;
                    }
                }
            }

            if (serilizer == SerializationMethod.Binary)
                return content.Skip(3).ToArray();

            return content;
        }
        public KeyValuePair<SerializationMethod, byte[]> ReadFromDB(HistoryFile f)
        {

            var path = HistoryDatabaseFuncs.GetPath(f);
            int[] dateTime = HistoryDatabaseFuncs.GetFolderStartTime(path);

            string period = f.Period;
            byte[] content = { };
            SerializationMethod serilizer = SerializationMethod.Text;
            if (f as ChunkFile != null)
            {
                var cnt = _dbase.Get(HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Chunk", period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], f.Part));
                if (cnt.Length >= 2 && cnt[0] == 'P' && cnt[1] == 'K')
                    serilizer = SerializationMethod.Zip;
                else
                if (cnt.Length >= 2 && cnt[0] == 'B' && cnt[1] == 'Z')
                    serilizer = SerializationMethod.BZip;
                else
                if (cnt.Length >= 3 && cnt[0] == 'B' && cnt[1] == 'Q' && cnt[2] == 'H')
                    serilizer = SerializationMethod.Binary;

                if (serilizer == SerializationMethod.Binary || serilizer == SerializationMethod.Text)
                {
                    List<byte> res;
                    if (serilizer == SerializationMethod.Binary)
                        res = new List<byte>(cnt.Skip(3));
                    else res = new List<byte>(cnt);
                    int flushPart = 1;
                    while (true)
                    {
                        var cntPart = _dbase.Get(HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Chunk", period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], f.Part, flushPart));
                        if (cntPart == null) break;
                        if (serilizer == SerializationMethod.Binary)
                            res.AddRange(cntPart.Skip(3));
                        else res.AddRange(cntPart);

                        flushPart++;
                    }
                    cnt = res.ToArray();
                }
                var Text = GetOrUnzip(cnt);
                return new KeyValuePair<SerializationMethod, byte[]>(serilizer, Text);
            }
            else
            {
                var meta = _dbase.Get(HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Meta", period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], f.Part));
                // (BitConverter.ToUInt32(dbVal, 0));
                Crc32 hash = new Crc32();
                hash.Value = (BitConverter.ToUInt32(meta, 0));
                var contStr = hash.Value.ToString("X8", CultureInfo.InvariantCulture);
                contStr += '\t';
                contStr += ((SerializationMethod)(meta[4]));
                return new KeyValuePair<SerializationMethod, byte[]>(SerializationMethod.Unknown, ASCIIEncoding.ASCII.GetBytes(contStr));

            }

        }
        public enum ReadMode
        {
            H1AllDate,
            ticksAllDate,
            oneDate
        }


        public byte[] ReadAllPart(HistoryFile f, ReadMode hm = ReadMode.oneDate)
        {

            var path = HistoryDatabaseFuncs.GetPath(f);
            int[] dateTime = HistoryDatabaseFuncs.GetFolderStartTime(path);

            List<byte> result = new List<byte>();
            int hstart = 0;
            int hend = 0;

            int dstart = 0;
            int dend = 0;

            if (hm == ReadMode.oneDate)
            {
                hstart = dateTime[3];
                hend = dateTime[3] + 1;
                dstart = dateTime[2];
                dend = dateTime[2] + 1;
            }
            else
            if (hm == ReadMode.ticksAllDate)
            {
                hstart = 0;
                hend = 24;
                dstart = dateTime[2];
                dend = dateTime[2] + 1;
            }
            else
            if (hm == ReadMode.H1AllDate)
            {
                hstart = 0;
                hend = 1;
                dstart = 1;
                dend = 32;
            }
            for (int day = dstart; day < dend; day++)
            {
                for (int hour = hstart; hour < hend; hour++)
                {
                    for (int i = 0; i < 30; i++)
                    {
                        var cntnt = _dbase.Get(HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Chunk", f.Period, dateTime[0], dateTime[1], day, hour, i));
                        SerializationMethod serilizer = SerializationMethod.Text;
                        if (cntnt.Length >= 2 && cntnt[0] == 'P' && cntnt[1] == 'K')
                            serilizer = SerializationMethod.Zip;
                        else
                        if (cntnt.Length >= 2 && cntnt[0] == 'B' && cntnt[1] == 'Z')
                            serilizer = SerializationMethod.BZip;
                        else
                        if (cntnt.Length >= 3 && cntnt[0] == 'B' && cntnt[1] == 'Q' && cntnt[2] == 'H')
                            serilizer = SerializationMethod.Binary;

                        if (serilizer == SerializationMethod.Binary || serilizer == SerializationMethod.Text)
                        {
                            var cntList = new List<byte>(cntnt);
                            var flushPart = 1;
                            while (true)
                            {
                                var cntFlushPart = _dbase.Get(HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Chunk", f.Period, dateTime[0], dateTime[1], day, hour, i, flushPart));
                                if (cntFlushPart == null) break;
                                else
                                {
                                    if (serilizer == SerializationMethod.Binary)
                                        cntList.AddRange(cntFlushPart.Skip(3));
                                    else cntList.AddRange(cntFlushPart);

                                    flushPart++;
                                }
                            }
                            cntnt = cntList.ToArray();
                        }

                        if (cntnt != null)
                            result.AddRange(GetOrUnzip(cntnt));
                        else break;
                    }
                }
            }
            return result.ToArray();
        }

        public byte[] ReadAllPart(HistoryDatabaseFuncs.DBEntry entry, ReadMode hm = ReadMode.oneDate)
        {

            List<byte> result = new List<byte>();
            int hstart = 0;
            int hend = 0;

            int dstart = 0;
            int dend = 0;

            if (hm == ReadMode.oneDate)
            {
                hstart = entry.Time.Hour;
                hend = entry.Time.Hour + 1;
                dstart = entry.Time.Day;
                dend = entry.Time.Day + 1;
            }
            else
            if (hm == ReadMode.ticksAllDate)
            {
                hstart = 0;
                hend = 24;
                dstart = entry.Time.Day;
                dend = entry.Time.Day + 1;
            }
            else
            if (hm == ReadMode.H1AllDate)
            {
                hstart = 0;
                hend = 1;
                dstart = 1;
                dend = 32;
            }
            for (int day = dstart; day < dend; day++)
            {
                for (int hour = hstart; hour < hend; hour++)
                {
                    for (int i = 0; i < 30; i++)
                    {
                        var cntnt = _dbase.Get(HistoryDatabaseFuncs.SerealizeKey(entry.Symbol, "Chunk", entry.Period, entry.Time.Year, entry.Time.Month, day, hour, i));

                        SerializationMethod serilizer = SerializationMethod.Text;
                        if (cntnt.Length >= 2 && cntnt[0] == 'P' && cntnt[1] == 'K')
                            serilizer = SerializationMethod.Zip;
                        else
                        if (cntnt.Length >= 2 && cntnt[0] == 'B' && cntnt[1] == 'Z')
                            serilizer = SerializationMethod.BZip;
                        else
                        if (cntnt.Length >= 3 && cntnt[0] == 'B' && cntnt[1] == 'Q' && cntnt[2] == 'H')
                            serilizer = SerializationMethod.Binary;

                        if (serilizer == SerializationMethod.Binary || serilizer == SerializationMethod.Text)
                        {
                            var cntList = new List<byte>(cntnt);
                            var flushPart = 1;
                            while (true)
                            {
                                var cntFlushPart = _dbase.Get(HistoryDatabaseFuncs.SerealizeKey(entry.Symbol, "Chunk", entry.Period, entry.Time.Year, entry.Time.Month, day, hour, i, flushPart));
                                if (cntFlushPart == null) break;
                                else
                                {
                                    if (serilizer == SerializationMethod.Binary)
                                        cntList.AddRange(cntFlushPart.Skip(3));
                                    else cntList.AddRange(cntFlushPart);

                                    flushPart++;
                                }
                            }
                            cntnt = cntList.ToArray();
                        }

                        if (cntnt != null)
                            result.AddRange(GetOrUnzip(cntnt));
                        else break;
                    }
                }
            }
            return result.ToArray();
        }

        public int SaveToDBParted(IEnumerable<QHItem> items, ChunkFile file, bool rebuildMeta = true, bool showMessages = true)
        {
            ChunkFile f = new ChunkFile(file.Name, file.Period, 0, file.Parent);
            int part = 0;
            List<QHItem> chunk = new List<QHItem>();
            foreach (var item in items)
            {
                if (chunk.Count == MaxCountPerChunk)
                {
                    f.Part = part;
                    part++;
                    SaveToDB(HistorySerializer.Serialize(chunk), f, showMessages);
                    chunk = new List<QHItem>();
                }
                chunk.Add(item);
            }
            if (chunk.Count != 0) SaveToDB(HistorySerializer.Serialize(chunk), f, showMessages); else part--;
            return part;
        }

        public int CalculatePartCount(IEnumerable<QHItem> items)
        {
            return items.Count() / MaxCountPerChunk + items.Count() % MaxCountPerChunk > 0 ? 1 : 0;
        }

        public void SaveToDB(byte[] content, ChunkFile f, bool showMessages = true)
        {
            QHItem[] items;
            try
            {
                items = HistorySerializer.Deserialize(f.Period, content).ToArray();
            }
            catch (InvalidDataException ex)
            {
                MessageBox.Show("There is a syntax error! Unable to save.\n\n" + ex.Message, "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            catch
            {

                MessageBox.Show("There is a syntax error! Unable to save.\n\n" + "Check the numeric format and punctuation", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.MainWindow.Activate();
                return;
            }

            var path = HistoryDatabaseFuncs.GetPath(f);
            int[] dateTime = HistoryDatabaseFuncs.GetFolderStartTime(path);
            string period = f.Period;
            var meta = _dbase.Get(HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Meta", period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], f.Part));

            var key = HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Chunk", period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], f.Part);
            byte[] value = { };

            if (meta != null && meta[4] == (byte)SerializationMethod.Text)
            {
                value = content;
            }
            if (meta != null && meta[4] == (byte)SerializationMethod.Binary)
            {
                value = HistorySerializer.SerializeBinary(items);
            }
            if (meta != null && meta[4] == (byte)SerializationMethod.BZip)
            {
                value = HistorySerializer.SerializeBinary(items);

                MemoryStream contentMemStream = new MemoryStream(value);
                MemoryStream outputMemStream = new MemoryStream();

                Bzip2.BZip2OutputStream bzipStream = new Bzip2.BZip2OutputStream(outputMemStream);
                int bufSize = 4096;
                byte[] buffer = new byte[bufSize];
                int cnt = 0;
                while (true)
                {
                    cnt = contentMemStream.Read(buffer, 0, bufSize);
                    bzipStream.Write(buffer, 0, cnt);
                    if (cnt < bufSize)
                        break;
                }
                //StreamUtils.Copy(contentMemStream, bzipStream, new byte[4096]);
                bzipStream.Close();
                value = outputMemStream.ToArray();
            }

            if(meta != null && meta[4] == (byte)SerializationMethod.Zip)
            {
                MemoryStream contentMemStream = new MemoryStream(content);
                MemoryStream outputMemStream = new MemoryStream();
                ZipOutputStream zipStream = new ZipOutputStream(outputMemStream);

                zipStream.SetLevel(3); //0-9, 9 being the highest level of compression

                ZipEntry newEntry = new ZipEntry(f.Period + ".txt");
                newEntry.DateTime = DateTime.Now;

                zipStream.PutNextEntry(newEntry);

                StreamUtils.Copy(contentMemStream, zipStream, new byte[4096]);
                zipStream.CloseEntry();

                zipStream.IsStreamOwner = false;    // False stops the Close also Closing the underlying stream.
                zipStream.Close();          // Must finish the ZipOutputStream before using outputMemStream.

                outputMemStream.Position = 0;
                value = outputMemStream.ToArray();
            }

            _dbase.Put(key, value);

            RebuildMeta(f, showMessages);
        }

        public KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>> GetChunkMetaForDB(byte[] content, HistoryDatabaseFuncs.DBEntry entry, bool binary = false)
        {
            var meta = _dbase.Get(HistoryDatabaseFuncs.SerealizeKey(entry.Symbol, "Meta", entry.Period, entry.Time.Year, entry.Time.Month, entry.Time.Day, entry.Time.Hour, entry.Part));
            var key = HistoryDatabaseFuncs.SerealizeKey(entry.Symbol, "Chunk", entry.Period, entry.Time.Year, entry.Time.Month, entry.Time.Day, entry.Time.Hour, entry.Part);
            byte[] value = { };
            SerializationMethod type = SerializationMethod.Zip;
            if (binary)
                type = SerializationMethod.BZip;
            if (meta?[4] == 2)
            {
                type = SerializationMethod.Text;
                if (binary)
                    type = SerializationMethod.Binary;
                value = content;
            }

            if (type == SerializationMethod.BZip)
            {
                MemoryStream contentMemStream = new MemoryStream(content);
                MemoryStream outputMemStream = new MemoryStream();

                Bzip2.BZip2OutputStream bzipStream = new Bzip2.BZip2OutputStream(outputMemStream);
                int bufSize = 4096;
                byte[] buffer = new byte[bufSize];
                int cnt = 0;
                while (true)
                {
                    cnt = contentMemStream.Read(buffer, 0, bufSize);
                    bzipStream.Write(buffer, 0, cnt);
                    if (cnt < bufSize)
                        break;
                }
                //StreamUtils.Copy(contentMemStream, bzipStream, new byte[4096]);
                bzipStream.Close();
                value = outputMemStream.ToArray();
            }

            if (type == SerializationMethod.Zip)
            {
                MemoryStream contentMemStream = new MemoryStream(content);
                MemoryStream outputMemStream = new MemoryStream();
                ZipOutputStream zipStream = new ZipOutputStream(outputMemStream);

                zipStream.SetLevel(3); //0-9, 9 being the highest level of compression

                ZipEntry newEntry = new ZipEntry(entry.Period + ".txt");
                newEntry.DateTime = DateTime.Now;

                zipStream.PutNextEntry(newEntry);

                StreamUtils.Copy(contentMemStream, zipStream, new byte[4096]);
                zipStream.CloseEntry();

                zipStream.IsStreamOwner = false;    // False stops the Close also Closing the underlying stream.
                zipStream.Close();          // Must finish the ZipOutputStream before using outputMemStream.

                outputMemStream.Position = 0;
                value = outputMemStream.ToArray();
            }
            KeyValuePair<byte[], byte[]> metaPair;
            if (type==SerializationMethod.BZip || type == SerializationMethod.BZip)
                metaPair = GetRebuildedMeta(content.Skip(3).ToArray(), type, entry);
            else
                metaPair = GetRebuildedMeta(content, type, entry);
            KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>> res = new KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>>(new KeyValuePair<byte[], byte[]>(key, value), new KeyValuePair<byte[], byte[]>(metaPair.Key, metaPair.Value));
            return res;
        }


        public KeyValuePair<byte[], byte[]> GetRebuildedMeta(byte[] Content, SerializationMethod type, HistoryDatabaseFuncs.DBEntry entry)
        {
            Crc32 hash = new Crc32();
            hash.Update(Content);
            var metaKey = HistoryDatabaseFuncs.SerealizeKey(entry.Symbol, "Meta", entry.Period, entry.Time.Year, entry.Time.Month, entry.Time.Day, entry.Time.Hour, entry.Part);
            byte[] GettedEntry = new byte[5];
            BitConverter.GetBytes((UInt32)(hash.Value)).CopyTo(GettedEntry, 0);
            GettedEntry[4] = (byte)type;

            return new KeyValuePair<byte[], byte[]>(metaKey, GettedEntry);
        }


        public void RebuildMeta(ChunkFile file, bool showMessages = true)
        {
            var path = HistoryDatabaseFuncs.GetPath(file);
            int[] dateTime = HistoryDatabaseFuncs.GetFolderStartTime(path);
            string MetaCorruptionMessage = "";
            var TypeAndtext = ReadFromDB(file);
            var Type = TypeAndtext.Key;
            var content = TypeAndtext.Value;
            Crc32 hash = new Crc32();
            hash.Update(content);
            var metaKey = HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Meta", file.Period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], file.Part);
            var it = _dbase.CreateIterator();
            it.Seek(metaKey);

            byte[] GettedEntry = new byte[5];
            BitConverter.GetBytes((UInt32)(hash.Value)).CopyTo(GettedEntry, 0);
            GettedEntry[4] = (byte)Type;
            if (showMessages)
            {
                if (!it.Valid() || (!HistoryDatabaseFuncs.ValidateKeyByKey(it.Key(), metaKey, true, 4, true, true, true)))
                {
                    string pathStr = "";
                    foreach (var path_part in path)
                    {
                        if (pathStr != "")
                            pathStr += "/";
                        pathStr += (path_part.Name);
                    }
                    pathStr += ("." + file.Part + "");

                    MetaCorruptionMessage = "Meta for file " + pathStr + "was not found.\nMeta was recalculated";
                }
                else
                {
                    var metaEntry = it.Value();
                    Crc32 hashFromDB = new Crc32();
                    hashFromDB.Value = (BitConverter.ToUInt32(metaEntry, 0));
                    var metaStr = hashFromDB.Value.ToString("X8", CultureInfo.InvariantCulture);
                    metaStr += '\t';
                    metaStr += (SerializationMethod)metaEntry[4];

                    var contStr = hash.Value.ToString("X8", CultureInfo.InvariantCulture);
                    contStr += ("\t" + Type);
                    if (metaStr != contStr)
                    {
                        string pathStr = "";
                        foreach (var path_part in path)
                        {
                            if (pathStr != "")
                                pathStr += "/";
                            pathStr += (path_part.Name);
                        }
                        pathStr += ("." + file.Part + "");
                        MetaCorruptionMessage = "Meta for file " + pathStr + " was recalculated";
                    }
                }
            }
            _dbase.Put(metaKey, GettedEntry);
            it.Dispose();
            if (MetaCorruptionMessage != "" && showMessages)
            {
                MessageBox.Show(MetaCorruptionMessage, "Meta rebuild", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
        }


        public QHBar[] GetH1FromM1(IEnumerable<QHBar> bars)
        {
            if (bars == null || bars.Count() == 0)
                return new QHBar[] { };

            List<QHBar> resBars = new List<QHBar>();

            var curBar = new QHBar();


            curBar.Time = new DateTime(bars.First().Time.Year, bars.First().Time.Month, bars.First().Time.Day, bars.First().Time.Hour, 0, 0);
            curBar.Open = bars.First().Open;
            curBar.High = bars.First().High;
            curBar.Low = bars.First().Low;
            curBar.Close = bars.First().Close;
            curBar.Volume = 0;

            foreach (var bar in bars)
            {
                var time = new DateTime(bar.Time.Year, bar.Time.Month, bar.Time.Day, bar.Time.Hour, 0, 0);
                if (curBar.Time != time)
                {
                    resBars.Add(curBar);
                    curBar = new QHBar();
                    curBar.Time = time;
                    curBar.Open = bar.Open;
                    curBar.High = bar.High;
                    curBar.Low = bar.Low;
                    curBar.Close = bar.Close;
                    curBar.Volume = 0;
                }

                curBar.Close = bar.Close;
                curBar.Volume += bar.Volume;
                curBar.High = Math.Max(curBar.High, bar.High);
                curBar.Low = Math.Min(curBar.Low, bar.Low);
            }
            resBars.Add(curBar);

            return resBars.ToArray();
        }

        public KeyValuePair<QHBar[], QHBar[]> GetM1FromTicks(IEnumerable<QHTick> ticks)
        {
            if (ticks == null || ticks.Count() == 0)
                return new KeyValuePair<QHBar[], QHBar[]>(null, null);

            List<QHBar> resBid = new List<QHBar>();
            List<QHBar> resAsk = new List<QHBar>();

            var curBid = new QHBar();
            var curAsk = new QHBar();

            curBid.Time = curAsk.Time = new DateTime(ticks.First().Time.Year, ticks.First().Time.Month, ticks.First().Time.Day, ticks.First().Time.Hour, ticks.First().Time.Minute, 0);
            curBid.Open = curBid.High = curBid.Low = curBid.Close = ticks.First().Bid;
            curAsk.Open = curAsk.High = curAsk.Low = curAsk.Close = ticks.First().Ask;

            foreach (var tick in ticks)
            {
                var time = new DateTime(tick.Time.Year, tick.Time.Month, tick.Time.Day, tick.Time.Hour, tick.Time.Minute, 0);
                if (curBid.Time != time)
                {
                    resBid.Add(curBid);
                    curBid = new QHBar();
                    curBid.Time = time;
                    curBid.Open = curBid.High = curBid.Low = curBid.Close = tick.Bid;
                    curBid.Volume = 0;
                }
                if (curAsk.Time != time)
                {
                    resAsk.Add(curAsk);
                    curAsk = new QHBar();
                    curAsk.Time = time;
                    curAsk.Open = curAsk.High = curAsk.Low = curAsk.Close = tick.Ask;
                    curAsk.Volume = 0;
                }

                curBid.Close = tick.Bid;
                curBid.Volume += tick.BidVolume;
                curBid.High = Math.Max(curBid.High, tick.Bid);
                curBid.Low = Math.Min(curBid.Low, tick.Bid);

                curAsk.Close = tick.Ask;
                curAsk.Volume += tick.AskVolume;
                curAsk.High = Math.Max(curAsk.High, tick.Ask);
                curAsk.Low = Math.Min(curAsk.Low, tick.Ask);
            }
            resBid.Add(curBid);
            resAsk.Add(curAsk);

            return new KeyValuePair<QHBar[], QHBar[]>(resBid.ToArray(), resAsk.ToArray());
        }
        public QHTick[] GetTicksFromLevel2(IEnumerable<QHTickLevel2> ticks2)
        {
            if (ticks2 == null || ticks2.Count() == 0)
                return null;

            List<QHTick> res = new List<QHTick>();

            foreach (var t2 in ticks2)
            {
                res.Add(new QHTick()
                {
                    Part = t2.Part,
                    Time = t2.Time,
                    Bid = t2.BestBid.Key,
                    BidVolume = t2.BestBid.Value,
                    Ask = t2.BestAsk.Key,
                    AskVolume = t2.BestAsk.Value
                });
            }

            return res.ToArray();
        }
        void DeleteChunksAndMetaFromFolders(IList<Folder> Folders)
        {
            var toDel = new List<Folder>();

            foreach (var file in Folders)
            {
                if (file as ChunkFile != null || file as MetaFile != null)
                {
                    toDel.Add(file);
                }
            }

            toDel.ForEach(t => { Folders.Remove(t); });
        }

        void SvCntToFld(string content, string period, int part, Folder parent)
        {
            var chunk = new ChunkFile(period + " file", period, 0, parent);
            var meta = new MetaFile(period + " meta", period, 0, parent);
            parent.Folders.Add(chunk);
            parent.Folders.Add(meta);
            //SaveToDB(content, chunk);
            RebuildMeta(chunk);
        }

        public IEnumerable<KeyValuePair<byte[], byte[]>> EnumerateFilesInFolder(Folder fold, List<string> periods = null, List<string> types = null, bool onlyKeys = false)
        {

            Stopwatch w = new Stopwatch();
            if (periods == null)
                periods = new List<string>() { "ticks", "ticks level2", "M1 ask", "M1 bid", "H1 ask", "H1 bid" };
            if (types == null)
                types = new List<string>() { "Meta", "Chunk" };

            var it = _dbase.CreateIterator();
            try
            {
                if (fold as ChunkFile == null && fold as MetaFile == null)
                {
                    var path = HistoryDatabaseFuncs.GetPath(fold);
                    if (path.Count > 4)
                        periods = new List<string>() { "ticks", "ticks level2" };
                    int[] dateTime = { 2000, 1, 1, 0 };
                    for (int i = 1; i < path.Count; i++)
                    {
                        dateTime[i - 1] = int.Parse(path[i].Name);
                    }

                    foreach (var period in HistoryDatabaseFuncs.periodicityDict)
                    {
                        if (periods.Contains(period.Key))
                            foreach (var type in HistoryDatabaseFuncs.typeDict)
                            {
                                if (types.Contains(type.Key))
                                {
                                    var key = HistoryDatabaseFuncs.SerealizeKey(path[0].Name, type.Key, period.Key, dateTime[0], dateTime[1], dateTime[2], dateTime[3], 0);
                                    it.Seek(key);
                                    List<KeyValuePair<byte[], byte[]>> resList = new List<KeyValuePair<byte[], byte[]>>();
                                    while (it.Valid() && HistoryDatabaseFuncs.ValidateKeyByKey(it.Key(), key, true, path.Count - 1, true, true, false, false))
                                    {
                                        if (resList.Count < 128)
                                            resList.Add(new KeyValuePair<byte[], byte[]>(it.Key(), onlyKeys ? null : it.Value()));
                                        else
                                        {
                                            foreach (var pair in resList)
                                                yield return pair;
                                            resList = new List<KeyValuePair<byte[], byte[]>>();
                                        }
                                        it.Next();
                                    }
                                    foreach (var pair in resList)
                                        yield return pair;
                                }
                            }
                    }
                }
                else
                {
                    byte[] key = new byte[] { };
                    var path = HistoryDatabaseFuncs.GetPath(fold);
                    int[] dateTime = HistoryDatabaseFuncs.GetFolderStartTime(path);
                    string type = "";
                    if (fold as ChunkFile != null)
                    {
                        ChunkFile chunk = fold as ChunkFile;
                        key = HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Chunk", chunk.Period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], chunk.Part);
                        type = "Chunk";
                        it.Seek(key);
                    }
                    if (fold as MetaFile != null)
                    {
                        MetaFile metaFile = fold as MetaFile;
                        key = HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Meta", metaFile.Period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], metaFile.Part);
                        type = "Meta";
                        it.Seek(key);
                    }
                    if (it.Valid() && HistoryDatabaseFuncs.ValidateKeyByKey(it.Key(), key, true, path.Count - 2, true, true, true))
                    {
                        var file = fold as HistoryFile;
                        if (periods.Contains(file.Period) && types.Contains(type))
                            yield return new KeyValuePair<byte[], byte[]>(it.Key(), onlyKeys ? null : it.Value());
                    }
                }
                it.Dispose();
            }
            finally
            {
                it.Dispose();
            }
        }

    }
}
