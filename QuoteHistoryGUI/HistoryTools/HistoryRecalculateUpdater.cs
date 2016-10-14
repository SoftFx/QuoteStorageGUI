using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuoteHistoryGUI.HistoryTools
{
    class HistoryRecalculateUpdater
    {
        public static void RecalculateTickToM1(IEnumerable<QHTick> ticks, ref IEnumerable<QHBar> bids, ref IEnumerable<QHBar> asks)
        {
            if(ticks != null && bids != null && asks != null)
            {

                var itBid = bids.GetEnumerator();
                var itAsk = asks.GetEnumerator();

                var currentUpTime = new DateTime();
                var resBid = new List<QHBar>();
                var resAsk = new List<QHBar>();

                var curBarBid = new QHBar();
                var curBarAsk = new QHBar();

                var lastTick = new QHTick();

                bool updateStarted = false;
                itBid.MoveNext();
                itAsk.MoveNext();
                foreach (var tick in ticks)
                {
                    if (tick.Time.Minute != currentUpTime.Minute)
                    {                        
                        if (updateStarted)
                        {
                            curBarBid.Close = lastTick.Bid;
                            while (itBid.Current!=null && itBid.Current.Time < currentUpTime)
                            {
                                resBid.Add(itBid.Current);
                                itBid.MoveNext();
                            }
                            resBid.Add(curBarBid);
                            if (itBid.Current != null && itBid.Current.Time == currentUpTime)
                                itBid.MoveNext();

                            curBarAsk.Close = lastTick.Ask;
                            while (itAsk.Current!=null && itAsk.Current.Time < currentUpTime)
                            {
                                resAsk.Add(itAsk.Current);
                                itAsk.MoveNext();
                            }
                            resAsk.Add(curBarAsk);
                            if (itAsk.Current != null && itAsk.Current.Time == currentUpTime)
                                itAsk.MoveNext();
                        }

                        updateStarted = true;
                        currentUpTime = tick.Time;
                        currentUpTime = currentUpTime.AddSeconds(-tick.Time.Second);
                        currentUpTime = currentUpTime.AddMilliseconds(-tick.Time.Millisecond);
                        
                        curBarBid = new QHBar();
                        curBarAsk = new QHBar();

                        curBarBid.Time = currentUpTime;
                        curBarAsk.Time = currentUpTime;

                        curBarBid.Open = tick.Bid;
                        curBarBid.High = tick.Bid;
                        curBarBid.Low = tick.Bid;
                        curBarBid.Close = tick.Bid;
                        curBarBid.Volume = 0;

                        curBarAsk.Open = tick.Ask;
                        curBarAsk.High = tick.Ask;
                        curBarAsk.Low = tick.Ask;
                        curBarAsk.Close = tick.Ask;
                        curBarAsk.Volume = 0;
                    }
                    lastTick = tick;
                    if (curBarBid.High < tick.Bid)
                        curBarBid.High = tick.Bid;
                    if (curBarBid.Low > tick.Bid)
                        curBarBid.Low = tick.Bid;
                    curBarBid.Volume += tick.BidVolume;

                    if (curBarAsk.High < tick.Ask)
                        curBarAsk.High = tick.Ask;
                    if (curBarAsk.Low > tick.Ask)
                        curBarAsk.Low = tick.Ask;
                    curBarAsk.Volume += tick.AskVolume;

                }
                curBarBid.Close = lastTick.Bid;
                while (itBid.Current != null && itBid.Current.Time < currentUpTime)
                {
                    resBid.Add(itBid.Current);
                    itBid.MoveNext();
                }
                resBid.Add(curBarBid);
                if (itBid.Current != null && itBid.Current.Time == currentUpTime)
                    itBid.MoveNext();

                curBarAsk.Close = lastTick.Ask;
                while (itAsk.Current != null && itAsk.Current.Time < currentUpTime)
                {
                    resBid.Add(itAsk.Current);
                    itAsk.MoveNext();
                }
                resAsk.Add(curBarAsk);
                if (itAsk.Current != null && itAsk.Current.Time == currentUpTime)
                    itAsk.MoveNext();
                bids = resBid;
                asks = resAsk;
            }
            

        }
    }
}
