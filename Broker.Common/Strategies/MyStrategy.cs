using Broker.Common.Events;
using Broker.Common.WebAPI;
using System;
using System.Linq;
using System.Collections.Generic;
using static Broker.Common.Strategies.Enumerator;
using Broker.Common.Utility;
using System.Threading;
using Serilog;
using Broker.Common.Indicators;
using Broker.Common.WebAPI.Models;
using System.Threading.Tasks;

namespace Broker.Common.Strategies
{

    public class MyStrategy
    {

        // variables
        private readonly IStrategy strategies;
        private readonly IList<MyWebAPI> webAPI;
        private readonly IEvents events;
        private readonly MyAuthentication authentication;
        private readonly int requiredHistory, exposedPercentage;
        private bool isInWarmUp = true, mustStartTicker = true;


        // properties
        private Timer timerOrder { get; set; } = null;
        private int retryTimes { get; set; } = 1;
        private DateTime strategyStarted { get; set; } = DateTime.Now;
        private bool isTradeInError { get; set; } = false;
        public static MACDSignal MACDAverage { get; set; } = null;
        public static MomentumSignal MomentumAverage { get; set; } = null;
        public static RSISignal RSIAverage { get; set; } = null;


        // init
        public MyStrategy(IStrategy Strategies, IEvents Events, IList<MyWebAPI> WebAPI)
        {

            // variables
            webAPI = WebAPI; events = Events; strategies = Strategies;
            authentication = Extension.GetAuthentication;
            requiredHistory = int.Parse(Misc.GetParameterValue("requiredHistory"));
            exposedPercentage = int.Parse(Misc.GetParameterValue("exposedPercentage"));
            var config = ServiceLocator.Current.GetInstance<IConfigurationManager>();

            // interface events
            Events.onTickerUpdate += Strategies.Events_onTickerUpdate;
            Events.onTradeCompleted += Strategies.Events_onTradeCompleted;
            Events.onTradeAborted += Strategies.Events_onTradeAborted;
            Events.onTradeCancelled += Strategies.Events_onTradeCancelled;
            Events.onTradeErrored += Strategies.Events_onTradeErrored;
            Events.onWarmUpEnded += Strategies.Events_onWarmUpEnded;

            // local events
            Events.onTickerUpdate += Events_onTickerUpdate;
            Events.onCandleUpdate += Events_onCandleUpdate;
            Events.myTradeRequest += Events_myTradeRequest;
            Events.myWarmUpRequest += Events_myWarmUpRequest;

            // telegram
            if (config.mustStartTelegram)
            {
                string telegramToken = Misc.GetTelegramToken;
                strategies.TelegramBotPassword = Misc.GetTelegramPassword;
                strategies.TelegramBotUsernameTo = Misc.GetTelegramUsernameTo;
                strategies.TelegramBotClient = new Telegram.Bot.TelegramBotClient(telegramToken);
                strategies.TelegramBotClient.OnMessage += Events_onTelegramMessage;
                strategies.TelegramBotClient.StartReceiving();
                Log.Debug("-> Telegram BOT started");
                Log.Debug("Token : " + telegramToken);
            }

            // build indicators
            using (BrokerDBContext db = new BrokerDBContext())
            {
                List<MyCandle> candles = db.MyCandles
                .OrderByDescending(s => s.Date)
                .Take(50).OrderBy(s => s.Date).ToList();
                Log.Information("-> INIT average indicators");
                Log.Information("Found candle        : " + candles.Count);
                MACDAverage = new MACDSignal(12, 26, 9, candles);
                MomentumAverage = new MomentumSignal(10, candles);
                RSIAverage = new RSISignal(14, candles);
            }
        }

