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
using TickTrader.BusinessObjects.QuoteHistory.Store;
using TickTrader.Server.QuoteHistory.Store;
using TickTrader.BusinessObjects.QuoteHistory;
using TickTrader.Server.QuoteHistory.Serialization;
using TickTrader.BusinessObjects;
using TickTrader.Common.Business;

namespace QuoteHistoryGUI.HistoryTools
{
    public class HistoryEditor
    {

        public static int MaxCountPerChunk = 131072;

        private DB _dbase;
        private Dictionary<ChunkMetaInfo.SerializationMethod, IItemsSerializer<TickValue, TickValueList>> ticksSerializers;
        private Dictionary<ChunkMetaInfo.SerializationMethod, IItemsSerializer<TickValue, TickValueList>> ticksL2Serializers;
        private Dictionary<string, Dictionary<ChunkMetaInfo.SerializationMethod, IItemsSerializer<HistoryBar, List<HistoryBar>>>> barsSerializers;
        public HistoryEditor(DB db)
        {
            _dbase = db;

            ticksSerializers = new Dictionary<ChunkMetaInfo.SerializationMethod, IItemsSerializer<TickValue, TickValueList>>();
            ticksL2Serializers = new Dictionary<ChunkMetaInfo.SerializationMethod, IItemsSerializer<TickValue, TickValueList>>();
            barsSerializers = new Dictionary<string, Dictionary<ChunkMetaInfo.SerializationMethod, IItemsSerializer<HistoryBar, List<HistoryBar>>>>();
            barsSerializers["M1 bid"] = new Dictionary<ChunkMetaInfo.SerializationMethod, IItemsSerializer<HistoryBar, List<HistoryBar>>>();
            barsSerializers["M1 ask"] = new Dictionary<ChunkMetaInfo.SerializationMethod, IItemsSerializer<HistoryBar, List<HistoryBar>>>();
            barsSerializers["H1 bid"] = new Dictionary<ChunkMetaInfo.SerializationMethod, IItemsSerializer<HistoryBar, List<HistoryBar>>>();
            barsSerializers["H1 ask"] = new Dictionary<ChunkMetaInfo.SerializationMethod, IItemsSerializer<HistoryBar, List<HistoryBar>>>();


            ticksSerializers[ChunkMetaInfo.SerializationMethod.Text] = new ItemsTextSerializer<TickValue, TickValueList>(FeedTickFormatter.Instance, "ticks");
            ticksL2Serializers[ChunkMetaInfo.SerializationMethod.Text] = new ItemsTextSerializer<TickValue, TickValueList>(FeedTickLevel2Formatter.Instance, "ticks level2");
            barsSerializers["M1 bid"][ChunkMetaInfo.SerializationMethod.Text] = new ItemsTextSerializer<HistoryBar, List<HistoryBar>>(BarFormatter.Default, "M1 bid");
            barsSerializers["M1 ask"][ChunkMetaInfo.SerializationMethod.Text] = new ItemsTextSerializer<HistoryBar, List<HistoryBar>>(BarFormatter.Default, "M1 ask");
            barsSerializers["H1 bid"][ChunkMetaInfo.SerializationMethod.Text] = new ItemsTextSerializer<HistoryBar, List<HistoryBar>>(BarFormatter.Default, "H1 bid");
            barsSerializers["H1 ask"][ChunkMetaInfo.SerializationMethod.Text] = new ItemsTextSerializer<HistoryBar, List<HistoryBar>>(BarFormatter.Default, "H1 ask");

            ticksSerializers[ChunkMetaInfo.SerializationMethod.Zip] = new ItemsZipSerializer<TickValue, TickValueList>(FeedTickFormatter.Instance, "ticks");
            ticksL2Serializers[ChunkMetaInfo.SerializationMethod.Zip] = new ItemsZipSerializer<TickValue, TickValueList>(FeedTickLevel2Formatter.Instance, "ticks level2");
            barsSerializers["M1 bid"][ChunkMetaInfo.SerializationMethod.Zip] = new ItemsZipSerializer<HistoryBar, List<HistoryBar>>(BarFormatter.Default, "M1 bid");
            barsSerializers["M1 ask"][ChunkMetaInfo.SerializationMethod.Zip] = new ItemsZipSerializer<HistoryBar, List<HistoryBar>>(BarFormatter.Default, "M1 ask");
            barsSerializers["H1 bid"][ChunkMetaInfo.SerializationMethod.Zip] = new ItemsZipSerializer<HistoryBar, List<HistoryBar>>(BarFormatter.Default, "H1 bid");
            barsSerializers["H1 ask"][ChunkMetaInfo.SerializationMethod.Zip] = new ItemsZipSerializer<HistoryBar, List<HistoryBar>>(BarFormatter.Default, "H1 ask");


            ticksSerializers[ChunkMetaInfo.SerializationMethod.Binary] = new ItemsBinarySerializer<TickValue, TickValueList>(BinaryFeedTickFormatter.Instance, "ticks");
            ticksL2Serializers[ChunkMetaInfo.SerializationMethod.Binary] = new ItemsBinarySerializer<TickValue, TickValueList>(BinaryFeedTickLevel2Formatter.Instance, "ticks level2");
            barsSerializers["M1 bid"][ChunkMetaInfo.SerializationMethod.Binary] = new ItemsBinarySerializer<HistoryBar, List<HistoryBar>>(BinaryBarFormatter.Default, "M1 bid");
            barsSerializers["M1 ask"][ChunkMetaInfo.SerializationMethod.Binary] = new ItemsBinarySerializer<HistoryBar, List<HistoryBar>>(BinaryBarFormatter.Default, "M1 ask");
            barsSerializers["H1 bid"][ChunkMetaInfo.SerializationMethod.Binary] = new ItemsBinarySerializer<HistoryBar, List<HistoryBar>>(BinaryBarFormatter.Default, "H1 bid");
            barsSerializers["H1 ask"][ChunkMetaInfo.SerializationMethod.Binary] = new ItemsBinarySerializer<HistoryBar, List<HistoryBar>>(BinaryBarFormatter.Default, "H1 ask");

            ticksSerializers[ChunkMetaInfo.SerializationMethod.BinaryZip] = new ItemsBinaryZipSerializer<TickValue, TickValueList>(BinaryFeedTickFormatter.Instance, "ticks");
            ticksL2Serializers[ChunkMetaInfo.SerializationMethod.BinaryZip] = new ItemsBinaryZipSerializer<TickValue, TickValueList>(BinaryFeedTickLevel2Formatter.Instance, "ticks level2");
            barsSerializers["M1 bid"][ChunkMetaInfo.SerializationMethod.BinaryZip] = new ItemsBinaryZipSerializer<HistoryBar, List<HistoryBar>>(BinaryBarFormatter.Default, "M1 bid");
            barsSerializers["M1 ask"][ChunkMetaInfo.SerializationMethod.BinaryZip] = new ItemsBinaryZipSerializer<HistoryBar, List<HistoryBar>>(BinaryBarFormatter.Default, "M1 ask");
            barsSerializers["H1 bid"][ChunkMetaInfo.SerializationMethod.BinaryZip] = new ItemsBinaryZipSerializer<HistoryBar, List<HistoryBar>>(BinaryBarFormatter.Default, "H1 bid");
            barsSerializers["H1 ask"][ChunkMetaInfo.SerializationMethod.BinaryZip] = new ItemsBinaryZipSerializer<HistoryBar, List<HistoryBar>>(BinaryBarFormatter.Default, "H1 ask");

        }

