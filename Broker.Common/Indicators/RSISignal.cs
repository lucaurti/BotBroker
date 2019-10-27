using System;
using System.Collections.Generic;
using System.Linq;
using Broker.Common.Utility;
using Serilog;

namespace Broker.Common.Indicators
{

    public class RSISignal
    {
        private RSIAverage emav;

        public RSISignal(int pPeriods, List<MyCandle> candles)
        {
            emav = new RSIAverage(pPeriods);
            foreach (var tick in candles)
                ReceiveTick(tick, false);
        }

        public void ReceiveTick(MyCandle candle, bool saveOnDB = true)
        {
            emav.ReceiveTick(candle.Close);

            if (saveOnDB && emav.isPrimed())
            {
                Log.Debug("-> Signal new RSI");
                Log.Debug("RSI  : " + emav.Value().ToStringRound(2));

                // save it to db
                BrokerDBContext db = new BrokerDBContext();
                MyRSI MyRSI;
                MyRSI = new MyRSI();
                MyRSI.Timestamp = DateTime.Now.ToEpochTime();
                MyRSI.RSIValue = emav.Value();
                MyRSI.Candle = db.MyCandles.First(s => s.Id == candle.Id);
                db.MyRSIs.Add(MyRSI);
                db.SaveChanges();
            }
        }
        public decimal Value()
        {
            return emav.isPrimed() ? emav.Value() : 0;
        }
        public bool isPrimed()
        {    
            return emav.isPrimed();
        }
        public int PeriodElaborated() 
        {
            return emav.PeriodElaborated();
        }

    }

}