        // events
        private void Events_myTradeRequest(MyWebAPISettings settings, TradeAction tradeType,
            int? percentage = null, decimal? price = null, decimal? priceLimit = null)
        {
            // variables
            MyTradeCompleted tradeCompleted = new MyTradeCompleted();
            List<MyBalance> balances;
            MyBalance currency, asset;
            using (BrokerDBContext db = new BrokerDBContext())
            {
                decimal volume = 0; string orderID;

                // check timer
                if (timerOrder != null)
                {
                    string message = "Timer active, one operation is still in progress.";
                    MyTradeCancelled errored = new MyTradeCancelled
                    {
                        IdReference = tradeCompleted.Id,
                        Reason = message,
                        Settings = settings
                    };
                    Log.Error(errored.Reason);
                    events.OnTradeErrored(errored);
                    return;
                }

                // get balance
                try
                {
                    if (!webAPI.ResolveWebAPI(settings).GetBalance(out balances))
                        throw new Exception("Balance unavailable.");
                    currency = balances.Where(s => s.Asset == settings.Currency).FirstOrDefault();
                    asset = balances.Where(s => s.Asset == settings.Asset).FirstOrDefault();
                    if (currency == null) currency = new MyBalance() { Asset = settings.Currency };
                    if (asset == null) asset = new MyBalance() { Asset = settings.Asset };
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message + Environment.NewLine + ex.ToString());
                    MyTradeCancelled errored = new MyTradeCancelled
                    {
                        IdReference = tradeCompleted.Id,
                        Reason = ex.Message,
                        Settings = settings
                    };
                    events.OnTradeErrored(errored);
                    return;
                }

                // find price if necessary
                if (!price.HasValue)
                {
                    try
                    {
                        List<MyTicker> tickers;
                        if (webAPI.ResolveWebAPI(settings).GetTicker(out tickers))
                        {
                            price = (tradeType == TradeAction.Long) ? tickers.Last().Bid : tickers.Last().Ask;

                            // check spread
                            decimal spread = Math.Abs(tickers.Last().Ask - tickers.Last().Bid);
                            decimal maxSpread = Extension.GetMaxSpread;
                            if (spread > maxSpread)
                            {
                                MyTradeCancelled aborted = new MyTradeCancelled
                                {
                                    IdReference = tradeCompleted.Id,
                                    Reason = "Current spread (" + spread.ToPrecision(settings, TypeCoin.Currency) +
                                        ") is over the limit (" + maxSpread.ToPrecision(settings, TypeCoin.Currency) + ")",
                                    Settings = settings
                                };
                                Log.Debug(aborted.Reason);
                                events.OnTradeAborted(aborted);
                                return;
                            }
                        }
                        else
                            price = GetLastPriceTicker(tradeCompleted.Action);

                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex.Message + Environment.NewLine + ex.ToString());
                        MyTradeCancelled errored = new MyTradeCancelled
                        {
                            IdReference = tradeCompleted.Id,
                            Reason = ex.Message,
                            Settings = settings
                        };
                        events.OnTradeErrored(errored);
                        return;
                    }
                }


                // buy
                if (tradeType == TradeAction.Long)
                {
                    // check funds
                    if (currency.Amount <= 0)
                    {
                        MyTradeCancelled aborted = new MyTradeCancelled
                        {
                            IdReference = tradeCompleted.Id,
                            Reason = "Found insufficient.",
                            Settings = settings
                        };
                        Log.Debug(aborted.Reason);
                        events.OnTradeAborted(aborted);
                        return;
                    }

                    // check exposed
                    price -= Extension.GetSlipPage;
                    if (!Misc.IsPaperTrade)
                    {
                        decimal exposed = (asset.Amount * price.Value) + currency.Amount;
                        exposed = 100 - ((currency.Amount / exposed) * 100);
                        if (exposed > exposedPercentage)
                        {
                            MyTradeCancelled aborted = new MyTradeCancelled
                            {
                                IdReference = tradeCompleted.Id,
                                Reason = "Too exposed, asset: " + asset.Amount.ToPrecision(settings, TypeCoin.Asset) +
                                ", currency: " + currency.Amount.ToPrecision(settings, TypeCoin.Currency) +
                                ", exposed: " + exposed.ToStringRound(2) + "%",
                                Settings = settings
                            };
                            Log.Information(aborted.Reason);
                            events.OnTradeAborted(aborted);
                            return;
                        }

                        // calculate volume
                        volume = (percentage.HasValue ?
                            (
                                (currency.Amount / price.Value) / 100) * percentage.Value :
                                (currency.Amount / price.Value)
                            );
                    }
                    else
                        volume = (percentage.HasValue ?
                            (
                                (1000 / price.Value) / 100) * percentage.Value :
                                (1000 / price.Value)
                            );

                }

