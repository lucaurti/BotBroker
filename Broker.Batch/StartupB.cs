using Broker.Common.Strategies;
using Broker.Common.Utility;
using Broker.Common.WebAPI;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Text;

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


        // services endpoint
        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                Log.Information("*** Broker started ***");

                // init
                myWebAPIList = ServiceLocator.Current.GetInstance<IList<MyWebAPI>>();
                strategy = ServiceLocator.Current.GetInstance<MyStrategy>();
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
                }
            }
            catch (Exception ex)
            {
                Utils.LogError(ex);
            }
            return Task.CompletedTask;
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
