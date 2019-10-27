using System;
using System.Collections.Generic;
using System.Linq;
using Broker.Common.Utility;
using Serilog;

namespace Broker.Common.Indicators
{

    public class MomentumSignal
    {
        private MomentumAverage emav;

        public MomentumSignal(int pPeriods, List<MyCandle> candles)
        {
            emav = new MomentumAverage(pPeriods);
            foreach (var tick in candles)
                ReceiveTick(tick, false);
        }

        public void ReceiveTick(MyCandle candle, bool saveOnDB = true)
        {
            emav.ReceiveTick(candle.Close);

            if (saveOnDB && emav.isPrimed())
            {
                Log.Debug("-> Signal new Momentum");
                Log.Debug("Momentum  : " + emav.Value().ToStringRound(2));

                // save it to db
                BrokerDBContext db = new BrokerDBContext();
                MyMomentum MyMomentum;
                MyMomentum = new MyMomentum();
                MyMomentum.Timestamp = DateTime.Now.ToEpochTime();
                MyMomentum.MomentumValue = emav.Value();
                MyMomentum.Candle = db.MyCandles.First(s => s.Id == candle.Id);
                db.MyMomentums.Add(MyMomentum);
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