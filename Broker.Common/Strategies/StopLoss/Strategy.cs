using Broker.Common.Events;
using Broker.Common.Indicators;
using Broker.Common.Utility;
using Broker.Common.WebAPI;
using Broker.Common.WebAPI.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Bot;
using static Broker.Common.Strategies.Enumerator;
using static Broker.Common.Strategies.StopLoss.Values;

namespace Broker.Common.Strategies.StopLoss
{

    public class Strategy : IStrategy
    {

        // variables
        private readonly IEvents events;
        private TelegramBotClient telegramBot = null;
        private string telegramPassword = null, telegramUsernameTo = null;
        private bool telegramSendLow = false, telegramSendHigh = false;


        // properties
        private Values Current { get; set; }  = new Values();
        private Values Saved { get; set; } = new Values();
        TelegramBotClient IStrategy.TelegramBotClient { get => telegramBot; set => telegramBot = value; }
        string IStrategy.TelegramBotPassword { get => telegramPassword; set => telegramPassword = value; }
        string IStrategy.TelegramBotUsernameTo { get => telegramUsernameTo; set => telegramUsernameTo = value; }


        // init
        public Strategy(IEvents Events)
        {
            events = Events;
        }


        // events
        void IStrategy.Events_onWarmUpEnded()
        {
            Log.Information("-> Signaling WARMUP ended");
            this.Current.PreviousAction = ActionType.WarmUp;
        }
        void IStrategy.Events_onTickerUpdate(List<MyTicker> myTickers)
        {
            if (myTickers == null) return;
            if (myTickers.Count() == 0) return;

            decimal high = myTickers.Max(s => s.LastTrade);
            decimal low = myTickers.Min(s => s.LastTrade);
            MyWebAPISettings settings = myTickers.First().Settings;

            if (high > this.Current.LastHigh)
            {
                this.Current.LastHigh = high; decimal stopLoss = ComputeStopless();
                Log.Information("-> Signaling new parameter");
                Log.Information("High seen updated to : " + this.Current.LastHigh.ToPrecision(settings, TypeCoin.Currency));
                if (stopLoss > 0)
                    Log.Information("New stopLoss        : " + stopLoss.ToPrecision(settings, TypeCoin.Currency));

                // telegram
                telegramSendHigh = true;
            }
            if (low < this.Current.LastLow)
            {
                this.Current.LastLow = low;
                Log.Information("-> Signaling new parameter");
                Log.Information("Low seen updated to  : " + this.Current.LastLow.ToPrecision(settings, TypeCoin.Currency));

                // telegram
                telegramSendLow = true;
            }
        }
        void IStrategy.Events_onCandleUpdate(MyCandle myCandle)
        {
            // check market type
            CheckMarketType();
            
            // adjust values from price
            this.Current.BuyAt = Misc.GetParameterValue("buyAt", "stoploss").ToDecimal();
            this.Current.StopLoss = Misc.GetParameterValue("stopLoss", "stoploss").ToDecimal();
            this.Current.PreviousCandleClose = this.Current.CurrentCandleClose;
            this.Current.CurrentCandleClose = myCandle.Close;
            decimal stopLoss = ComputeStopless();

            // telegram
            if (telegramSendLow) 
            {
                this.onTradeCandle(CandleType.Low, this.Current.LastLow);
                telegramSendLow = false;
            }
            if (telegramSendHigh)
            {
                this.onTradeCandle(CandleType.High, this.Current.LastHigh, stopLoss);
                telegramSendHigh = false;
            }

            // check pause
            if (this.DetectPauseStatus()) return;

            // init
            if (this.Current.PreviousAction == ActionType.WarmUp)
            {
                // retrive old trade data
                string recoveredInfo = "";
                var action = ToActionType((string)Misc.CacheManager("PreviousAction", Misc.CacheType.Load));
                var market = ToMarketType((string)Misc.CacheManager("MarketState", Misc.CacheType.Load));
                var price = Misc.CacheManager("PreviousActionPrice", Misc.CacheType.Load);
                var data = Misc.CacheManager("LastOpDate", Misc.CacheType.Load);

                // set trade data
                if (DetectPauseStatus()) this.Current.PreviousAction = ActionType.Pause;
                else this.Current.PreviousAction = ToActionType(Misc.GetParameterValue("lastOperation", "stoploss"));
                decimal prevPrice = Misc.GetParameterValue("lastActionPrice", "stoploss").ToDecimal();
                this.Current.PreviousActionPrice = (this.Current.PreviousAction == ActionType.StopExit ? this.Current.LastHigh : this.Current.LastLow);
                if (prevPrice > 0) this.Current.PreviousActionPrice = prevPrice; this.Current.LastOpDate = DateTime.Now;
                if (Misc.MustRecoverData && action != ActionType.None && price != null && data != null) 
                {
                    this.Saved = this.Current.DeepClone();
                    this.Current.PreviousAction = action;
                    this.Current.MarketState = market;
                    this.Current.PreviousActionPrice = ((string)price).ToDecimal();
                    this.Current.LastOpDate = DateTime.Parse((string)data);
                    recoveredInfo = " (recovered data)";
                }
                stopLoss = ComputeStopless();

                // log summary
                Log.Information("-> INIT trade" + recoveredInfo);
                Log.Information("PreviousAction      : " + this.Current.PreviousAction);
                Log.Information("LastLow             : " + this.Current.LastLow.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                Log.Information("LastHigh            : " + this.Current.LastHigh.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                Log.Information("Last date           : " + this.Current.LastOpDate.ToShortDateTimeString());

                // log initial trade values
                decimal newThreshold = 0;
                if (this.Current.PreviousAction == ActionType.StopExit)
                {
                    newThreshold = (this.Current.PreviousActionPrice * this.Current.BuyAt).Round(myCandle.Settings.PrecisionCurrency);
                    Log.Information("New threshold       : " + newThreshold.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                }
                else
                {
                    newThreshold = stopLoss;
                    Log.Information("New stoploss        : " + stopLoss.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                }

                // telegram
                this.onTradeStart(this.Current.LastLow, this.Current.LastHigh, newThreshold);

            }
            // stoploss strategy
            else if (this.Current.PreviousAction == ActionType.Normal ||
                this.Current.PreviousAction == ActionType.StopLoss)
            {
                if (myCandle.Close < stopLoss)
                {

                    // log values
                    Log.Debug("-> Inside STOPLOSS strategy");
                    Log.Debug("CandleClose         : " + myCandle.Close.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                    Log.Debug("StopLoss parameter  : " + this.Current.StopLoss.ToStringRound(4));
                    Log.Debug("StopLoss            : " + stopLoss.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                    Log.Debug("PreviousAction      : " + this.Current.PreviousAction);

                    // first stoploss signal
                    if (this.Current.PreviousAction != ActionType.StopLoss)
                    {

                        // save old values
                        this.Saved = this.Current.DeepClone();

                        // new parameters
                        Log.Information("-> Signaling advice STOPLOSS");
                        Log.Information("CandleClose         : " + myCandle.Close.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                        Log.Information("StopLoss parameter  : " + this.Current.StopLoss.ToStringRound(4));
                        Log.Information("StopLoss            : " + stopLoss.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                        Log.Information("PreviousAction      : " + this.Current.PreviousAction);
                        this.Current.PreviousActionPrice = myCandle.Close;
                        this.Current.PreviousAction = ActionType.SospLoss;
                        events.MyTradeRequest(myCandle.Settings, TradeAction.Short,
                            int.Parse(Misc.GetParameterValue("stopLossPercentage", "stoploss")));

                        // exit 
                        return;
                        
                    }

                    // second or more stoploss signal
                    else if (myCandle.Close <= this.Current.PreviousActionPrice)
                    {

                        Log.Information("-> Signaling advice FREEFALL");
                        Log.Information("CandleClose         : " + myCandle.Close.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                        Log.Information("StopLoss parameter  : " + this.Current.StopLoss.ToStringRound(4));
                        Log.Information("StopLoss            : " + stopLoss.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                        Log.Information("PreviousActionPrice : " + this.Current.PreviousActionPrice.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                        Log.Information("PreviousAction      : " + this.Current.PreviousAction);
                        this.Current.PreviousActionPrice = myCandle.Close;

                        // exit 
                        return;
                    }

                }

                // stoploss exit on price go up, enable buy
                if (this.Current.PreviousAction == ActionType.StopLoss && 
                    this.Current.MarketState == MarketType.Bullish)
                {

                    // new parameters
                    Log.Information("-> Signaling advice EXIT STOPLOSS");
                    Log.Information("CandleClose         : " + myCandle.Close.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                    Log.Information("StopLoss parameter  : " + this.Current.StopLoss.ToStringRound(4));
                    Log.Information("StopLoss            : " + stopLoss.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                    Log.Information("PreviousActionPrice : " + this.Current.PreviousActionPrice.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                    this.Current.PreviousAction = ActionType.StopExit;
                    this.Current.PreviousActionPrice = myCandle.Close;
                    this.Current.LastHigh = myCandle.High;
                    this.Current.LastLow = myCandle.Low;

                    // telegram
                    this.onTradeStopCompleted(myCandle.Close, stopLoss);
                }
            }
            // stop exit trade
            else if (this.Current.PreviousAction == ActionType.StopExit)
            {

                // calculate the minimum price in order to buy some
                decimal threshold = (this.Current.PreviousActionPrice * 
                    this.Current.BuyAt).Round(myCandle.Settings.PrecisionCurrency);

                // log parameters
                Log.Debug("-> Expected LONG (buy)");
                Log.Debug("CandleClose         : " + myCandle.Close.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                Log.Debug("Curr date           : " + myCandle.Date.ToShortDateTimeString());
                Log.Debug("Threshold           : " + threshold.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                Log.Debug("PreviousCandleClose : " + this.Current.PreviousCandleClose);
                Log.Debug("MarketState         : " + this.Current.MarketState.ToString());

                // buy by price
                if 
                (
                    (myCandle.Close < threshold && 
                        myCandle.Close >= this.Current.PreviousCandleClose) ||
                    myCandle.Close > stopLoss
                )
                {

                    // save old values
                    this.Saved = this.Current.DeepClone();
                    
                    // new parameters
                    Log.Information("-> Signaling advice LONG (normal)");
                    this.Current.PreviousAction = ActionType.SospBuy;
                    this.Current.LastLow = myCandle.Low;
                    this.Current.LastHigh = myCandle.Close;
                    events.MyTradeRequest(myCandle.Settings, TradeAction.Long,
                        int.Parse(Misc.GetParameterValue("stopLossPercentage", "stoploss")));

                }

            }

        }
        void IStrategy.Events_onTradeCompleted(MyTradeCompleted tradeCompleted)
        {
            Log.Information("-> Signaling onTradeCompleted");
            decimal stopLoss = ComputeStopless();

            if (this.Current.PreviousAction == ActionType.SospBuy)
            {
                this.Current.PreviousAction = ActionType.Normal;
                this.Current.PreviousActionPrice = tradeCompleted.Price;
                this.Current.LastOpDate = tradeCompleted.Date;

                Log.Information("CandleClose         : " + tradeCompleted.Price.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency));
                Log.Information("New stopLoss        : " + stopLoss.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency));

                // telegram
                this.onTradeCompleted(tradeCompleted, stopLoss);
            }
            else if (this.Current.PreviousAction == ActionType.SospLoss)
            {
                this.Current.PreviousAction = ActionType.StopLoss;
                this.Current.PreviousActionPrice = tradeCompleted.Price;
                this.Current.LastOpDate = tradeCompleted.Date;

                Log.Information("CandleClose         : " + tradeCompleted.Price.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency));
                Log.Information("New stopLoss        : " + stopLoss.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency));

                // telegram
                this.onTradeStopCompleted(tradeCompleted.Price, stopLoss);
            }
        }
        void IStrategy.Events_onTradeAborted(MyTradeCancelled tradeAborted)
        {
            Log.Information("-> Signaling onTradeAborted");
            this.ResetTradeAborted(TradeStatus.Aborted, tradeAborted);
        }
        void IStrategy.Events_onTradeCancelled(MyTradeCancelled traceCancelled)
        {
            Log.Information("-> Signaling onTradeCancelled");
            this.ResetTradeAborted(TradeStatus.Cancelled, traceCancelled);
        }
        void IStrategy.Events_onTradeErrored(MyTradeCancelled tradeErrored)
        {
            Log.Information("-> Signaling onTradeErrored");
            this.ResetTradeAborted(TradeStatus.Errored, tradeErrored);
        }
        void IStrategy.Events_onTelegramMessage(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            if (e.Message.Text.ToLower().StartsWith("/status"))
                this.onEmitStatus(e.Message.Chat.Id);
            else if (e.Message.Text.ToLower().StartsWith("/pause"))
                this.onEditStatus(e.Message.Chat.Id, e.Message.Text, false);
            else if (e.Message.Text.ToLower().StartsWith("/resume"))
                this.onEditStatus(e.Message.Chat.Id, e.Message.Text, true);
            else
                telegramBot.SendTextMessageAsync(e.Message.Chat.Id, "Sorry! I don't understand!");
        }


        // telegram
        private void onEditStatus(long chatId, string text, bool status)
        {
            if (!text.Contains(telegramPassword))
                telegramBot.SendTextMessageAsync(chatId, "Password mismatch!");
            else
            {

                // abort by telegram
                Log.Information("-> Telegram advice change status: " + status.ToString());

                // change status
                if (!status) 
                    this.Current.PreviousAction = ActionType.Pause;
                else
                    this.Current.PreviousAction = ToActionType((string)Misc.CacheManager("PreviousAction", Misc.CacheType.Load));

                // telegram
                telegramBot.SendTextMessageAsync(chatId, "Changed status to " + (!status ? "Pause" : "Running") + ".");

            }
        }
        private void onEmitStatus(long chatId)
        {
            string message; decimal stopLoss = ComputeStopless();
            MyWebAPISettings settings = Misc.GenerateMyWebAPISettings();

            if (this.Current.PreviousAction == ActionType.None) 
            {
                message = "Trade Status: " +
                    "\nWarmUp in progress.";
                telegramBot.SendTextMessageAsync(chatId, message);
                return;
            }

            message = "Trade Status: " +
                "\nCurr. market: " + this.Current.MarketState.ToString() +
                "\nPrev. action: " + this.Current.PreviousAction.ToString() +
                "\nPrev. action price: " + this.Current.PreviousActionPrice.ToPrecision(settings, TypeCoin.Currency) +
                "\nLast low price: " + this.Current.LastLow.ToPrecision(settings, TypeCoin.Currency) +
                "\nLast high price: " + this.Current.LastHigh.ToPrecision(settings, TypeCoin.Currency) + 
                "\nStopLoss price: " + stopLoss.ToPrecision(settings, TypeCoin.Currency);
            telegramBot.SendTextMessageAsync(chatId, message);
        }
        private void onTradeCandle(CandleType type, decimal price, decimal? newPrice = null)
        {
            if (this.telegramBot == null || this.telegramUsernameTo == null) return;
            MyWebAPISettings settings = Misc.GenerateMyWebAPISettings();
            string message = "Limit changed. " +
                "\nDirection: " + (type == CandleType.High ? "High" : "Low") +
                "\nPrice: " + price.ToPrecision(settings, TypeCoin.Currency);
            if (newPrice.HasValue && newPrice.Value > 0)
                message += "\nNew stopLoss: " + newPrice.Value.ToPrecision(settings, TypeCoin.Currency);
            telegramBot.SendTextMessageAsync(this.telegramUsernameTo, message);
        }
        private void onTradeStart(decimal low, decimal high, decimal threshold)
        {
            if (this.telegramBot == null || this.telegramUsernameTo == null) return;
            MyWebAPISettings settings = Misc.GenerateMyWebAPISettings();
            string message = "Trade started. " +
                "\nLimit low: " + low.ToPrecision(settings, TypeCoin.Currency) +
                "\nLimit high: " + high.ToPrecision(settings, TypeCoin.Currency) +
                "\nStoploss: " + threshold.ToPrecision(settings, TypeCoin.Currency);
            telegramBot.SendTextMessageAsync(this.telegramUsernameTo, message);
        }
        private void onTradeCancelled(TradeStatus status, MyTradeCancelled tradeCancelled)
        {
            if (this.telegramBot == null || this.telegramUsernameTo == null) return;
            string message = "Trade " + status.ToString().ToLower() + "." +
                "\nID: " + tradeCancelled.Id.ToString().Substring(0, 6);
            if (tradeCancelled.IdReference != null)
                message += "\nIdRef: " + tradeCancelled.IdReference.ToString().Substring(0, 6);
            if (tradeCancelled.Reason != null)
                message += "\nReason: " + tradeCancelled.Reason;
            telegramBot.SendTextMessageAsync(this.telegramUsernameTo, message);
        }
        private void onTradeCompleted(MyTradeCompleted tradeCompleted, decimal newUpDown)
        {
            if (this.telegramBot == null || this.telegramUsernameTo == null) return;
            var message = "Trade completed. " +
	            "\nID: " + tradeCompleted.OrderId + 
                "\nAction: " + tradeCompleted.Action +
                "\nPrice: " + tradeCompleted.Price.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency) +
                "\nAmount: " + tradeCompleted.Amount.ToPrecision(tradeCompleted.Settings, TypeCoin.Asset) +
                "\nCost: " + tradeCompleted.Cost.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency) +
                "\nBalance: " + tradeCompleted.Balance.ToStringRound(2) +
                "\nEffective price: " + tradeCompleted.EffectivePrice.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency) +
	            "\nNew stopLoss: " + newUpDown.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency);
            telegramBot.SendTextMessageAsync(this.telegramUsernameTo, message);
        }
        private void onTradeStopCompleted(decimal price, decimal newStopless)
        {
            if (this.telegramBot == null || this.telegramUsernameTo == null) return;
            MyWebAPISettings settings = Misc.GenerateMyWebAPISettings();
            string message = "StopLoss exited. " +
                "\nPrice: " + price.ToPrecision(settings, TypeCoin.Currency) +
                "\nNew stopLoss: " + newStopless.ToPrecision(settings, TypeCoin.Currency) +
                "\nLimit low: " + this.Current.LastLow.ToPrecision(settings, TypeCoin.Currency) +
                "\nLimit high: " + this.Current.LastHigh.ToPrecision(settings, TypeCoin.Currency);
            telegramBot.SendTextMessageAsync(this.telegramUsernameTo, message);
        }


        // function
        private bool DetectPauseStatus()
        {
            if (this.Current.PreviousAction == ActionType.WarmUp ||
                this.Current.PreviousAction == ActionType.None)
                return false;

            string isInPause = (string)Misc.CacheManager("BotInPause", Misc.CacheType.Load);
            if (isInPause != null) return bool.Parse(isInPause);
            return false;
        }
        private void ResetTradeAborted(TradeStatus status, MyTradeCancelled tradeAborted)
        {
            // telegram
            if (status == TradeStatus.Cancelled)
            {
                Log.Information("-> Signaling advice TRADE CANCELLED");
                Log.Information("Reason : Order not filled.");
            }
            else
            {
                Log.Information("-> Signaling advice TRADE ABORTED");
                Log.Information("Reason : " + tradeAborted.Reason);
            }
            this.onTradeCancelled(status, tradeAborted);

            // stoploss exception
            if (this.Current.PreviousAction == ActionType.SospLoss)
            {
                Log.Information("Method : Re-initiate stopless strategy.");
                events.MyTradeRequest(tradeAborted.Settings, TradeAction.Short,
                    int.Parse(Misc.GetParameterValue("stopLossPercentage", "stoploss")));
                return;
            }

            // restore old values
            this.Current = this.Saved.DeepClone();
        }
        private void CheckMarketType()
        {
            
            if (this.Current.PreviousAction == ActionType.WarmUp ||
                this.Current.PreviousAction == ActionType.None)
                return;

            // macd check cross line
            decimal macdValue, signalValue, hist;
            BrokerDBContext db = new BrokerDBContext();
            MyMACD oldMacd = db.MyMACDs.OrderByDescending(s => s.Id).Skip(1).FirstOrDefault();
            MyStrategy.MACDAverage.Value(out macdValue, out signalValue, out hist);
            if (oldMacd != null && MyStrategy.MACDAverage.isPrimed())
            {
                Line macdLine = new Line() { x1 = 0, x2 = 1, y1 = oldMacd.MACD, y2 = macdValue };
                Line signalLine = new Line() { x1 = 0, x2 = 1, y1 = oldMacd.SignalValue, y2 = signalValue };
                Point c = LineIntersection.FindIntersection(macdLine, signalLine);
                if (!c.Equals(default(Point))) 
                {
                    Log.Information("-> Signaling crossover line");
                    Log.Debug("Point Y             : " + c.y.ToStringRound(2));
                    Log.Debug("MACD Y              : " + macdLine.y2.ToStringRound(2));
                    if (macdLine.y2 > c.y)
                    {
                        this.Current.MarketState = MarketType.Bullish;
                        Log.Information("Direction           : Up");
                    }
                    else
                    {
                        this.Current.MarketState = MarketType.Bearish;
                        Log.Information("Direction           : Down");
                    }
                }
            }

        }
        private decimal ComputeStopless()
        {
            decimal high = this.Current.PreviousActionPrice > this.Current.LastHigh ?
                this.Current.PreviousActionPrice : this.Current.LastHigh;
            decimal stopLossPerc = high * this.Current.StopLoss;
            decimal stopLossMin = high - Misc.GetParameterValue("stopLossMin", "stoploss").ToDecimal();
            return stopLossPerc < stopLossMin ? stopLossPerc : stopLossMin;
        }

    }

}