        public static byte[] GetOrUnzip(byte[] content)
        {
            ChunkMetaInfo.SerializationMethod serilizer = ChunkMetaInfo.SerializationMethod.Text;
            if (content == null || content.Count() == 0) return new byte[] { };
            if (content[0] == 'P' && content[1] == 'K')
                serilizer = ChunkMetaInfo.SerializationMethod.Zip;
            else if (content[0] == 'B' && content[1] == 'Z')
                serilizer = ChunkMetaInfo.SerializationMethod.BZip;
            else if (content[0] == 'B' && content[1] == 'Q' && content[2] == 'H')
                serilizer = ChunkMetaInfo.SerializationMethod.Binary;


            if (serilizer == ChunkMetaInfo.SerializationMethod.Zip)
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
                if (content[0] == 'B' && content[1] == 'Q' && content[2] == 'H')
                    content = content.Skip(3).ToArray();
                return content;
            }


            if (serilizer == ChunkMetaInfo.SerializationMethod.Binary)
                return content.Skip(3).ToArray();

            return content;
        }

        public KeyValuePair<ChunkMetaInfo.SerializationMethod, byte[]> GetSerTypeAndFlushContent(string symbol, string period, DateTime id, int part)
        {
            byte[] dbContent = _dbase.Get(HistoryDatabaseFuncs.SerealizeKey(symbol, "Chunk", period, id.Year,
                id.Month, id.Day, id.Hour, part));

            ChunkMetaInfo.SerializationMethod serializer = ChunkMetaInfo.SerializationMethod.Text;

            if (dbContent.Length >= 2 && dbContent[0] == 'P' && dbContent[1] == 'K')
                serializer = ChunkMetaInfo.SerializationMethod.Zip;
            else
                if (dbContent.Length >= 2 && dbContent[0] == 'B' && dbContent[1] == 'Z')
                serializer = ChunkMetaInfo.SerializationMethod.BZip;
            else
                if (dbContent.Length >= 3 && dbContent[0] == 'B' && dbContent[1] == 'Q' && dbContent[2] == 'H')
                serializer = ChunkMetaInfo.SerializationMethod.Binary;

            if (serializer == ChunkMetaInfo.SerializationMethod.Zip)
            {
                MemoryStream data = new MemoryStream(dbContent);
                ZipFile zip = new ZipFile(data);

                foreach (ZipEntry zipEntry in zip)
                {
                    Stream zipStream = zip.GetInputStream(zipEntry);
                    byte[] buffer = new byte[3];
                    if (zipStream.Read(buffer, 0, 3) == 3)
                    {
                        if (buffer[0] == 'B' && buffer[1] == 'Q' && buffer[2] == 'H')
                            serializer = ChunkMetaInfo.SerializationMethod.BinaryZip;
                    }
                }
            }

            if (serializer == ChunkMetaInfo.SerializationMethod.Binary || serializer == ChunkMetaInfo.SerializationMethod.Text)
            {
                List<byte> res = new List<byte>(dbContent);
                int flushPart = 1;
                while (true)
                {
                    var cntPart = _dbase.Get(HistoryDatabaseFuncs.SerealizeKey(symbol, "Chunk", period, id.Year,
                id.Month, id.Day, id.Hour, part, flushPart));
                    if (cntPart == null) break;
                    if (serializer == ChunkMetaInfo.SerializationMethod.Binary)
                        res.AddRange(cntPart.Skip(3));
                    else res.AddRange(cntPart);

                    flushPart++;
                }
                dbContent = res.ToArray();
            }

            var resultPair = new KeyValuePair<ChunkMetaInfo.SerializationMethod, byte[]>(serializer, dbContent);
            return resultPair;
        }

