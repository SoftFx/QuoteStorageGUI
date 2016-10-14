using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuoteHistoryGUI.HistoryTools
{


    class QHItem
    {
        public DateTime Time;
    }
    class QHBar: QHItem
    {
        public decimal Open;
        public decimal High;
        public decimal Low;
        public decimal Close;
        public decimal Volume;
    }
    class QHTick : QHItem
    {
        public decimal Bid;
        public decimal BidVolume;
        public decimal Ask;
        public decimal AskVolume;
    }

    class QHTickLevel2 : QHItem
    {
        public KeyValuePair<decimal,decimal>[] Bids;
        public KeyValuePair<decimal, decimal>[] Asks;
        public KeyValuePair<decimal, decimal> BestBid { get { return Bids.Count()>0?Bids.Last():new KeyValuePair<decimal, decimal>(); } }
        public KeyValuePair<decimal, decimal> BestAsk { get { return Asks.Count() > 0 ? Asks.Last() : new KeyValuePair<decimal, decimal>();} }
    }

    class HistorySerializer
    {
        public static IEnumerable<QHItem> Deserialize(string period, byte[] content)
        {
            if (content == null)
                return new List<QHItem>();
            if(period == "ticks")
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
                var splittedLine = reader.ReadLine().Split('\t');
                QHBar bar = new QHBar();
                bar.Time = DateTime.Parse(splittedLine[0], CultureInfo.InvariantCulture);
                bar.Open = decimal.Parse(splittedLine[1], CultureInfo.InvariantCulture);
                bar.High = decimal.Parse(splittedLine[2], CultureInfo.InvariantCulture);
                bar.Low = decimal.Parse(splittedLine[3], CultureInfo.InvariantCulture);
                bar.Close = decimal.Parse(splittedLine[4], CultureInfo.InvariantCulture);
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
                var splittedLine = reader.ReadLine().Split('\t');
                QHTick tick = new QHTick();
                tick.Time = DateTime.Parse(splittedLine[0], CultureInfo.InvariantCulture);
                tick.Bid = decimal.Parse(splittedLine[1], CultureInfo.InvariantCulture);
                tick.BidVolume = decimal.Parse(splittedLine[2], CultureInfo.InvariantCulture);
                tick.Ask = decimal.Parse(splittedLine[3], CultureInfo.InvariantCulture);
                tick.AskVolume = decimal.Parse(splittedLine[4], CultureInfo.InvariantCulture);
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
                var splittedLine = reader.ReadLine().Split('\t');
                QHTickLevel2 tick = new QHTickLevel2();
                tick.Time = DateTime.Parse(splittedLine[0], CultureInfo.InvariantCulture);
                List<KeyValuePair<decimal, decimal>> Bids = new List<KeyValuePair<decimal, decimal>>();
                List<KeyValuePair<decimal, decimal>> Asks = new List<KeyValuePair<decimal, decimal>>();
                int i = 1;
                if(i < splittedLine.Count() && splittedLine[i]=="bid")
                {
                    i++;
                    while(i < splittedLine.Count() && splittedLine[i] != "ask")
                    {
                        Bids.Add(new KeyValuePair<decimal, decimal>(decimal.Parse(splittedLine[i], CultureInfo.InvariantCulture), decimal.Parse(splittedLine[i+1], CultureInfo.InvariantCulture)));
                        i += 2;
                    }
                }
                if (i < splittedLine.Count() && splittedLine[i] == "ask")
                {
                    i++;
                    while (i < splittedLine.Count())
                    {
                        Asks.Add(new KeyValuePair<decimal, decimal>(decimal.Parse(splittedLine[i], CultureInfo.InvariantCulture), decimal.Parse(splittedLine[i+1], CultureInfo.InvariantCulture)));
                        i += 2;
                    }
                }


                tick.Bids = Bids.ToArray();
                tick.Asks = Asks.ToArray();

                res.Add(tick);
            }
            return res;
        }


        public static byte[] SerializeBars(IEnumerable<QHBar> bars)
        {
            MemoryStream stream = new MemoryStream();
            List<string> res = new List<string>(); 
                        foreach(var bar in bars)
            {
                string barStr = "";
                barStr += bar.Time.ToString(CultureInfo.InvariantCulture);
                barStr += "\t";
                barStr += bar.Open.ToString(CultureInfo.InvariantCulture);
                barStr += "\t";
                barStr += bar.High.ToString(CultureInfo.InvariantCulture);
                barStr += "\t";
                barStr += bar.Low.ToString(CultureInfo.InvariantCulture);
                barStr += "\t";
                barStr += bar.Close.ToString(CultureInfo.InvariantCulture);
                barStr += "\t";
                barStr += bar.Volume.ToString(CultureInfo.InvariantCulture);
                barStr += "\r\n";
                res.Add(barStr);
            }
            return ASCIIEncoding.ASCII.GetBytes(string.Concat(res));
        }

    }
}
