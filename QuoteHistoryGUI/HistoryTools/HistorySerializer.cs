﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuoteHistoryGUI.HistoryTools
{


    public abstract class QHItem
    {
        public DateTime Time;
        public abstract byte[] Serialize();
        public abstract byte[] SerializeBinary();
    }
    public class QHBar : QHItem
    {
        public decimal Open;
        public decimal High;
        public decimal Low;
        public decimal Close;
        public decimal Volume;

        public override string ToString()
        {
            string barStr = "";
            barStr += Time.ToString("yyyy.MM.dd HH:mm:ss");
            barStr += "\t";
            barStr += Open.ToString(CultureInfo.InvariantCulture);
            barStr += "\t";
            barStr += High.ToString(CultureInfo.InvariantCulture);
            barStr += "\t";
            barStr += Low.ToString(CultureInfo.InvariantCulture);
            barStr += "\t";
            barStr += Close.ToString(CultureInfo.InvariantCulture);
            barStr += "\t";
            barStr += Volume.ToString(CultureInfo.InvariantCulture);
            return barStr;
        }

        public override byte[] Serialize()
        {
            string barStr = "";
            barStr += Time.ToString("yyyy.MM.dd HH:mm:ss");
            barStr += "\t";
            barStr += Open.ToString(CultureInfo.InvariantCulture);
            barStr += "\t";
            barStr += High.ToString(CultureInfo.InvariantCulture);
            barStr += "\t";
            barStr += Low.ToString(CultureInfo.InvariantCulture);
            barStr += "\t";
            barStr += Close.ToString(CultureInfo.InvariantCulture);
            barStr += "\t";
            barStr += Volume.ToString(CultureInfo.InvariantCulture);
            barStr += "\r\n";
            return ASCIIEncoding.ASCII.GetBytes(barStr);
        }

        public override byte[] SerializeBinary()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter bw = new BinaryWriter(ms);

                bw.Write(Time.ToBinary());
                bw.Write((double)Open);
                bw.Write((double)High);
                bw.Write((double)Low);
                bw.Write((double)Close);
                bw.Write((double)Volume);
                bw.Flush();
                return ms.ToArray();
            }
        }
    }
    public class QHTick : QHItem
    {
        public decimal Bid;
        public decimal BidVolume;
        public decimal Ask;
        public decimal AskVolume;
        public int Part = 0;

        public override string ToString()
        {
            string tickStr = "";
            tickStr += Time.ToString("yyyy.MM.dd HH:mm:ss.fff");
            if (Part > 0)
                tickStr = tickStr + "-" + Part;
            tickStr += "\t";
            tickStr += Bid.ToString(CultureInfo.InvariantCulture);
            tickStr += "\t";
            tickStr += BidVolume.ToString(CultureInfo.InvariantCulture);
            tickStr += "\t";
            tickStr += Ask.ToString(CultureInfo.InvariantCulture);
            tickStr += "\t";
            tickStr += AskVolume.ToString(CultureInfo.InvariantCulture);
            return tickStr;
        }

        public override byte[] Serialize()
        {
            string tickStr = "";
            tickStr += Time.ToString("yyyy.MM.dd HH:mm:ss.fff");
            if (Part > 0)
                tickStr = tickStr + "-" + Part;
            tickStr += "\t";
            tickStr += Bid.ToString(CultureInfo.InvariantCulture);
            tickStr += "\t";
            tickStr += BidVolume.ToString(CultureInfo.InvariantCulture);
            tickStr += "\t";
            tickStr += Ask.ToString(CultureInfo.InvariantCulture);
            tickStr += "\t";
            tickStr += AskVolume.ToString(CultureInfo.InvariantCulture);
            tickStr += "\r\n";
            return ASCIIEncoding.ASCII.GetBytes(tickStr);
        }

        public override byte[] SerializeBinary()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter bw = new BinaryWriter(ms);

                bw.Write(this.Time.ToBinary());
                bw.Write((byte)this.Part);
                bw.Write((double)this.Bid);
                bw.Write((double)this.BidVolume);
                bw.Write((double)this.Ask);
                bw.Write((double)this.AskVolume);
                bw.Flush();
                return ms.ToArray();
            }
        }
    }

    public class QHTickLevel2 : QHItem
    {
        public KeyValuePair<decimal, decimal>[] Bids;
        public KeyValuePair<decimal, decimal>[] Asks;
        public KeyValuePair<decimal, decimal> BestBid { get { return Bids.Count() > 0 ? Bids.Last() : new KeyValuePair<decimal, decimal>(); } }
        public KeyValuePair<decimal, decimal> BestAsk { get { return Asks.Count() > 0 ? Asks.First() : new KeyValuePair<decimal, decimal>(); } }
        public int Part = 0;

        public override string ToString()
        {
            string tickStr = "";
            tickStr += Time.ToString("yyyy.MM.dd HH:mm:ss.fff");
            if (Part > 0)
                tickStr = tickStr + "-" + Part;
            tickStr += "\tbid";
            foreach(var bid in Bids)
                tickStr += ("\t"+bid.Key+"\t"+bid.Value);
            tickStr += "\task";
            foreach (var ask in Asks)
                tickStr += ("\t" + ask.Key + "\t" + ask.Value);
            return tickStr;
        }

        public override byte[] Serialize()
        {
            throw new NotImplementedException();
        }

        public override byte[] SerializeBinary()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter bw = new BinaryWriter(ms);
                bw.Write(this.Time.ToBinary());
                bw.Write((byte)this.Part);
                
                bw.Write((byte)Bids.Count());
                for (int i = 0; i < Bids.Count(); i++)
                {
                    bw.Write((double)(Bids[i].Key));
                    bw.Write((double)(Bids[i].Value));
                }
                bw.Write((byte)Asks.Count());
                for (int i = 0; i < Asks.Count(); i++)
                {
                    bw.Write((double)(Asks[i].Key));
                    bw.Write((double)(Asks[i].Value));
                }
                bw.Flush();
                return ms.ToArray();
            }
        }
    }

    public class HistorySerializer
    {
        public static IEnumerable<QHItem> Deserialize(string period, byte[] content, int degreeOfParallelism = 4)
        {
            if (content == null)
                return new List<QHItem>();
            if (period == "ticks")
            {
                return DeserializeTicks(content);
            }
            else if (period == "ticks level2")
            {
                return DeserializeTicksLevel2(content, degreeOfParallelism);
            }
            else if (period == "M1 ask" || period == "M1 bid" || period == "H1 ask" || period == "H1 bid")
            {
                return DeserializeBars(content);
            }

            return null;
        }

        public static IEnumerable<QHItem> DeserializeBinary(string period, byte[] content, int degreeOfParallelism = 4)
        {
            if (content == null)
                return new List<QHItem>();
            if (period == "ticks")
            {
                return DeserializeTicksBinary(content);
            }
            else if (period == "ticks level2")
            {
                return DeserializeTicksLevel2Binary(content, degreeOfParallelism);
            }
            else if (period == "M1 ask" || period == "M1 bid" || period == "H1 ask" || period == "H1 bid")
            {
                return DeserializeBarsBinary(content);
            }
            return null;
        }


        public static IEnumerable<QHBar> DeserializeBars(byte[] content)
        {
            List<QHBar> res = new List<QHBar>();
            StreamReader reader = new StreamReader(new MemoryStream(content));
            string line = "";
            try
            {
                while (!reader.EndOfStream)
                {
                    line = reader.ReadLine();
                    var splittedLine = line.Split(new char[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (splittedLine.Count() == 0)
                    {
                        throw new InvalidDataException("Blank line not at the end of the file is not allowed.");
                    }
                    QHBar bar = new QHBar();
                    bar.Time = DateTime.Parse(splittedLine[0] + " " + splittedLine[1], CultureInfo.InvariantCulture);
                    bar.Open = decimal.Parse(splittedLine[2], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture);
                    bar.High = decimal.Parse(splittedLine[3], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture);
                    bar.Low = decimal.Parse(splittedLine[4], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture);
                    bar.Close = decimal.Parse(splittedLine[5], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture);
                    bar.Volume = decimal.Parse(splittedLine[6], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture);

                    res.Add(bar);
                }
            }
            catch (FormatException ex)
            {
                throw new FormatException("Line " + line + " caused " + ex);
            }
            return res;
        }

        public static IEnumerable<QHBar> DeserializeBarsBinary(byte[] content)
        {
            List<QHBar> res = new List<QHBar>();
            StreamReader reader = new StreamReader(new MemoryStream(content));
            string line = "";
            try
            {
                QHBar bar;
                int contentIndex = 0;

                while (contentIndex < content.Count())
                {

                    DateTime time = DateTime.FromBinary(BitConverter.ToInt64(content, contentIndex));
                    contentIndex += sizeof(Int64);
                    decimal open = (decimal)BitConverter.ToDouble(content, contentIndex);
                    contentIndex += sizeof(double);
                    decimal high = (decimal)BitConverter.ToDouble(content, contentIndex);
                    contentIndex += sizeof(double);
                    decimal low = (decimal)BitConverter.ToDouble(content, contentIndex);
                    contentIndex += sizeof(double);
                    decimal close = (decimal)BitConverter.ToDouble(content, contentIndex);
                    contentIndex += sizeof(double);
                    double volume = BitConverter.ToDouble(content, contentIndex);
                    contentIndex += sizeof(double);
                    bar = new QHBar()
                    {
                        Time = time,
                        Open = open,
                        High = high,
                        Low = low,
                        Close = close,
                        Volume = (decimal)volume
                    };

                    res.Add(bar);
                }
                
            }
            catch (FormatException ex)
            {
                throw new FormatException("Line " + line + " caused " + ex);
            }
            return res;
        }


        public static IEnumerable<QHTick> DeserializeTicks(byte[] content)
        {
            List<QHTick> res = new List<QHTick>();
            StreamReader reader = new StreamReader(new MemoryStream(content));
            string line = "";
            try
            {
                while (!reader.EndOfStream)
                {
                    line = reader.ReadLine();
                    var splittedLine = line.Split(new char[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (splittedLine.Count() == 0)
                    {
                        throw new InvalidDataException("Blank line not at the end of the file is not allowed.");
                    }
                    QHTick tick = new QHTick();
                    var dateAndPartStr = splittedLine[1].Split('-');
                    if (dateAndPartStr.Count() == 2)
                    {
                        tick.Part = int.Parse(dateAndPartStr[1]);
                        splittedLine[1] = dateAndPartStr[0];
                    }
                    tick.Time = DateTime.Parse(splittedLine[0] + " " + splittedLine[1], CultureInfo.InvariantCulture);
                    tick.Bid = decimal.Parse(splittedLine[2], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture);
                    tick.BidVolume = decimal.Parse(splittedLine[3], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture);
                    tick.Ask = decimal.Parse(splittedLine[4], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture);
                    tick.AskVolume = decimal.Parse(splittedLine[5], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture);
                    res.Add(tick);
                }
            }
            catch (FormatException ex)
            {
                throw new FormatException("Line " + line + " caused " + ex);
            }
            return res;
        }

        public static IEnumerable<QHTick> DeserializeTicksBinary(byte[] content)
        {
            List<QHTick> res = new List<QHTick>();
            string line = "";
            try
            {
                QHTick tick;
                int contentIndex = 0;

                while (contentIndex < content.Count())
                {
                    tick = new QHTick();
                    tick.Time = DateTime.FromBinary(BitConverter.ToInt64(content, contentIndex));
                    contentIndex += sizeof(Int64);
                    tick.Part = content[contentIndex];
                    contentIndex += 1;
                    tick.Bid = (decimal)BitConverter.ToDouble(content, contentIndex);
                    contentIndex += sizeof(double);
                    tick.BidVolume = (decimal)BitConverter.ToDouble(content, contentIndex);
                    contentIndex += sizeof(double);
                    tick.Ask = (decimal)BitConverter.ToDouble(content, contentIndex);
                    contentIndex += sizeof(double);
                    tick.AskVolume = (decimal)BitConverter.ToDouble(content, contentIndex);
                    contentIndex += sizeof(double);
                    
                    res.Add(tick);
                }
            }
            catch (FormatException ex)
            {
                throw new FormatException("Line " + line + " caused " + ex);
            }
            return res;
        }

        public static IEnumerable<QHTickLevel2> DeserializeTicksLevel2(byte[] content, int degreeOfParallelism = 4)
        {
            List<QHTickLevel2> res = new List<QHTickLevel2>();
            StreamReader reader = new StreamReader(new MemoryStream(content));
            List<string> lines = new List<string>();
            while (!reader.EndOfStream)
            {
                lines.Add(reader.ReadLine());
                res.Add(null);
            }

            Parallel.ForEach(lines, new ParallelOptions { MaxDegreeOfParallelism = degreeOfParallelism }, (line, state, index) =>
             {

                 var splittedLine = line.Split(new char[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                 if (splittedLine.Count() == 0)
                 {
                     throw new InvalidDataException("Blank line not at the end of the file is not allowed.");
                 }
                 QHTickLevel2 tick = new QHTickLevel2();
                 var dateAndPartStr = splittedLine[1].Split('-');
                 if (dateAndPartStr.Count() == 2)
                 {
                     tick.Part = int.Parse(dateAndPartStr[1]);
                     splittedLine[1] = dateAndPartStr[0];
                 }
                 tick.Time = DateTime.Parse(splittedLine[0] + " " + splittedLine[1], CultureInfo.InvariantCulture);
                 List<KeyValuePair<decimal, decimal>> Bids = new List<KeyValuePair<decimal, decimal>>();
                 List<KeyValuePair<decimal, decimal>> Asks = new List<KeyValuePair<decimal, decimal>>();
                 int i = 2;
                 int linPartCnt = splittedLine.Count();
                 if (i < linPartCnt && splittedLine[i] == "bid")
                 {
                     i++;
                     while (i < linPartCnt && splittedLine[i] != "ask")
                     {
                         Bids.Add(new KeyValuePair<decimal, decimal>(decimal.Parse(splittedLine[i], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture),
                             decimal.Parse(splittedLine[i + 1], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture)));
                         i += 2;
                     }
                 }
                 if (i < linPartCnt && splittedLine[i] == "ask")
                 {
                     i++;
                     while (i < linPartCnt)
                     {
                         Asks.Add(new KeyValuePair<decimal, decimal>(decimal.Parse(splittedLine[i], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture),
                             decimal.Parse(splittedLine[i + 1], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture)));
                         i += 2;
                     }
                 }

                 tick.Bids = Bids.ToArray();
                 tick.Asks = Asks.ToArray();

                 res[(int)index] = tick;
             });

            return res;
        }

        public static IEnumerable<QHTickLevel2> DeserializeTicksLevel2Binary(byte[] content, int degreeOfParallelism = 4)
        {
            List<QHTickLevel2> res = new List<QHTickLevel2>();
            QHTickLevel2 tick;
            int contentIndex = 0;

            while (contentIndex < content.Count())
            {
                tick = new QHTickLevel2();
                tick.Time = DateTime.FromBinary(BitConverter.ToInt64(content, contentIndex));
                contentIndex += sizeof(Int64);
                tick.Part = content[contentIndex];
                contentIndex += 1;

                List<KeyValuePair<decimal, decimal>> level2Collection = new List<KeyValuePair<decimal, decimal>>();

                byte bidCnt = content[contentIndex];
                contentIndex += 1;
                tick.Bids = new KeyValuePair<decimal, decimal>[bidCnt];
                for (int i = 0; i < bidCnt; i++)
                {
                    decimal price = (decimal)BitConverter.ToDouble(content, contentIndex);
                    contentIndex += sizeof(double);
                    double volume = BitConverter.ToDouble(content, contentIndex);
                    contentIndex += sizeof(double);
                    tick.Bids[i] = new KeyValuePair<decimal, decimal>(price, (decimal)volume);
                }
                byte askCnt = content[contentIndex];
                contentIndex += 1;
                tick.Asks = new KeyValuePair<decimal, decimal>[askCnt];
                for (int i = 0; i < askCnt; i++)
                {
                    decimal price = (decimal)BitConverter.ToDouble(content, contentIndex);
                    contentIndex += sizeof(double);
                    double volume = BitConverter.ToDouble(content, contentIndex);
                    contentIndex += sizeof(double);
                    tick.Asks[i] = new KeyValuePair<decimal, decimal>(price, (decimal)volume);
                }
                
                res.Add(tick);
            }

            return res;
        }

        internal static byte[] Serialize(IEnumerable<QHItem> chunk)
        {
            List<byte> res = new List<byte>();
            foreach (var it in chunk)
            {
                res.AddRange(it.Serialize());
            }
            return res.ToArray();
        }

        internal static byte[] SerializeBinary(IEnumerable<QHItem> chunk)
        {
            List<byte> res = new List<byte>();
            res.AddRange(ASCIIEncoding.ASCII.GetBytes("BQH"));
            foreach (var it in chunk)
            {
                res.AddRange(it.SerializeBinary());
            }
            return res.ToArray();
        }


    }
}
