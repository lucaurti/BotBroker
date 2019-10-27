using System;
using System.Collections.Generic;
using System.Linq;
using Broker.Common.Utility;
using Serilog;

namespace Broker.Common.Indicators
{
    public class MACDSignal
    {

        // variables
        int pSlowEMA, pFastEMA, pSignalEMA;
        EMAAverage slowEMA, fastEMA, signalEMA;


        public MACDSignal(int pPFastEMA, int pPSlowEMA, int pPSignalEMA, List<MyCandle> candles)
        {
            pFastEMA = pPFastEMA;
            pSlowEMA = pPSlowEMA;
            pSignalEMA = pPSignalEMA;

            slowEMA = new EMAAverage(pSlowEMA);
            fastEMA = new EMAAverage(pFastEMA);
            signalEMA = new EMAAverage(pSignalEMA);

            foreach (var tick in candles)
                ReceiveTick(tick, false);

        }

        public void ReceiveTick(MyCandle candle, bool saveOnDB = true)
        {
            slowEMA.ReceiveTick(candle.Close);
            fastEMA.ReceiveTick(candle.Close);
            if (slowEMA.isPrimed() && fastEMA.isPrimed())
                signalEMA.ReceiveTick(fastEMA.Value() - slowEMA.Value());
                
            if (saveOnDB && signalEMA.isPrimed())
            {
                decimal MACD = fastEMA.Value() - slowEMA.Value();
                decimal signal = signalEMA.Value();
                decimal hist = MACD - signal;
                Log.Debug("-> Signal new MACD values");
                Log.Debug("MACD      : " + MACD.ToStringRound(4));
                Log.Debug("Signal    : " + signal.ToStringRound(4));
                Log.Debug("Hist      : " + hist.ToStringRound(4));

                // save it to db
                BrokerDBContext db = new BrokerDBContext();
                MyMACD MyMacd;
                MyMacd = new MyMACD();
                MyMacd.Timestamp = DateTime.Now.ToEpochTime();
                MyMacd.FastValue = fastEMA.Value();
                MyMacd.SlowValue = slowEMA.Value();
                MyMacd.SignalValue = signalEMA.Value();
                MyMacd.MACD = MACD;
                MyMacd.Hist = hist;
                MyMacd.Candle = db.MyCandles.First(s => s.Id == candle.Id);
                db.MyMACDs.Add(MyMacd);
                db.SaveChanges();
            }
        }
        public void Value(out decimal MACD, out decimal signal, out decimal hist)
        {
            MACD = 0; signal = 0; hist = 0;
            if (signalEMA.isPrimed())
            {
                MACD = fastEMA.Value() - slowEMA.Value();
                signal = signalEMA.Value();
                hist = MACD - signal;
            }
        }

        public bool isPrimed()
        {
            return signalEMA.isPrimed();
        }
        public int PeriodElaborated() 
        {
            return signalEMA.PeriodElaborated();
        }

    }

}