        public KeyValuePair<ChunkMetaInfo.SerializationMethod, byte[]> GetSerTypeAndFlushContent(HistoryFile f)
        {
            var path = HistoryDatabaseFuncs.GetPath(f);
            int[] dateTime = HistoryDatabaseFuncs.GetFolderStartTime(path);
            if (f as ChunkFile != null)
            {
                return GetSerTypeAndFlushContent(path[0].Name, f.Period, new DateTime(dateTime[0], dateTime[1], dateTime[2], dateTime[3], 0, 0), f.Part);
            }
            else
            {
                var meta = _dbase.Get(HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Meta", f.Period, dateTime[0], dateTime[1], dateTime[2], dateTime[3], f.Part));
                //Crc32 hash = new Crc32();
                //hash.Value = (BitConverter.ToUInt32(meta, 0));
                //var contStr = hash.Value.ToString("X8", CultureInfo.InvariantCulture);
                //contStr += '\t';
                //contStr += ((ChunkMetaInfo.SerializationMethod)(meta[4]));
                return new KeyValuePair<ChunkMetaInfo.SerializationMethod, byte[]>(ChunkMetaInfo.SerializationMethod.Unknown, meta);

            }
        }

        public string GetText(string period, ChunkMetaInfo.SerializationMethod ser, byte[] content)
        {
            Crc32Hash hash;
            string result = "";
            switch (period)
            {
                case "ticks":
                    result = ASCIIEncoding.ASCII.GetString(ticksSerializers[ChunkMetaInfo.SerializationMethod.Text].Serialize(ticksSerializers[ser].Deserialize(content, out hash)));
                    break;
                case "ticks level2":
                    result = ASCIIEncoding.ASCII.GetString(ticksL2Serializers[ChunkMetaInfo.SerializationMethod.Text].Serialize(ticksL2Serializers[ser].Deserialize(content, out hash)));
                    break;
                default:
                    result = ASCIIEncoding.ASCII.GetString(barsSerializers[period][ChunkMetaInfo.SerializationMethod.Text].Serialize(barsSerializers[period][ser].Deserialize(content, out hash)));
                    break;
            }
            return result;
        }

        public bool SaveFromText(ChunkMetaInfo.SerializationMethod ser, string symbol ,string period, DateTime time, int part, string text)
        {
            Crc32Hash hash;
            byte[] content;
            try
            {
                switch (period)
                {
                    case "ticks":
                        content = ticksSerializers[ser].Serialize(ticksSerializers[ChunkMetaInfo.SerializationMethod.Text].Deserialize(ASCIIEncoding.ASCII.GetBytes(text), out hash));
                        break;
                    case "ticks level2":
                        content = ticksL2Serializers[ser].Serialize(ticksL2Serializers[ChunkMetaInfo.SerializationMethod.Text].Deserialize(ASCIIEncoding.ASCII.GetBytes(text), out hash));
                        break;
                    default:
                        content = barsSerializers[period][ser].Serialize(barsSerializers[period][ChunkMetaInfo.SerializationMethod.Text].Deserialize(ASCIIEncoding.ASCII.GetBytes(text), out hash));
                        break;
                }
            }
            catch
            {

                MessageBox.Show("There is a syntax error! Unable to save.\n\n" + "Check the numeric format and punctuation", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.MainWindow.Activate();
                return false;
            }

            var key = HistoryDatabaseFuncs.SerealizeKey(symbol, "Chunk", period, time.Year, time.Month, time.Day, time.Hour, part);
            _dbase.Put(key, content);

            RebuildMeta(symbol, period, time, part);

            return true;

        }

        public bool SaveFromText(ChunkMetaInfo.SerializationMethod ser, ChunkFile file, string text)
        {
            var path = HistoryDatabaseFuncs.GetPath(file);
            int[] dateTime = HistoryDatabaseFuncs.GetFolderStartTime(path);
            var TypeAndContent = GetSerTypeAndFlushContent(file);
            return SaveFromText(ser, path[0].Name, file.Period, new DateTime(dateTime[0], dateTime[1], dateTime[2], dateTime[3], 0, 0), file.Part, text);
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
                        ChunkMetaInfo.SerializationMethod serilizer = ChunkMetaInfo.SerializationMethod.Text;
                        if (cntnt != null)
                        {
                            if (cntnt.Length >= 2 && cntnt[0] == 'P' && cntnt[1] == 'K')
                                serilizer = ChunkMetaInfo.SerializationMethod.Zip;
                            else
                            if (cntnt.Length >= 2 && cntnt[0] == 'B' && cntnt[1] == 'Z')
                                serilizer = ChunkMetaInfo.SerializationMethod.BZip;
                            else
                            if (cntnt.Length >= 3 && cntnt[0] == 'B' && cntnt[1] == 'Q' && cntnt[2] == 'H')
                                serilizer = ChunkMetaInfo.SerializationMethod.Binary;

                            if (serilizer == ChunkMetaInfo.SerializationMethod.Binary || serilizer == ChunkMetaInfo.SerializationMethod.Text)
                            {
                                var cntList = new List<byte>(cntnt);
                                var flushPart = 1;
                                while (true)
                                {
                                    var cntFlushPart = _dbase.Get(HistoryDatabaseFuncs.SerealizeKey(path[0].Name, "Chunk", f.Period, dateTime[0], dateTime[1], day, hour, i, flushPart));
                                    if (cntFlushPart == null) break;
                                    else
                                    {
                                        if (serilizer == ChunkMetaInfo.SerializationMethod.Binary)
                                            cntList.AddRange(cntFlushPart.Skip(3));
                                        else cntList.AddRange(cntFlushPart);

                                        flushPart++;
                                    }
                                }
                                cntnt = cntList.ToArray();
                            }
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

                        ChunkMetaInfo.SerializationMethod serilizer = ChunkMetaInfo.SerializationMethod.Text;
                        if (cntnt.Length >= 2 && cntnt[0] == 'P' && cntnt[1] == 'K')
                            serilizer = ChunkMetaInfo.SerializationMethod.Zip;
                        else
                        if (cntnt.Length >= 2 && cntnt[0] == 'B' && cntnt[1] == 'Z')
                            serilizer = ChunkMetaInfo.SerializationMethod.BZip;
                        else
                        if (cntnt.Length >= 3 && cntnt[0] == 'B' && cntnt[1] == 'Q' && cntnt[2] == 'H')
                            serilizer = ChunkMetaInfo.SerializationMethod.Binary;

                        if (serilizer == ChunkMetaInfo.SerializationMethod.Binary || serilizer == ChunkMetaInfo.SerializationMethod.Text)
                        {
                            var cntList = new List<byte>(cntnt);
                            var flushPart = 1;
                            while (true)
                            {
                                var cntFlushPart = _dbase.Get(HistoryDatabaseFuncs.SerealizeKey(entry.Symbol, "Chunk", entry.Period, entry.Time.Year, entry.Time.Month, day, hour, i, flushPart));
                                if (cntFlushPart == null) break;
                                else
                                {
                                    if (serilizer == ChunkMetaInfo.SerializationMethod.Binary)
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

            if (meta != null && meta[4] == (byte)ChunkMetaInfo.SerializationMethod.Text)
            {
                value = content;
            }
            if (meta != null && meta[4] == (byte)ChunkMetaInfo.SerializationMethod.Binary)
            {
                value = HistorySerializer.SerializeBinary(items);
            }
            if (meta != null && meta[4] == (byte)ChunkMetaInfo.SerializationMethod.BZip)
            {
                //value = HistorySerializer.SerializeBinary(items);

                //MemoryStream contentMemStream = new MemoryStream(value);
                //MemoryStream outputMemStream = new MemoryStream();

                //Bzip2.BZip2OutputStream bzipStream = new Bzip2.BZip2OutputStream(outputMemStream);
                //int bufSize = 4096;
                //byte[] buffer = new byte[bufSize];
                //int cnt = 0;
                //while (true)
                //{
                //    cnt = contentMemStream.Read(buffer, 0, bufSize);
                //    bzipStream.Write(buffer, 0, cnt);
                //    if (cnt < bufSize)
                //        break;
                //}
                ////StreamUtils.Copy(contentMemStream, bzipStream, new byte[4096]);
                //bzipStream.Close();
                //value = outputMemStream.ToArray();
            }

            if (meta != null && meta[4] == (byte)ChunkMetaInfo.SerializationMethod.Zip)
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
            ChunkMetaInfo.SerializationMethod type = ChunkMetaInfo.SerializationMethod.Zip;
            if (binary)
                type = ChunkMetaInfo.SerializationMethod.BZip;
            if (meta?[4] == 2)
            {
                type = ChunkMetaInfo.SerializationMethod.Text;
                if (binary)
                    type = ChunkMetaInfo.SerializationMethod.Binary;
                value = content;
            }

            if (type == ChunkMetaInfo.SerializationMethod.BZip)
            {
                //MemoryStream contentMemStream = new MemoryStream(content);
                //MemoryStream outputMemStream = new MemoryStream();

                //Bzip2.BZip2OutputStream bzipStream = new Bzip2.BZip2OutputStream(outputMemStream);
                //int bufSize = 4096;
                //byte[] buffer = new byte[bufSize];
                //int cnt = 0;
                //while (true)
                //{
                //    cnt = contentMemStream.Read(buffer, 0, bufSize);
                //    bzipStream.Write(buffer, 0, cnt);
                //    if (cnt < bufSize)
                //        break;
                //}
                ////StreamUtils.Copy(contentMemStream, bzipStream, new byte[4096]);
                //bzipStream.Close();
                //value = outputMemStream.ToArray();
            }

            if (type == ChunkMetaInfo.SerializationMethod.Zip)
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
            if (type == ChunkMetaInfo.SerializationMethod.BZip || type == ChunkMetaInfo.SerializationMethod.BZip)
                metaPair = GetRebuildedMeta(content.Skip(3).ToArray(), type, entry);
            else
                metaPair = GetRebuildedMeta(content, type, entry);
            KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>> res = new KeyValuePair<KeyValuePair<byte[], byte[]>, KeyValuePair<byte[], byte[]>>(new KeyValuePair<byte[], byte[]>(key, value), new KeyValuePair<byte[], byte[]>(metaPair.Key, metaPair.Value));
            return res;
        }


        public KeyValuePair<byte[], byte[]> GetRebuildedMeta(byte[] Content, ChunkMetaInfo.SerializationMethod type, HistoryDatabaseFuncs.DBEntry entry)
        {
            Crc32 hash = new Crc32();
            hash.Update(Content);
            var metaKey = HistoryDatabaseFuncs.SerealizeKey(entry.Symbol, "Meta", entry.Period, entry.Time.Year, entry.Time.Month, entry.Time.Day, entry.Time.Hour, entry.Part);
            byte[] GettedEntry = new byte[5];
            BitConverter.GetBytes((UInt32)(hash.Value)).CopyTo(GettedEntry, 0);
            GettedEntry[4] = (byte)type;

            return new KeyValuePair<byte[], byte[]>(metaKey, GettedEntry);
        }


        public void RebuildMeta(string symbol, string period, DateTime time, int part, bool showMessages = true)
        {
            string MetaCorruptionMessage = "";

            var TypeAndContent = GetSerTypeAndFlushContent(symbol, period, time, part);

            Crc32Hash hash;

            switch (period)
            {
                case "ticks":
                    ticksSerializers[TypeAndContent.Key].Deserialize(TypeAndContent.Value, out hash);
                    break;
                case "ticks level2":
                    ticksL2Serializers[TypeAndContent.Key].Deserialize(TypeAndContent.Value, out hash);
                    break;
                default:
                    barsSerializers[period][TypeAndContent.Key].Deserialize(TypeAndContent.Value, out hash);
                    break;
            }

            var metaKey = HistoryDatabaseFuncs.SerealizeKey(symbol, "Meta", period, time.Year, time.Month, time.Day, time.Hour, part);
            var it = _dbase.CreateIterator();
            it.Seek(metaKey);
            byte[] RebuildedEntry = new byte[5];
            BitConverter.GetBytes((UInt32)(hash)).CopyTo(RebuildedEntry, 0);
            RebuildedEntry[4] = (byte)TypeAndContent.Key;


            if (!it.Valid() || (!HistoryDatabaseFuncs.ValidateKeyByKey(it.Key(), metaKey, true, 4, true, true, true)))
            {
                string pathStr = part == 0 ? "" : ("." + part);

                if (period == "ticks" || period == "ticks level2")
                    pathStr = symbol + "/" + time.Year + "/" + time.Month + "/" + time.Day + "/" + time.Hour + "/" + period + pathStr;
                if (period == "M1 ask" || period == "M1 bid")
                    pathStr = symbol + "/" + time.Year + "/" + time.Month + "/" + time.Day + "/" + period + pathStr;
                if (period == "H1 ask" || period == "H1 bid")
                    pathStr = symbol + "/" + time.Year + "/" + time.Month + "/" + period + pathStr;


                MetaCorruptionMessage = "Meta for file " + pathStr + "was not found.\nMeta was recalculated";
                _dbase.Put(metaKey, RebuildedEntry);
            }
            else
            {
                var metaEntry = it.Value();
                Crc32 hashFromDB = new Crc32();
                hashFromDB.Value = (BitConverter.ToUInt32(metaEntry, 0));
                var metaStr = hashFromDB.Value.ToString("X8", CultureInfo.InvariantCulture);
                metaStr += '\t';
                metaStr += (ChunkMetaInfo.SerializationMethod)metaEntry[4];

                var contStr = hash.ToString();
                contStr += ("\t" + TypeAndContent.Key);
                if (metaStr != contStr)
                {
                    string pathStr = part == 0 ? "" : ("." + part);

                    if (period == "ticks" || period == "ticks level2")
                        pathStr = symbol + "/" + time.Year + "/" + time.Month + "/" + time.Day + "/" + time.Hour + "/" + period + pathStr;
                    if (period == "M1 ask" || period == "M1 bid")
                        pathStr = symbol + "/" + time.Year + "/" + time.Month + "/" + time.Day + "/" + period + pathStr;
                    if (period == "H1 ask" || period == "H1 bid")
                        pathStr = symbol + "/" + time.Year + "/" + time.Month + "/" + period + pathStr;

                    MetaCorruptionMessage = "Meta for file " + pathStr + " was recalculated";
                    _dbase.Put(metaKey, RebuildedEntry);
                }
            }

            it.Dispose();
            if (MetaCorruptionMessage != "" && showMessages)
            {
                MessageBox.Show(MetaCorruptionMessage, "Meta rebuild", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
        }
        public void RebuildMeta(ChunkFile file, bool showMessages = true)
        {
            var path = HistoryDatabaseFuncs.GetPath(file);
            int[] dateTime = HistoryDatabaseFuncs.GetFolderStartTime(path);
            var TypeAndContent = GetSerTypeAndFlushContent(file);
            RebuildMeta(path[0].Name, file.Period, new DateTime(dateTime[0], dateTime[1], dateTime[2], dateTime[3], 0, 0), file.Part, showMessages);
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
