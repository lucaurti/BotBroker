using System;
using System.Collections.Generic;
using System.Linq;
using Broker.Common.Utility;
using Serilog;

namespace Broker.Common.Indicators
{
    public class EMATwoAverage
    {
        int pSlowEMA, pFastEMA;
        EMAAverage slowEMA, fastEMA;

        public EMATwoAverage(int pPFastEMA, int pPSlowEMA, List<MyCandle> candles)
        {
            pFastEMA = pPFastEMA;
            pSlowEMA = pPSlowEMA;

            slowEMA = new EMAAverage(pSlowEMA);
            fastEMA = new EMAAverage(pFastEMA);

            foreach (var tick in candles)
                ReceiveTick(tick);

        }

        public void ReceiveTick(MyCandle candle)
        {
            slowEMA.ReceiveTick(candle.Close);
            fastEMA.ReceiveTick(candle.Close);

            if (slowEMA.isPrimed())
            {
                decimal hist = fastEMA.Value() - slowEMA.Value();
                Log.Debug("-> Signal new EMA values");
                Log.Debug("Slow      : " + slowEMA.Value().ToStringRound(4));
                Log.Debug("Fast      : " + fastEMA.Value().ToStringRound(4));
                Log.Debug("Hist      : " + hist.ToStringRound(4));

                // save it to db
                using (BrokerDBContext db = new BrokerDBContext())
                {
                    MyMACD MyMacd;
                    MyMacd = new MyMACD();
                    MyMacd.Timestamp = DateTime.Now.ToEpochTime();
                    MyMacd.FastValue = fastEMA.Value();
                    MyMacd.SlowValue = slowEMA.Value();
                    MyMacd.Hist = hist;
                    MyMacd.Candle = db.MyCandles.First(s => s.Id == candle.Id);
                    db.MyMACDs.Add(MyMacd);
                    db.SaveChanges();
                }
            }
        }

        public void Value(out decimal fast, out decimal slow, out decimal hist)
        {
            fast = 0; slow = 0; hist = 0;
            if (slowEMA.isPrimed())
            {
                fast = fastEMA.Value();
                slow = slowEMA.Value();
                hist = fastEMA.Value() - slowEMA.Value();
                Log.Debug("-> Signal new EMA values");
                Log.Debug("Slow      : " + slowEMA.Value().ToStringRound(4));
                Log.Debug("Fast      : " + fastEMA.Value().ToStringRound(4));
                Log.Debug("Hist      : " + hist.ToStringRound(4));

                // save it to db
                using (BrokerDBContext db = new BrokerDBContext())
                {
                    MyMACD MyMacd;
                    MyMacd = new MyMACD();
                    MyMacd.Timestamp = DateTime.Now.ToEpochTime();
                    MyMacd.FastValue = fastEMA.Value();
                    MyMacd.SlowValue = slowEMA.Value();
                    MyMacd.Hist = hist;
                    db.MyMACDs.Add(MyMacd);
                    db.SaveChanges();
                }
            }
        }

        public bool isPrimed()
        {
            return slowEMA.isPrimed();
        }

        public int PeriodElaborated()
        {
            return slowEMA.PeriodElaborated();
        }
    }

}