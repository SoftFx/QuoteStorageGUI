using System;
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
    }
    public class QHBar : QHItem
    {
        public decimal Open;
        public decimal High;
        public decimal Low;
        public decimal Close;
        public decimal Volume;

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

    }
    public class QHTick : QHItem
    {
        public decimal Bid;
        public decimal BidVolume;
        public decimal Ask;
        public decimal AskVolume;
        public int Part = 0;
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
    }

    public class QHTickLevel2 : QHItem
    {
        public KeyValuePair<decimal, decimal>[] Bids;
        public KeyValuePair<decimal, decimal>[] Asks;
        public KeyValuePair<decimal, decimal> BestBid { get { return Bids.Count() > 0 ? Bids.Last() : new KeyValuePair<decimal, decimal>(); } }
        public KeyValuePair<decimal, decimal> BestAsk { get { return Asks.Count() > 0 ? Asks.First() : new KeyValuePair<decimal, decimal>(); } }
        public int Part = 0;
        public override byte[] Serialize()
        {
            throw new NotImplementedException();
        }
    }

    class HistorySerializer
    {
        public static IEnumerable<QHItem> Deserialize(string period, byte[] content)
        {
            if (content == null)
                return new List<QHItem>();
            if (period == "ticks")
            {
                return DeserializeTicks(content);
            }
            else if (period == "ticks level2")
            {
                return DeserializeTicksLevel2(content);
            }
            else if (period == "M1 ask" || period == "M1 bid")
            {
                return DeserializeBars(content);
            }

            return null;
        }


        public static IEnumerable<QHBar> DeserializeBars(byte[] content)
        {
            List<QHBar> res = new List<QHBar>();
            StreamReader reader = new StreamReader(new MemoryStream(content));
            while (!reader.EndOfStream)
            {
                var splittedLine = reader.ReadLine().Split(new char[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
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
            return res;
        }


        public static IEnumerable<QHTick> DeserializeTicks(byte[] content)
        {
            List<QHTick> res = new List<QHTick>();
            StreamReader reader = new StreamReader(new MemoryStream(content));
            while (!reader.EndOfStream)
            {
                var splittedLine = reader.ReadLine().Split(new char[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
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
            return res;
        }

        public static IEnumerable<QHTickLevel2> DeserializeTicksLevel2(byte[] content)
        {
            List<QHTickLevel2> res = new List<QHTickLevel2>();
            StreamReader reader = new StreamReader(new MemoryStream(content));
            while (!reader.EndOfStream)
            {
                var splittedLine = reader.ReadLine().Split(new char[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
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
                if (i < splittedLine.Count() && splittedLine[i] == "bid")
                {
                    i++;
                    while (i < splittedLine.Count() && splittedLine[i] != "ask")
                    {
                        Bids.Add(new KeyValuePair<decimal, decimal>(decimal.Parse(splittedLine[i], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture),
                            decimal.Parse(splittedLine[i + 1], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture)));
                        i += 2;
                    }
                }
                if (i < splittedLine.Count() && splittedLine[i] == "ask")
                {
                    i++;
                    while (i < splittedLine.Count())
                    {
                        Asks.Add(new KeyValuePair<decimal, decimal>(decimal.Parse(splittedLine[i], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture),
                            decimal.Parse(splittedLine[i + 1], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture)));
                        i += 2;
                    }
                }


                tick.Bids = Bids.ToArray();
                tick.Asks = Asks.ToArray();

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

        
    }
}