                // sell
                else if (tradeType == TradeAction.Short)
                {

                    // calculate volume
                    price += Extension.GetSlipPage;
                    if (!Misc.IsPaperTrade)
                        volume = (percentage.HasValue ?
                            (asset.Amount / 100) * percentage.Value : asset.Amount);
                    else
                        volume = (percentage.HasValue ?
                            ((decimal)0.1 / 100) * percentage.Value : (decimal)0.1);

                }

                // set event handler
                tradeCompleted.Price = price.Value;
                tradeCompleted.Action = tradeType;
                tradeCompleted.Percentage = percentage;
                tradeCompleted.LimitPrice = priceLimit;
                tradeCompleted.Settings = settings;

                // check limit price
                if (priceLimit.HasValue)
                    if ((tradeType == TradeAction.Short && price < priceLimit) ||
                            (tradeType == TradeAction.Long && price > priceLimit))
                    {
                        MyTradeCancelled cancelled = new MyTradeCancelled
                        {
                            IdReference = tradeCompleted.Id,
                            Reason = "Price limit reached up: " + priceLimit.Value.ToPrecision(settings, TypeCoin.Currency),
                            Settings = settings
                        };
                        Log.Information(cancelled.Reason);
                        events.OnTradeCancelled(cancelled);
                        return;
                    }

