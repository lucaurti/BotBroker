using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Broker.Common.Strategies;
using Broker.Common.Utility;
using Broker.Common.WebAPI;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Broker.Batch
{
    class StartupB : IHostedService, IDisposable
    {
        // properities
        private IList<MyWebAPI> myWebAPIList { get; set; } = null;
        private MyStrategy strategy { get; set; } = null;
        private Timer timerTicker { get; set; } = null;
        private Timer timerCandle { get; set; } = null;
        private Timer timerRemoveOldTicker { get; set; } = null;
        private Timer timerRemoveOldRsi { get; set; } = null;
        private Timer timerRemoveOldMacd { get; set; } = null;
        private Timer timerRemoveOldMomentum { get; set; } = null;
        private Timer timerIamAlive { get; set; } = null;


        // services endpoint
        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                Log.Information("*** Broker started ***");

                // init
                myWebAPIList = ServiceLocator.Current.GetInstance<IList<MyWebAPI>>();
                strategy = ServiceLocator.Current.GetInstance<MyStrategy>();
                TestCode(myWebAPIList.FirstOrDefault());
                foreach (MyWebAPI webapi in myWebAPIList)
                {
                    //tickers
                    if (!Extension.UseWebSocketTickers)
                        timerTicker = new Timer(
                            (e) => TimerTicker_Elapsed(webapi),
                            null,
                            TimeSpan.Zero,
                            TimeSpan.FromSeconds(Misc.GetTickerTime));

                    //candles
                    timerCandle = new Timer(
                        (e) => TimerCandle_Elapsed(webapi),
                        null,
                        Misc.RoundDateTimeCandle,
                        TimeSpan.FromMinutes(Misc.GetCandleTime));

                    // remove old Candle
                    timerRemoveOldTicker = new Timer(
                        (e) => TimerRemoveOldCandle_Elapsed(webapi),
                        null,
                        Misc.RoundDateTimeCandle,
                        TimeSpan.FromMinutes(Misc.GetCandleTime));

                    // remove old Ticker
                    timerRemoveOldTicker = new Timer(
                        (e) => TimerRemoveOldTicker_Elapsed(webapi),
                        null,
                        Misc.RoundDateTimeCandle,
                        TimeSpan.FromMinutes(Misc.GetCandleTime));

                    // remove old rsi
                    timerRemoveOldRsi = new Timer(
                        (e) => TimerRemoveOldRSI_Elapsed(webapi),
                        null,
                        Misc.RoundDateTimeCandle,
                        TimeSpan.FromMinutes(Misc.GetCandleTime));

                    // remove old momentum
                    timerRemoveOldMomentum = new Timer(
                        (e) => TimerRemoveOldMomentum_Elapsed(webapi),
                        null,
                        Misc.RoundDateTimeCandle,
                        TimeSpan.FromMinutes(Misc.GetCandleTime));

                    // remove old macd
                    timerRemoveOldMacd = new Timer(
                        (e) => TimerRemoveOldMacd_Elapsed(webapi),
                        null,
                        Misc.RoundDateTimeCandle,
                        TimeSpan.FromMinutes(Misc.GetCandleTime));

                    // remove old macd
                    timerIamAlive = new Timer(
                        (e) => TimerIamAlive_Elapsed(webapi),
                        null,
                        Misc.RoundDateTimeCandle,
                        TimeSpan.FromHours(24));
                }
            }
            catch (Exception ex)
            {
                Utils.LogError(ex);
            }
            return Task.CompletedTask;
        }

        private static void TestCode(MyWebAPI webapi)
        {
            //var list = new List<Common.WebAPI.Models.MyOrderBook>();
            //webapi.GetOrderBook(out list);
            //decimal ris = 0;
            //foreach (var item in list)
            //{
            //    if (item.Action == Enumerator.TradeAction.Long)
            //        ris = ris + item.Volume;
            //    else
            //        ris = ris + (-item.Volume);
            //}
            //string a = ris.ToString(); var averageCloseCandle = db.MyCandles.OrderByDescending(s => s.Date).Take(50).ToList().Average(s => s.Close);
            //}

            //double ris = 0;
            //var list = new List<Common.WebAPI.Models.MyTrade>();
            //webapi.GetTrades(out list);
            //foreach (var item in list)
            //{
            //    if (item.action == Enumerator.TradeAction.Long)
            //        ris += Convert.ToDouble(item.qty);
            //    else
            //        ris += (Convert.ToDouble(item.qty) * Convert.ToDouble(-1));
            //}
            //string a = ris.ToString();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                timerTicker?.Change(Timeout.Infinite, 0);
                timerCandle?.Change(Timeout.Infinite, 0);
                timerRemoveOldTicker?.Change(Timeout.Infinite, 0);
                timerRemoveOldMacd?.Change(Timeout.Infinite, 0);
                timerRemoveOldMomentum?.Change(Timeout.Infinite, 0);
                timerRemoveOldRsi?.Change(Timeout.Infinite, 0);
                Log.Information("*** Broker ended ***");

            }
            catch (Exception ex)
            {
                Utils.LogError(ex);
            }
            return Task.CompletedTask;
        }


        // events
        private void TimerTicker_Elapsed(MyWebAPI webapi)
        {
            try
            {
                webapi.GetTicker();
                Log.Debug("Timer ticker elapsed...");
            }
            catch (Exception ex)
            {
                Utils.LogError(ex);
            }
        }

        private void TimerCandle_Elapsed(MyWebAPI webapi)
        {
            try
            {
                webapi.GetCandle();
                Log.Debug("Timer candle elapsed...");
            }
            catch (Exception ex)
            {
                Utils.LogError(ex);
            }
        }

        private void TimerRemoveOldCandle_Elapsed(MyWebAPI webapi)
        {
            try
            {
                webapi.RemoveOldCandle();
                Log.Debug("Timer Remove Old Candle elapsed...");
            }
            catch (Exception ex)
            {
                Utils.LogError(ex);
            }
        }

        private void TimerRemoveOldTicker_Elapsed(MyWebAPI webapi)
        {
            try
            {
                webapi.RemoveOldTicker();
                Log.Debug("Timer Remove Old Ticker elapsed...");
            }
            catch (Exception ex)
            {
                Utils.LogError(ex);
            }
        }

        private void TimerRemoveOldRSI_Elapsed(MyWebAPI webapi)
        {
            try
            {
                webapi.RemoveOldRSI();
                Log.Debug("Timer Remove Old rsi elapsed...");
            }
            catch (Exception ex)
            {
                Utils.LogError(ex);
            }
        }

        private void TimerRemoveOldMacd_Elapsed(MyWebAPI webapi)
        {
            try
            {
                webapi.RemoveOldMacd();
                Log.Debug("Timer Remove Old macd elapsed...");
            }
            catch (Exception ex)
            {
                Utils.LogError(ex);
            }
        }

        private void TimerRemoveOldMomentum_Elapsed(MyWebAPI webapi)
        {
            try
            {
                webapi.RemoveOldMomentums();
                Log.Debug("Timer Remove Old Momentum elapsed...");
            }
            catch (Exception ex)
            {
                Utils.LogError(ex);
            }
        }

        private void TimerIamAlive_Elapsed(MyWebAPI webapi)
        {
            try
            {
                strategy.IamAlive();
                Log.Debug("Timer I am Alive elapsed...");
            }
            catch (Exception ex)
            {
                Utils.LogError(ex);
            }
        }

        // functions
        public void Dispose()
        {
            try
            {
                timerTicker?.Dispose();
                timerCandle?.Dispose();
                timerRemoveOldTicker?.Dispose();
                timerRemoveOldMacd?.Dispose();
                timerRemoveOldMomentum?.Dispose();
                timerRemoveOldRsi?.Dispose();
            }
            catch (Exception ex)
            {
                Utils.LogError(ex);
            }
        }
    }
}
