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
using static Broker.Common.Strategies.SellBuy.Values;

namespace Broker.Common.Strategies.SellBuy
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
            this.Current.BuyAt = Misc.GetParameterValue("buyAt", "sellbuy").ToDecimal();
            this.Current.SellAt = Misc.GetParameterValue("sellAt", "sellbuy").ToDecimal();
            this.Current.BuyAtUp = Misc.GetParameterValue("buyAtUp", "sellbuy").ToDecimal();
            this.Current.StopLoss = Misc.GetParameterValue("stopLoss", "sellbuy").ToDecimal();
            this.Current.LimitShortPrice = 
                (
                    this.Current.PreviousActionPrice * 
                    Misc.GetParameterValue("limitShortPrice", "sellbuy").ToDecimal()
                )
                .Round(myCandle.Settings.PrecisionCurrency);
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

            // detect stopless
            if (this.DetectStopLoss(myCandle, stopLoss)) return;

            // check pause
            if (this.DetectPauseStatus()) return;

            // reset per timeout
            ResetTrade(myCandle);

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
                else this.Current.PreviousAction = ToActionType(Misc.GetParameterValue("lastOperation", "sellbuy"));
                decimal prevPrice = Misc.GetParameterValue("lastActionPrice", "sellbuy").ToDecimal();
                this.Current.PreviousActionPrice = (this.Current.PreviousAction == ActionType.Buy ? this.Current.LastLow : this.Current.LastHigh);
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
                Log.Information("PreviousActionPrice : " + this.Current.PreviousActionPrice.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                Log.Information("LastLow             : " + this.Current.LastLow.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                Log.Information("LastHigh            : " + this.Current.LastHigh.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                Log.Information("Last date           : " + this.Current.LastOpDate.ToShortDateTimeString());

                // log initial trade values
                decimal newThreshold;
                if (this.Current.PreviousAction == ActionType.Sell)
                {
                    newThreshold = (this.Current.PreviousActionPrice * this.Current.BuyAt).Round(myCandle.Settings.PrecisionCurrency);
                    decimal newSellAtUp = (this.Current.PreviousActionPrice * this.Current.BuyAtUp).Round(myCandle.Settings.PrecisionCurrency);
                    Log.Information("New threshold       : " + newThreshold.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                    Log.Information("New sellAtUp        : " + newSellAtUp.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                }
                else
                {
                    newThreshold = (this.Current.PreviousActionPrice * this.Current.SellAt).Round(myCandle.Settings.PrecisionCurrency);
                    Log.Information("New threshold       : " + newThreshold.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                }

                // telegram
                this.onTradeStart(this.Current.LastLow, this.Current.LastHigh, newThreshold);

            }
            // reset trade
            else if (this.Current.PreviousAction == ActionType.Reset)
            {

                // set previous trade
                this.Current.PreviousActionPrice = (this.Current.LastHigh / this.Current.BuyAt).Round(myCandle.Settings.PrecisionCurrency);

                // log summary
                Log.Information("-> RESET trade");
                Log.Information("CandleClose         : " + myCandle.Close.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                Log.Information("PreviousActionPrice : " + this.Current.PreviousActionPrice.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                Log.Information("LastLow             : " + this.Current.LastLow.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                Log.Information("LastHigh            : " + this.Current.LastHigh.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                Log.Information("Last date           : " + this.Current.LastOpDate.ToShortDateTimeString());
                Log.Information("Curr date           : " + myCandle.Date.ToShortDateTimeString());

                // log initial trade values
                decimal newThreshold = (this.Current.PreviousActionPrice * this.Current.BuyAt).Round(myCandle.Settings.PrecisionCurrency);
                decimal newSellAtUp = (this.Current.PreviousActionPrice * this.Current.BuyAtUp).Round(myCandle.Settings.PrecisionCurrency);
                Log.Information("New threshold       : " + newThreshold.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                Log.Information("New sellAtUp        : " + newSellAtUp.ToPrecision(myCandle.Settings, TypeCoin.Currency));

                // set next trade
                this.Current.LastOpDate = myCandle.Date;
                this.Current.PreviousAction = ActionType.None;
                events.MyWarmUpRequest();

            }
            // buy trade
            else if (this.Current.PreviousAction == ActionType.Buy)
            {

                // calculate the minimum price in order to sell
                decimal threshold = (this.Current.PreviousActionPrice * this.Current.SellAt).Round(myCandle.Settings.PrecisionCurrency);

                // log parameters
                Log.Debug("-> Expected SHORT (sell)");
                Log.Debug("CandleClose         : " + myCandle.Close.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                Log.Debug("Threshold           : " + threshold.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                Log.Debug("PreviousCandleClose : " + this.Current.PreviousCandleClose.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                Log.Debug("MarketState         : " + this.Current.MarketState.ToString());

                // sell by price
                if 
                (
                    myCandle.Close > threshold && 
                    myCandle.Close <= this.Current.PreviousCandleClose
                )
                {

                    // save old values
                    this.Saved = this.Current.DeepClone();

                    // new parameters
                    Log.Information("-> Signaling advice SHORT");
                    this.Current.PreviousAction = ActionType.SospSell;
                    this.Current.LastLow = myCandle.Low;
                    this.Current.LastHigh = myCandle.Close;
                    events.MyTradeRequest(myCandle.Settings, TradeAction.Short, 
                        int.Parse(Misc.GetParameterValue("shortPercentage", "sellbuy")), 
                        null, this.Current.LimitShortPrice);

                }

            }
            // sell trade
            else if (this.Current.PreviousAction == ActionType.Sell)
            {

                // calculate the minimum price in order to buy some
                decimal threshold = (this.Current.PreviousActionPrice * this.Current.BuyAt).Round(myCandle.Settings.PrecisionCurrency);

                // calculate the price at which we should buy again if market goes up
                decimal sellAtUp = (this.Current.PreviousActionPrice * this.Current.BuyAtUp).Round(myCandle.Settings.PrecisionCurrency);

                // log parameters
                Log.Debug("-> Expected LONG (buy)");
                Log.Debug("CandleClose         : " + myCandle.Close.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                Log.Debug("Curr date           : " + myCandle.Date.ToShortDateTimeString());
                Log.Debug("Threshold           : " + threshold.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                Log.Debug("SellAtUp            : " + sellAtUp.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                Log.Debug("PreviousCandleClose : " + this.Current.PreviousCandleClose);
                Log.Debug("MarketState         : " + this.Current.MarketState.ToString());

                // buy by price
                if 
                (
                    myCandle.Close < threshold && 
                    myCandle.Close >= this.Current.PreviousCandleClose
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
                        int.Parse(Misc.GetParameterValue("longPercentage", "sellbuy")));

                }
                // buy by sell up
                else if 
                (
                    myCandle.Close > sellAtUp && 
                    myCandle.Close >= this.Current.PreviousCandleClose && 
                    this.Current.MarketState == MarketType.Bullish
                )
                {

                    // save old values
                    this.Saved = this.Current.DeepClone();

                    // new parameters
                    Log.Information("-> Signaling advice LONG (sellup)");
                    this.Current.PreviousAction = ActionType.SospBuy;
                    this.Current.LastLow = myCandle.Low;
                    this.Current.LastHigh = myCandle.Close;
                    events.MyTradeRequest(myCandle.Settings, TradeAction.Long,
                        int.Parse(Misc.GetParameterValue("longPercentage", "sellbuy")));

                }

            }

        }
        void IStrategy.Events_onTradeCompleted(MyTradeCompleted tradeCompleted)
        {
            Log.Information("-> Signaling onTradeCompleted");

            if (this.Current.PreviousAction == ActionType.SospSell)
            {
                this.Current.PreviousAction = ActionType.Sell;
                this.Current.PreviousActionPrice = tradeCompleted.Price;
                this.Current.LastOpDate = tradeCompleted.Date;

                // log new value
                decimal newThreshold = (this.Current.PreviousActionPrice * this.Current.BuyAt).Round(tradeCompleted.Settings.PrecisionCurrency);
                decimal newSellAtUp = (this.Current.PreviousActionPrice * this.Current.BuyAtUp).Round(tradeCompleted.Settings.PrecisionCurrency);

                Log.Information("CandleClose         : " + tradeCompleted.Price.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency));
                Log.Information("New threshold       : " + newThreshold.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency));
                Log.Information("New sellAtUp        : " + newSellAtUp.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency));

                // telegram
                this.onTradeCompleted(tradeCompleted, newThreshold, newSellAtUp);
            }
            else if (this.Current.PreviousAction == ActionType.SospBuy)
            {
                this.Current.PreviousAction = ActionType.Buy;
                this.Current.PreviousActionPrice = tradeCompleted.Price;
                this.Current.LastOpDate = tradeCompleted.Date;

                // log new values
                decimal newThreshold = (this.Current.PreviousActionPrice * this.Current.SellAt).Round(tradeCompleted.Settings.PrecisionCurrency);
                decimal stopLoss = ComputeStopless();

                Log.Information("CandleClose         : " + tradeCompleted.Price.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency));
                Log.Information("New threshold       : " + newThreshold.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency));
                Log.Information("New stopLoss        : " + stopLoss.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency));

                // telegram
                this.onTradeCompleted(tradeCompleted, newThreshold, stopLoss);
            }
            else if (this.Current.PreviousAction == ActionType.SospLoss)
            {
                this.Current.PreviousAction = ActionType.StopLoss;
                this.Current.PreviousActionPrice = tradeCompleted.Price;
                this.Current.LastOpDate = tradeCompleted.Date;

                // log new values
                decimal newThreshold = (this.Current.PreviousActionPrice * this.Current.SellAt).Round(tradeCompleted.Settings.PrecisionCurrency);
                decimal stopLoss = ComputeStopless();

                Log.Information("CandleClose         : " + tradeCompleted.Price.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency));
                Log.Information("New threshold       : " + newThreshold.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency));
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
            else if (e.Message.Text.ToLower().StartsWith("/short"))
                this.onEmitShort(e.Message.Chat.Id, e.Message.Text);
            else if (e.Message.Text.ToLower().StartsWith("/long"))
                this.onEmitLong(e.Message.Chat.Id, e.Message.Text);
            else if (e.Message.Text.ToLower().StartsWith("/reset"))
                this.onEmitReset(e.Message.Chat.Id, e.Message.Text);
            else if (e.Message.Text.ToLower().StartsWith("/abort"))
                this.onEmitAbort(e.Message.Chat.Id, e.Message.Text);
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
        private void onEmitAbort(long chatId, string text)
        {
            if (!text.Contains(telegramPassword))
                telegramBot.SendTextMessageAsync(chatId, "Password mismatch!");
            else
            {

                // abort by telegram
                Log.Information("-> Telegram advice ABORT");

                // restore old values
                this.Current = this.Saved.DeepClone();

                // telegram
                telegramBot.SendTextMessageAsync(chatId, "Trade abort done.");

            }
        }
        private void onEmitReset(long chatId, string text)
        {
            if (!text.Contains(telegramPassword))
                telegramBot.SendTextMessageAsync(chatId, "Password mismatch!");
            else
            {

                // reset by telegram
                MyWebAPISettings settings = Misc.GenerateMyWebAPISettings();
                Log.Information("-> Telegram advice RESET");
                Log.Information("CandleClose         : " + this.Current.CurrentCandleClose.ToPrecision(settings, TypeCoin.Currency));
                Log.Information("Last date           : " + this.Current.LastOpDate.ToShortDateTimeString());
                Log.Information("LastLow             : " + this.Current.LastLow.ToPrecision(settings, TypeCoin.Currency));
                Log.Information("LastHigh            : " + this.Current.LastHigh.ToPrecision(settings, TypeCoin.Currency));
                this.Current = new Values
                {
                    PreviousAction = ActionType.Reset
                };

                // telegram
                this.onTradeReset(this.Current.LastOpDate, DateTime.Now);

            }
        }
        private void onEmitLong(long chatId, string text)
        {
            if (!text.Contains(telegramPassword))
                telegramBot.SendTextMessageAsync(chatId, "Password mismatch!");
            else
            {
                // save old values
                MyWebAPISettings settings = Misc.GenerateMyWebAPISettings();
                this.Saved = this.Current.DeepClone();

                // buy by telegram
                Log.Information("-> Telegram advice LONG");
                this.Current.PreviousAction = ActionType.SospBuy;
                this.Current.LastLow = this.Current.CurrentCandleClose;
                this.Current.LastHigh = this.Current.CurrentCandleClose;
                events.MyTradeRequest(settings, TradeAction.Long,
                    int.Parse(Misc.GetParameterValue("longPercentage", "sellbuy")));

                // telegram
                telegramBot.SendTextMessageAsync(chatId, "Trade LONG initiated.");
            }
        }
        private void onEmitShort(long chatId, string text)
        {
            if (!text.Contains(telegramPassword))
                telegramBot.SendTextMessageAsync(chatId, "Password mismatch!");
            else
            {
                // save old values
                MyWebAPISettings settings = Misc.GenerateMyWebAPISettings();
                this.Saved = this.Current.DeepClone();

                // sell by telegram
                Log.Information("-> Telegram advice SHORT");
                this.Current.PreviousAction = ActionType.SospSell;
                this.Current.LastLow = this.Current.CurrentCandleClose;
                this.Current.LastHigh = this.Current.CurrentCandleClose;
                events.MyTradeRequest(settings, TradeAction.Short, 
                    int.Parse(Misc.GetParameterValue("shortPercentage", "sellbuy")));

                // telegram
                telegramBot.SendTextMessageAsync(chatId, "Trade SHORT initiated.");
            }
        }
        private void onEmitStatus(long chatId)
        {
            string message;
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
                "\nLast high price: " + this.Current.LastHigh.ToPrecision(settings, TypeCoin.Currency);

            // log initial trade values
            decimal newThreshold; decimal stopLoss = ComputeStopless();
            if (this.Current.PreviousAction == ActionType.Sell)
            {
                newThreshold = (this.Current.PreviousActionPrice * this.Current.BuyAt).Round(settings.PrecisionCurrency);
                decimal newSellAtUp = (this.Current.PreviousActionPrice * this.Current.BuyAtUp).Round(settings.PrecisionCurrency);
                message += "\nBuyAt price: " + newThreshold.ToPrecision(settings, TypeCoin.Currency) +
                "\nSellAtUp price: " + newSellAtUp.ToPrecision(settings, TypeCoin.Currency);
            }
            else if (this.Current.PreviousAction == ActionType.Buy)
            {
                newThreshold = (this.Current.PreviousActionPrice * this.Current.SellAt).Round(settings.PrecisionCurrency);
                message += "\nSellAt price: " + newThreshold.ToPrecision(settings, TypeCoin.Currency);
            }
            message += "\nStopLoss price: " + stopLoss.ToPrecision(settings, TypeCoin.Currency);
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
                "\nNew limit: " + threshold.ToPrecision(settings, TypeCoin.Currency);
            telegramBot.SendTextMessageAsync(this.telegramUsernameTo, message);
        }
        private void onTradeReset(DateTime lastOpDate, DateTime date)
        {
            if (this.telegramBot == null || this.telegramUsernameTo == null) return;
            string message = "Trade reset. " +
                "\nFrom date: " + lastOpDate.ToShortDateTimeString() +
                "\nTo date: " + date.ToShortDateTimeString();
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
        private void onTradeCompleted(MyTradeCompleted tradeCompleted, decimal newPrice, decimal newUpDown)
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
	            "\nNew price: " + newPrice.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency);
	        if (tradeCompleted.Action == TradeAction.Long)
		        message += "\nNew stopLoss: " + newUpDown.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency);
	        else if (tradeCompleted.Action == TradeAction.Short)
		        message += "\nNew sellUp: " + newUpDown.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency);
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
        private void ResetTrade(MyCandle myCandle)
        {
            if (this.Current.PreviousAction == ActionType.Sell)
            {
                decimal days = (decimal)((myCandle.Date - this.Current.LastOpDate).TotalDays);
                if (days >= int.Parse(Misc.GetParameterValue("timeoutDays", "sellbuy")))
                {
                    Log.Information("-> Signaling advice RESET");
                    Log.Information("CandleClose         : " + myCandle.Close.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                    Log.Information("Last date           : " + this.Current.LastOpDate.ToShortDateTimeString());
                    Log.Information("Day Passed          : " + days.ToStringRound(2));
                    Log.Information("LastLow             : " + this.Current.LastLow.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                    Log.Information("LastHigh            : " + this.Current.LastHigh.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                    this.Current = new Values();
                    this.Current.PreviousAction = ActionType.Reset;

                    // telegram
                    this.onTradeReset(this.Current.LastOpDate, myCandle.Date);
                }
            }
        }
        private bool DetectStopLoss(MyCandle myCandle, decimal stopLoss)
        {
            if (this.Current.PreviousAction == ActionType.None ||
                this.Current.PreviousAction == ActionType.WarmUp) return false;

            // stoploss strategy
            if (myCandle.Close < stopLoss)
            {

                // log values
                Log.Debug("-> Inside STOPLOSS strategy");
                Log.Debug("CandleClose         : " + myCandle.Close.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                Log.Debug("StopLoss parameter  : " + this.Current.StopLoss.ToStringRound(4));
                Log.Debug("StopLoss            : " + stopLoss.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                Log.Debug("PreviousActionPrice : " + this.Current.PreviousActionPrice.ToPrecision(myCandle.Settings, TypeCoin.Currency));
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
                    Log.Information("PreviousActionPrice : " + this.Current.PreviousActionPrice.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                    Log.Information("PreviousAction      : " + this.Current.PreviousAction);
                    this.Current.PreviousActionPrice = myCandle.Close;
                    this.Current.PreviousAction = ActionType.SospLoss;
                    events.MyTradeRequest(myCandle.Settings, TradeAction.Short,
                        int.Parse(Misc.GetParameterValue("stopLossPercentage", "sellbuy")));

                    return true;
                    
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
                    return true;
                }

            }

            // stoploss exit on price go up, enable buy
            if (this.Current.PreviousAction == ActionType.StopLoss &&
                (myCandle.Close >= stopLoss ||
                    this.Current.MarketState == MarketType.Bullish))
            {

                // new parameters
                Log.Information("-> Signaling advice EXIT STOPLOSS");
                Log.Information("CandleClose         : " + myCandle.Close.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                Log.Information("StopLoss parameter  : " + this.Current.StopLoss.ToStringRound(4));
                Log.Information("StopLoss            : " + stopLoss.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                Log.Information("PreviousActionPrice : " + this.Current.PreviousActionPrice.ToPrecision(myCandle.Settings, TypeCoin.Currency));
                this.Current.PreviousAction = ActionType.Sell;
                this.Current.PreviousActionPrice = myCandle.Close;
                this.Current.LastHigh = myCandle.High;
                this.Current.LastLow = myCandle.Low;

                // telegram
                this.onTradeStopCompleted(myCandle.Close, stopLoss);
            }

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
                    int.Parse(Misc.GetParameterValue("stopLossPercentage", "sellbuy")));
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