                // make order
                try
                {
                    if (webAPI.ResolveWebAPI(settings).PostNewOrder(tradeType, volume, price.Value, out orderID))
                    {
                        int retryTime = Extension.GetRetryTime;
                        tradeCompleted.OrderId = orderID;
                        Log.Information("-> Order in progress");
                        Log.Information("Order ID            : " + tradeCompleted.OrderId);
                        Log.Information("Trade               : " + tradeCompleted.Action.ToString());
                        Log.Information("Volume              : " + tradeCompleted.Amount.ToPrecision(settings, TypeCoin.Asset));
                        Log.Information("Trade               : " + tradeCompleted.Price.ToPrecision(settings, TypeCoin.Currency));
                        isTradeInError = false;
                        timerOrder = new Timer(
                            (e) => TimerOrder_Elapsed(tradeCompleted),
                            null,
                            new TimeSpan(0, 0, retryTime),
                            TimeSpan.FromSeconds(retryTime));
                    }
                    else
                        throw new Exception("Order wasn't initiated");
                }
                catch (Exception ex)
                {
                    MyTradeCancelled errored = new MyTradeCancelled
                    {
                        IdReference = tradeCompleted.Id,
                        Reason = ex.Message,
                        Settings = settings
                    };
                    Log.Error(errored.Reason);
                    events.OnTradeErrored(errored);
                    return;
                }
            }
        }

        private void Events_onTickerUpdate(List<MyTicker> myTickers)
        {
            // check warmup status
            if (mustStartTicker)
            {
                using (BrokerDBContext db = new BrokerDBContext())
                {
                    int minutesSub = requiredHistory * Misc.GetCandleTime;
                    long strategyStartedTime = strategyStarted.AddMinutes(minutesSub * -1).ToEpochTime();
                    var tickers = db.MyTickers
                        .Where(s => s.Timestamp >= strategyStartedTime)
                        .ToList();
                    myTickers = myTickers.Union(tickers).ToList();
                    Log.Information("-> Signaling WarmUp tickers");
                    Log.Information("WarmUp tickers       : " + tickers.Count());
                    mustStartTicker = false;
                }
            }

            // execute interface event
            Log.Debug("Tickers found        : " + myTickers.Count());
            strategies.Events_onTickerUpdate(myTickers);
        }

        private void Events_onCandleUpdate(MyCandle myCandle)
        {
            // variables
            List<MyBalance> balances = null;
            using (BrokerDBContext db = new BrokerDBContext())
            {
                // check warmup status
                if (isInWarmUp)
                {
                    int minutesSub = requiredHistory * Misc.GetCandleTime;
                    int numCandleIn = db.MyCandles
                        .Where(s => s.Date >= strategyStarted
                            .AddMinutes(minutesSub * -1))
                        .Take(requiredHistory)
                        .Count();
                    Log.Information("-> Signaling WarmUp candles");
                    Log.Information("WarmUp candles          : " + numCandleIn);
                    Log.Information("WarmUp required candles : " + requiredHistory);

                    if (numCandleIn == requiredHistory)
                    {
                        isInWarmUp = false;
                        events.OnWarmUpEnded();
                    }
                }

                // save balance
                try
                {
                    if (webAPI.ResolveWebAPI(myCandle.Settings).GetBalance(out balances))
                    {
                        foreach (var x in balances)
                        {
                            if ((x.Amount + x.Reserved) == 0) continue;
                            x.Candle = db.MyCandles.First(s => s.Id == myCandle.Id);
                            x.Settings = myCandle.Settings.GenerateMyWebAPISettings(db);
                            if (x.Asset.Trim().ToUpper() == myCandle.Settings.Currency.ToUpper())
                            {
                                x.ToEuro = (x.Amount + x.Reserved).Round(myCandle.Settings.PrecisionCurrency);
                                db.MyBalances.Add(x);
                            }
                            else if (x.Asset.Trim().ToUpper() == myCandle.Settings.Asset.ToUpper())
                            {
                                x.ToEuro = (myCandle.Close * (x.Amount + x.Reserved)).Round(myCandle.Settings.PrecisionCurrency);
                                db.MyBalances.Add(x);
                            }
                            else
                            {
                                List<MyTicker> tickers;
                                MyWebAPISettings settings = new MyWebAPISettings
                                (
                                    Asset: x.Asset,
                                    Currency: myCandle.Settings.Currency,
                                    Separator: myCandle.Settings.Separator,
                                    PrecisionAsset: myCandle.Settings.PrecisionAsset,
                                    PrecisionCurrency: myCandle.Settings.PrecisionCurrency
                                );
                                settings = settings.GenerateMyWebAPISettings(db);
                                if (webAPI.ResolveWebAPI(myCandle.Settings).GetTicker(out tickers, settings, false))
                                {
                                    if (tickers.Count == 0) continue;
                                    x.ToEuro = (tickers[0].LastTrade *
                                        (x.Amount + x.Reserved)).Round(myCandle.Settings.PrecisionCurrency);
                                    x.Settings = db.MyWebAPISettings.First(s => s.Id == settings.Id);
                                    db.MyBalances.Add(x);
                                }
                            }
                        }
                        db.SaveChanges();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message + Environment.NewLine + ex.ToString());
                }

                // update average indicators
                MACDAverage.ReceiveTick(myCandle);
                MomentumAverage.ReceiveTick(myCandle);
                RSIAverage.ReceiveTick(myCandle);

                // excute interface event
                strategies.Events_onCandleUpdate(myCandle);
            }
        }

        private void Events_myWarmUpRequest()
        {
            Log.Information("-> Signaling new WARMUP");
            strategyStarted = DateTime.Now;
            isInWarmUp = true;
            mustStartTicker = true;
        }

        private void Events_onTelegramMessage(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            Log.Information("-> Signaling telegram bot");
            Log.Information("Message: " + e.Message.Text.Replace(strategies.TelegramBotPassword, "*********"));

            if (e.Message.Text.ToLower().StartsWith("/start"))
                this.onEmitOnline(e.Message.Chat.Id, e.Message.From.Id);
            else if (e.Message.Text.ToLower().StartsWith("/ticker"))
                this.onEmitTicker(e.Message.Chat.Id);
            else if (e.Message.Text.ToLower().StartsWith("/candle"))
                this.onEmitCandle(e.Message.Chat.Id);
            else
                strategies.Events_onTelegramMessage(sender, e);
        }

        // telegram
        private void onEmitOnline(long chatId, long fromId)
        {
            strategies.TelegramBotClient.SendTextMessageAsync(chatId, "ChatID: " + chatId + "\nUserID: " + fromId + "\nGreat! You are registred!");
            if (strategies.TelegramBotUsernameTo == null) strategies.TelegramBotUsernameTo = fromId.ToString();
        }

        private void onEmitTicker(long chatId)
        {
            using (BrokerDBContext db = new BrokerDBContext())
            {
                MyTicker ticker = db.MyTickers.OrderByDescending(s => s.Id).FirstOrDefault();
                if (ticker != null)
                {
                    string message = "Last ticker." +
                        "\nTime: " + ticker.Timestamp.ToDateTime().ToShortTimeStringEU() +
                        "\nAsk: " + ticker.Ask.ToPrecision(ticker.Settings, TypeCoin.Currency) +
                        "\nBid: " + ticker.Bid.ToPrecision(ticker.Settings, TypeCoin.Currency) +
                        "\nLast: " + ticker.LastTrade.ToPrecision(ticker.Settings, TypeCoin.Currency) +
                        "\nRolling: " + ticker.Volume.ToStringRound(2);
                    strategies.TelegramBotClient.SendTextMessageAsync(chatId, message);
                }
            }
        }

        private void onEmitCandle(long chatId)
        {
            BrokerDBContext db = new BrokerDBContext();
            MyCandle candle = db.MyCandles.OrderByDescending(s => s.Id).FirstOrDefault();
            if (candle != null)
            {
                string message = "Last candle." +
                    "\nTime: " + candle.Date.ToShortTimeStringEU() +
                    "\nOpen: " + candle.Open.ToPrecision(candle.Settings, TypeCoin.Currency) +
                    "\nClose: " + candle.Close.ToPrecision(candle.Settings, TypeCoin.Currency) +
                    "\nLow: " + candle.Low.ToPrecision(candle.Settings, TypeCoin.Currency) +
                    "\nHigh: " + candle.High.ToPrecision(candle.Settings, TypeCoin.Currency);
                strategies.TelegramBotClient.SendTextMessageAsync(chatId, message);
            }
        }

        // timer
        private void TimerOrder_Elapsed(MyTradeCompleted tradeCompleted)
        {
            // variables
            MyOrder order; decimal price;

            // check order
            Log.Information("-> Check order in progress");
            Log.Information("Order ID            : " + tradeCompleted.OrderId);
            Log.Information("Trade               : " + tradeCompleted.Action.ToString());
            Log.Information("Volume              : " + tradeCompleted.Amount.ToPrecision(tradeCompleted.Settings, TypeCoin.Asset));
            Log.Information("Trade               : " + tradeCompleted.Price.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency));
            try
            {
                if (!webAPI.ResolveWebAPI(tradeCompleted.Settings).GetOrder(tradeCompleted.OrderId, out order))
                    throw new Exception("Order unavailable.");
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + Environment.NewLine + ex.ToString());
                return;
            }

            if (order.State == TradeState.Completed)
            {
                // balance
                List<MyBalance> balances; MyBalance currency, asset;
                decimal balance = 0;
                try
                {
                    if (!webAPI.ResolveWebAPI(tradeCompleted.Settings).GetBalance(out balances))
                        throw new Exception("Balance unavailable.");
                    currency = balances.Find(s => s.Asset == tradeCompleted.Settings.Currency);
                    asset = balances.Find(s => s.Asset == tradeCompleted.Settings.Asset);
                    balance = currency.Amount + (asset.Amount * order.Price);
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message + Environment.NewLine + ex.ToString());
                }

                // reset timer
                ResetTimer();

                // save order
                BrokerDBContext db = new BrokerDBContext();
                order.Settings = order.Settings.GenerateMyWebAPISettings(db);
                db.MyOrders.Add(order);
                db.SaveChanges();

                // set values
                tradeCompleted.Amount = order.Volume;
                tradeCompleted.Balance = balance;
                tradeCompleted.Cost = order.Fee;
                tradeCompleted.Date = order.Completed.ToDateTime();
                tradeCompleted.EffectivePrice = order.Price;
                events.OnTradeCompleted(tradeCompleted);
                return;

            }
            else
            {

                // check error and possibility of a new order
                if (!isTradeInError)
                    if (retryTimes >= 1 && DateTime.Now <= tradeCompleted.Date.AddMinutes(5)) return;

                // delete order
                isTradeInError = false; retryTimes++;
                try
                {
                    if (!webAPI.ResolveWebAPI(tradeCompleted.Settings).PostCancelOrder(tradeCompleted.OrderId))
                        throw new Exception("Order wasn't cancelled.");
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message + Environment.NewLine + ex.ToString());
                    isTradeInError = true;
                    return;
                }

                // check retry limit
                if (retryTimes >= Extension.GetRetryLimit)
                {
                    MyTradeCancelled cancelled = new MyTradeCancelled
                    {
                        IdReference = tradeCompleted.Id,
                        Reason = "Retry limit reached up: " + retryTimes,
                        Settings = tradeCompleted.Settings
                    };
                    ResetTimer();
                    Log.Information(cancelled.Reason);
                    events.OnTradeCancelled(cancelled);
                    return;
                }

                // find new price
                try
                {
                    List<MyTicker> tickers;
                    if (webAPI.ResolveWebAPI(tradeCompleted.Settings).GetTicker(out tickers))
                        price = (tradeCompleted.Action == TradeAction.Long) ? tickers.Last().Bid : tickers.Last().Ask;
                    else
                        price = GetLastPriceTicker(tradeCompleted.Action);
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message + Environment.NewLine + ex.ToString());
                    isTradeInError = true;
                    return;
                }

                // check limit price
                if (tradeCompleted.LimitPrice.HasValue)
                    if ((tradeCompleted.Action == TradeAction.Short && price < tradeCompleted.LimitPrice) ||
                        (tradeCompleted.Action == TradeAction.Long && price > tradeCompleted.LimitPrice))
                    {
                        MyTradeCancelled cancelled = new MyTradeCancelled
                        {
                            IdReference = tradeCompleted.Id,
                            Reason = "Price limit reached up: " +
                                tradeCompleted.LimitPrice.Value.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency),
                            Settings = tradeCompleted.Settings
                        };
                        ResetTimer();
                        Log.Information(cancelled.Reason);
                        events.OnTradeCancelled(cancelled);
                        return;
                    }

                // reset timer
                ResetTimer(false);

                // make new order
                this.Events_myTradeRequest(tradeCompleted.Settings, tradeCompleted.Action, tradeCompleted.Percentage, price, tradeCompleted.LimitPrice);
            }
        }

        // functions
        private void ResetTimer(bool resetRetryTime = true)
        {
            timerOrder.Change(Timeout.Infinite, Timeout.Infinite);
            timerOrder.Dispose(); timerOrder = null;
            if (resetRetryTime) retryTimes = 1;
        }

        private decimal GetLastPriceTicker(TradeAction tradeAction)
        {
            BrokerDBContext db = new BrokerDBContext();
            MyTicker myTicker = db.MyTickers
                .OrderByDescending(s => s.Timestamp)
                .FirstOrDefault();
            if (myTicker != null)
                return (tradeAction == TradeAction.Long) ?
                    myTicker.Bid : myTicker.Ask;
            else
                throw new Exception("Ticker unavailable.");
        }
    }
}
