using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Broker.Common.Events;
using Broker.Common.Indicators;
using Broker.Common.Utility;
using Broker.Common.WebAPI;
using Broker.Common.WebAPI.Models;
using Serilog.Events;
using Telegram.Bot;
using static Broker.Common.Strategies.Enumerator;
using static Broker.Common.Strategies.SellBuyPercMacdNoSellupNoStopLoss.Values;

namespace Broker.Common.Strategies.SellBuyPercMacdNoSellupNoStopLoss
{

    public class Strategy : IStrategy
    {
        private const string NameStrategy = "SellBuyPercMacdNoSellupNoStopLoss";

        // variables
        private readonly IEvents events;
        private TelegramBotClient telegramBot = null;
        private string telegramPassword = null, telegramUsernameTo = null;
        private bool telegramSendLow = false, telegramSendHigh = false;


        // properties
        private Values Current { get; set; } = new Values();
        private Values Saved { get; set; } = new Values();
        TelegramBotClient IStrategy.TelegramBotClient { get => telegramBot; set => telegramBot = value; }
        string IStrategy.TelegramBotPassword { get => telegramPassword; set => telegramPassword = value; }
        string IStrategy.TelegramBotUsernameTo { get => telegramUsernameTo; set => telegramUsernameTo = value; }

        private decimal LimitLowerSell { get; set; }
        private decimal LimitUpperBuy { get; set; }

        public Strategy(IEvents Events)
        {
            events = Events;
            telegramSendLow = false;
            telegramSendHigh = false;
        }

        void IStrategy.Events_onWarmUpEnded()
        {
            LogStrategy.AppendLog("-> Signaling WARMUP ended");
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
                this.Current.LastHigh = high;
                if (telegramSendHigh)
                {
                    LogStrategy.AppendLog("-> Signaling new parameter", LogEventLevel.Information, LogStrategy.Destination.File);
                    LogStrategy.AppendLog("High seen updated to: " + this.Current.LastHigh.ToPrecision(settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.File);
                }

                // telegram
                telegramSendHigh = false;
            }
            if (low < this.Current.LastLow)
            {
                this.Current.LastLow = low;
                if (telegramSendLow)
                {
                    LogStrategy.AppendLog("-> Signaling new parameter", LogEventLevel.Information, LogStrategy.Destination.File);
                    LogStrategy.AppendLog("Low seen updated to: " + this.Current.LastLow.ToPrecision(settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.File);
                }

                // telegram
                telegramSendLow = false;
            }
        }

        void IStrategy.Events_onCandleUpdate(MyCandle myCandle)
        {
            //save the previous market type
            this.Current.PreviousMarketState = this.Current.MarketState;

            //check market type
            CheckMarketType();

            //double ResultVolumeInLastCandle = 0;
            //double ResultVolumePriceInLastCandle = 0;
            //MarketType marketTypeTradesInLastCandle = MarketType.None;
            //CheckLastTradesType(myCandle.Settings, out ResultVolumeInLastCandle, out ResultVolumePriceInLastCandle, out marketTypeTradesInLastCandle);

            //retrieve params for buy and sell
            this.Current.BuyAt = Misc.GetParameterValue("buyAt", NameStrategy).ToDecimal();
            this.Current.SellAt = Misc.GetParameterValue("sellAt", NameStrategy).ToDecimal();
            this.Current.PreviousCandleClose = this.Current.CurrentCandleClose;
            this.Current.CurrentCandleClose = myCandle.Close;

            //check bot in pause
            if (this.DetectPauseStatus())
            {
                LogStrategy.AppendLog("Bot in pause", LogEventLevel.Information, LogStrategy.Destination.All);
                return;
            }

            //check reset for timeout
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
                if (DetectPauseStatus())
                    this.Current.PreviousAction = ActionType.Pause;
                else
                    this.Current.PreviousAction = ToActionType(Misc.GetParameterValue("lastOperation", NameStrategy));
                decimal prevPrice = Misc.GetParameterValue("lastActionPrice", NameStrategy).ToDecimal();
                this.Current.PreviousActionPrice = (this.Current.PreviousAction == ActionType.Buy ? this.Current.LastLow : this.Current.LastHigh);
                if (prevPrice > 0)
                    this.Current.PreviousActionPrice = prevPrice;
                this.Current.LastOpDate = DateTime.Now;
                if (Misc.MustRecoverData && action != ActionType.None && price != null && data != null)
                {
                    this.Saved = this.Current.DeepClone();
                    this.Current.PreviousAction = action;
                    this.Current.MarketState = market;
                    this.Current.PreviousMarketState = market;
                    this.Current.PreviousActionPrice = ((string)price).ToDecimal();
                    this.Current.LastOpDate = DateTime.Parse((string)data);
                    recoveredInfo = " (recovered data)";
                }

                // log summary
                LogStrategy.AppendLog("-> INIT trade" + recoveredInfo, LogEventLevel.Information, LogStrategy.Destination.All);
                LogStrategy.AppendLog("PreviousAction: " + this.Current.PreviousAction, LogEventLevel.Information, LogStrategy.Destination.All);
                LogStrategy.AppendLog("PreviousActionPrice: " + this.Current.PreviousActionPrice.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.All);
                LogStrategy.AppendLog("LastLow: " + this.Current.LastLow.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.All);
                LogStrategy.AppendLog("LastHigh: " + this.Current.LastHigh.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.All);
                LogStrategy.AppendLog("Last date: " + this.Current.LastOpDate.ToShortDateTimeString(), LogEventLevel.Information, LogStrategy.Destination.All);

                // log initial trade values
                decimal newThreshold;
                if (this.Current.PreviousAction == ActionType.Sell)
                {
                    newThreshold = (this.Current.PreviousActionPrice * this.Current.BuyAt).Round(myCandle.Settings.PrecisionCurrency);
                    LogStrategy.AppendLog("New BuyAt: " + newThreshold.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.All);
                }
                else
                {
                    newThreshold = (this.Current.PreviousActionPrice * this.Current.SellAt).Round(myCandle.Settings.PrecisionCurrency);
                    LogStrategy.AppendLog("New SellAt: " + newThreshold.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.All);
                }

                // telegram
                this.onTradeStart(this.Current.LastLow, this.Current.LastHigh, newThreshold);
            }
            // PreviousAction: Reset
            else if (this.Current.PreviousAction == ActionType.Reset)
            {
                LogStrategy.AppendLog("-> RESET trade", LogEventLevel.Information, LogStrategy.Destination.All);
                LogStrategy.AppendLog("CandleClose: " + myCandle.Close.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.All);
                LogStrategy.AppendLog("PreviousActionPrice: " + this.Current.PreviousActionPrice.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.All);
                decimal averageCloseCandle = 0;
                using (BrokerDBContext db = new BrokerDBContext())
                {
                    averageCloseCandle = db.MyCandles.OrderByDescending(s => s.Date).Take(50).ToList().Average(s => s.Close);
                }
                this.Current.PreviousActionPrice = averageCloseCandle.Round(myCandle.Settings.PrecisionCurrency);
                this.Current.LastOpDate = myCandle.Date;
                this.Current.PreviousAction = ActionType.Sell;
                LogStrategy.AppendLog("->new price: " + averageCloseCandle, LogEventLevel.Information, LogStrategy.Destination.All);
            }
            // PreviousAction: buy trade 
            else if (this.Current.PreviousAction == ActionType.Buy)
            {
                //ActualAction: sell trade
                LimitLowerSell = this.Current.PreviousActionPrice * this.Current.SellAt;
                // log parameters
                LogStrategy.AppendLog("-> Expected SHORT (sell)", LogEventLevel.Debug, LogStrategy.Destination.All);
                LogStrategy.AppendLog("CandleClose: " + myCandle.Close.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Debug, LogStrategy.Destination.All);
                LogStrategy.AppendLog("PreviousMarketState: " + this.Current.PreviousMarketState, LogEventLevel.Debug, LogStrategy.Destination.All);
                LogStrategy.AppendLog("PreviousBuyPrice: " + this.Current.PreviousActionPrice.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Debug, LogStrategy.Destination.All);
                LogStrategy.AppendLog("LimitLowerSell: " + LimitLowerSell.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Debug, LogStrategy.Destination.All);
                LogStrategy.AppendLog("MarketState: " + this.Current.MarketState.ToString(), LogEventLevel.Debug, LogStrategy.Destination.All);
                //LogStrategy.AppendLog("ResultVolumeInLastCandle: " + ResultVolumeInLastCandle, LogEventLevel.Debug, LogStrategy.Destination.All);
                //LogStrategy.AppendLog("ResultVolumePriceInLastCandle: " + ResultVolumePriceInLastCandle, LogEventLevel.Debug, LogStrategy.Destination.All);
                //LogStrategy.AppendLog("MarketTypeTradesInLastCandle: " + marketTypeTradesInLastCandle, LogEventLevel.Debug, LogStrategy.Destination.All);

                if (this.Current.MarketState == MarketType.Bearish && myCandle.Close >= LimitLowerSell)
                {
                    // save old values
                    this.Saved = this.Current.DeepClone();

                    // new parameters
                    LogStrategy.AppendLog("-> Signaling advice SHORT", LogEventLevel.Debug, LogStrategy.Destination.All);
                    this.Current.PreviousAction = ActionType.SospSell;
                    this.Current.LastLow = myCandle.Low;
                    this.Current.LastHigh = myCandle.Close;
                    events.MyTradeRequest(settings: myCandle.Settings, tradeType: TradeAction.Short,
                          percentage: int.Parse(Misc.GetParameterValue("shortPercentage", NameStrategy)),
                          price: null, priceLimit: null);
                }
                else
                    LogStrategy.AppendLog("-> Signaling advice NOT SHORT (not sell)", LogEventLevel.Debug, LogStrategy.Destination.All);

            }
            // PreviousAction: sell trade 
            else if (this.Current.PreviousAction == ActionType.Sell)
            {
                //ActualAction: buy trade
                LimitUpperBuy = this.Current.PreviousActionPrice * this.Current.BuyAt;
                // log parameters
                LogStrategy.AppendLog("-> Expected LONG (buy)", LogEventLevel.Debug, LogStrategy.Destination.All);
                LogStrategy.AppendLog("CandleClose: " + myCandle.Close.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Debug, LogStrategy.Destination.All);
                LogStrategy.AppendLog("PreviousMarketState: " + this.Current.PreviousMarketState, LogEventLevel.Debug, LogStrategy.Destination.All);
                LogStrategy.AppendLog("PreviousBuyPrice: " + this.Current.PreviousActionPrice.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Debug, LogStrategy.Destination.All);
                LogStrategy.AppendLog("LimitUpperBuy: " + LimitUpperBuy.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Debug, LogStrategy.Destination.All);
                LogStrategy.AppendLog("MarketState: " + this.Current.MarketState.ToString(), LogEventLevel.Debug, LogStrategy.Destination.All);
                //LogStrategy.AppendLog("ResultVolumeInLastCandle: " + ResultVolumeInLastCandle, LogEventLevel.Debug, LogStrategy.Destination.All);
                //LogStrategy.AppendLog("ResultVolumePriceInLastCandle: " + ResultVolumePriceInLastCandle, LogEventLevel.Debug, LogStrategy.Destination.All);
                //LogStrategy.AppendLog("MarketTypeTradesInLastCandle: " + marketTypeTradesInLastCandle, LogEventLevel.Debug, LogStrategy.Destination.All);

                // buy by price
                if (this.Current.MarketState == MarketType.Bullish && myCandle.Close <= LimitUpperBuy)
                {
                    // save old values
                    this.Saved = this.Current.DeepClone();
                    // new parameters
                    LogStrategy.AppendLog("-> Signaling advice LONG (buy)", LogEventLevel.Debug, LogStrategy.Destination.All);
                    this.Current.PreviousAction = ActionType.SospBuy;
                    this.Current.LastLow = myCandle.Low;
                    this.Current.LastHigh = myCandle.Close;
                    events.MyTradeRequest(myCandle.Settings, TradeAction.Long,
                        int.Parse(Misc.GetParameterValue("longPercentage", NameStrategy)));
                }
                else
                    LogStrategy.AppendLog("-> Signaling advice NOT LONG (not buy)", LogEventLevel.Debug, LogStrategy.Destination.All);
            }
            string log = LogStrategy.GetLog();
            LogStrategy.AppendLog(log, LogEventLevel.Fatal, LogStrategy.Destination.File);
            onCandleUpdateCompleted(log);
        }

        void IStrategy.Events_onTradeCompleted(MyTradeCompleted tradeCompleted)
        {
            LogStrategy.AppendLog("-> Signaling onTradeCompleted", LogEventLevel.Information, LogStrategy.Destination.File);

            if (this.Current.PreviousAction == ActionType.SospSell)
            {
                this.Current.PreviousAction = ActionType.Sell;
                this.Current.PreviousActionPrice = tradeCompleted.Price;
                this.Current.LastOpDate = tradeCompleted.Date;

                // log new value
                decimal newThreshold = (this.Current.PreviousActionPrice * this.Current.BuyAt).Round(tradeCompleted.Settings.PrecisionCurrency);
                //decimal newSellAtUp = (this.Current.PreviousActionPrice * this.Current.BuyAtUp).Round(tradeCompleted.Settings.PrecisionCurrency);

                LogStrategy.AppendLog("CandleClose: " + tradeCompleted.Price.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.File);
                LogStrategy.AppendLog("New threshold: " + newThreshold.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.File);
                //LogStrategy.AppendLog("New sellAtUp: " + newSellAtUp.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.File);

                // telegram
                this.onTradeCompleted(tradeCompleted: tradeCompleted, newPrice: newThreshold, newUpDown: null);
            }
            else if (this.Current.PreviousAction == ActionType.SospBuy)
            {
                this.Current.PreviousAction = ActionType.Buy;
                this.Current.PreviousActionPrice = tradeCompleted.Price;
                this.Current.LastOpDate = tradeCompleted.Date;

                // log new values
                decimal newThreshold = (this.Current.PreviousActionPrice * this.Current.SellAt).Round(tradeCompleted.Settings.PrecisionCurrency);
                //decimal stopLoss = ComputeStopless();

                LogStrategy.AppendLog("CandleClose: " + tradeCompleted.Price.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.File);
                LogStrategy.AppendLog("New threshold: " + newThreshold.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.File);
                //LogStrategy.AppendLog("New stopLoss: " + stopLoss.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.File);

                // telegram
                this.onTradeCompleted(tradeCompleted: tradeCompleted, newPrice: newThreshold, newUpDown: null);
            }
            //else if (this.Current.PreviousAction == ActionType.SospLoss)
            //{
            //    this.Current.PreviousAction = ActionType.StopLoss;
            //    this.Current.PreviousActionPrice = tradeCompleted.Price;
            //    this.Current.LastOpDate = tradeCompleted.Date;

            //    // log new values
            //    decimal newThreshold = (this.Current.PreviousActionPrice * this.Current.SellAt).Round(tradeCompleted.Settings.PrecisionCurrency);
            //    decimal stopLoss = ComputeStopless();

            //    LogStrategy.AppendLog("CandleClose: " + tradeCompleted.Price.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.File);
            //    LogStrategy.AppendLog("New threshold: " + newThreshold.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.File);
            //    LogStrategy.AppendLog("New stopLoss: " + stopLoss.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.File);

            //    // telegram
            //    this.onTradeStopCompleted(tradeCompleted.Price, stopLoss);
            //}
        }

        void IStrategy.Events_onTradeAborted(MyTradeCancelled tradeAborted)
        {
            LogStrategy.AppendLog("-> Signaling onTradeAborted");
            this.ResetTradeAborted(TradeStatus.Aborted, tradeAborted);
        }

        void IStrategy.Events_onTradeCancelled(MyTradeCancelled traceCancelled)
        {
            LogStrategy.AppendLog("-> Signaling onTradeCancelled");
            this.ResetTradeAborted(TradeStatus.Cancelled, traceCancelled);
        }

        void IStrategy.Events_onTradeErrored(MyTradeCancelled tradeErrored)
        {
            LogStrategy.AppendLog("-> Signaling onTradeErrored");
            this.ResetTradeAborted(TradeStatus.Errored, tradeErrored);
        }

        void IStrategy.Events_onTelegramMessage(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            if (e.Message.Text.ToLower().StartsWith("/status", StringComparison.Ordinal))
                this.onEmitStatus(e.Message.Chat.Id);
            else if (e.Message.Text.ToLower().StartsWith("/short", StringComparison.Ordinal))
                this.onEmitShort(e.Message.Chat.Id, e.Message.Text);
            else if (e.Message.Text.ToLower().StartsWith("/long", StringComparison.Ordinal))
                this.onEmitLong(e.Message.Chat.Id, e.Message.Text);
            else if (e.Message.Text.ToLower().StartsWith("/reset", StringComparison.Ordinal))
                this.onEmitReset(e.Message.Chat.Id, e.Message.Text);
            else if (e.Message.Text.ToLower().StartsWith("/abort", StringComparison.Ordinal))
                this.onEmitAbort(e.Message.Chat.Id, e.Message.Text);
            else if (e.Message.Text.ToLower().StartsWith("/pause", StringComparison.Ordinal))
                this.onEditStatus(e.Message.Chat.Id, e.Message.Text, false);
            else if (e.Message.Text.ToLower().StartsWith("/resume", StringComparison.Ordinal))
                this.onEditStatus(e.Message.Chat.Id, e.Message.Text, true);
            else if (e.Message.Text.ToLower().StartsWith("/setupPrevAction".ToLower(), StringComparison.Ordinal))
                this.onEditSetupPrevAction(e.Message.Chat.Id, e.Message.Text);
            else
                telegramBot.SendTextMessageAsync(e.Message.Chat.Id, "Sorry! I don't understand!");
        }

        private void onEditSetupPrevAction(long chatId, string text)
        {
            string[] strInText = text.Split(" ");
            if (strInText.Count() != 4)
            {
                telegramBot.SendTextMessageAsync(chatId, "Format non valid");
                return;
            }
            if (!strInText.ElementAt(1).Contains(telegramPassword))
                telegramBot.SendTextMessageAsync(chatId, "Password mismatch!");
            else
            {
                // abort by telegram
                LogStrategy.AppendLog("-> Telegram advice change status: " + strInText[2] + ";" + strInText[3]);

                if (strInText[2].ToLower() == ActionType.Buy.ToString().ToLower())
                    this.Current.PreviousAction = ActionType.Buy;
                else if (strInText[2].ToLower() == ActionType.Sell.ToString().ToLower())
                    this.Current.PreviousAction = ActionType.Sell;
                this.Current.PreviousActionPrice = Convert.ToDecimal(strInText[3]);
                this.Current.LastOpDate = DateTime.Now;

                // telegram
                telegramBot.SendTextMessageAsync(chatId, "Changed setups");

            }
        }

        private void onEditStatus(long chatId, string text, bool status)
        {
            if (!text.Contains(telegramPassword))
                telegramBot.SendTextMessageAsync(chatId, "Password mismatch!");
            else
            {
                // abort by telegram
                LogStrategy.AppendLog("-> Telegram advice change status: " + status.ToString());

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
                LogStrategy.AppendLog("-> Telegram advice ABORT");

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
                LogStrategy.AppendLog("-> Telegram advice RESET");
                LogStrategy.AppendLog("CandleClose: " + this.Current.CurrentCandleClose.ToPrecision(settings, TypeCoin.Currency));
                LogStrategy.AppendLog("Last date: " + this.Current.LastOpDate.ToShortDateTimeString());
                LogStrategy.AppendLog("LastLow: " + this.Current.LastLow.ToPrecision(settings, TypeCoin.Currency));
                LogStrategy.AppendLog("LastHigh: " + this.Current.LastHigh.ToPrecision(settings, TypeCoin.Currency));
                this.Current = new Values
                {
                    PreviousAction = ActionType.Reset,
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
                LogStrategy.AppendLog("-> Telegram advice LONG");
                this.Current.PreviousAction = ActionType.SospBuy;
                this.Current.LastLow = this.Current.CurrentCandleClose;
                this.Current.LastHigh = this.Current.CurrentCandleClose;
                events.MyTradeRequest(settings, TradeAction.Long,
                    int.Parse(Misc.GetParameterValue("longPercentage", NameStrategy)));

                // telegram
                telegramBot.SendTextMessageAsync(chatId, "Trade LONG initiated");
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
                LogStrategy.AppendLog("-> Telegram advice SHORT");
                this.Current.PreviousAction = ActionType.SospSell;
                this.Current.LastLow = this.Current.CurrentCandleClose;
                this.Current.LastHigh = this.Current.CurrentCandleClose;
                events.MyTradeRequest(settings, TradeAction.Short,
                    int.Parse(Misc.GetParameterValue("shortPercentage", NameStrategy)));

                // telegram
                telegramBot.SendTextMessageAsync(chatId, "Trade SHORT initiated.");
            }
        }

        private void onEmitStatus(long chatId)
        {
            StringBuilder message = null;
            MyWebAPISettings settings = Misc.GenerateMyWebAPISettings();

            if (this.Current.PreviousAction == ActionType.None)
            {
                message = new StringBuilder();
                message.AppendLine("Trade Status: ");
                message.AppendLine("WarmUp in progress");
                telegramBot.SendTextMessageAsync(chatId, message.ToString());
                return;
            }

            message = new StringBuilder();
            message.AppendLine("Trade Status: ");
            message.AppendLine("Curr. market: " + this.Current.MarketState.ToString());
            message.AppendLine("Prev. market: " + this.Current.PreviousMarketState.ToString());
            message.AppendLine("Prev. action: " + this.Current.PreviousAction.ToString());
            message.AppendLine("Prev. action price: " + this.Current.PreviousActionPrice.ToPrecision(settings, TypeCoin.Currency));
            message.AppendLine("Prev. candle close: " + this.Current.PreviousCandleClose.ToPrecision(settings, TypeCoin.Currency));

            // log initial trade values
            //decimal newThreshold;
            //decimal stopLoss = ComputeStopless();
            if (this.Current.PreviousAction == ActionType.Sell)
            {
                message.AppendLine("Limit upper buy: " + LimitUpperBuy.ToPrecision(settings, TypeCoin.Currency));
                //newThreshold = (this.Current.PreviousActionPrice * this.Current.BuyAt).Round(settings.PrecisionCurrency);
                //decimal newSellAtUp = (this.Current.PreviousActionPrice * this.Current.BuyAtUp).Round(settings.PrecisionCurrency);
                //message.AppendLine("BuyAt price: " + newThreshold.ToPrecision(settings, TypeCoin.Currency));
            }
            else if (this.Current.PreviousAction == ActionType.Buy)
            {
                //newThreshold = (this.Current.PreviousActionPrice * this.Current.SellAt).Round(settings.PrecisionCurrency);
                //message += "\nSellAt price: " + newThreshold.ToPrecision(settings, TypeCoin.Currency);
                message.AppendLine("Limit upper sell: " + LimitLowerSell.ToPrecision(settings, TypeCoin.Currency));
            }
            //message += "\nStopLoss price: " + stopLoss.ToPrecision(settings, TypeCoin.Currency);
            telegramBot.SendTextMessageAsync(chatId, message.ToString());
        }

        private void onTradeStart(decimal low, decimal high, decimal threshold)
        {
            if (this.telegramBot == null || this.telegramUsernameTo == null) return;
            MyWebAPISettings settings = Misc.GenerateMyWebAPISettings();
            StringBuilder message = new StringBuilder();
            message.AppendLine("Trade started. ");
            message.AppendLine("Limit low: " + low.ToPrecision(settings, TypeCoin.Currency));
            message.AppendLine("Limit high: " + high.ToPrecision(settings, TypeCoin.Currency));
            message.AppendLine("New limit: " + threshold.ToPrecision(settings, TypeCoin.Currency));
            telegramBot.SendTextMessageAsync(this.telegramUsernameTo, message.ToString());
        }

        private void onTradeReset(DateTime lastOpDate, DateTime date)
        {
            if (this.telegramBot == null || this.telegramUsernameTo == null) return;
            StringBuilder message = new StringBuilder();
            message.AppendLine("Trade reset.");
            message.AppendLine("From date: " + lastOpDate.ToShortDateTimeString());
            message.AppendLine("To date: " + date.ToShortDateTimeString());
            telegramBot.SendTextMessageAsync(this.telegramUsernameTo, message.ToString());
        }

        private void onTradeCancelled(TradeStatus status, MyTradeCancelled tradeCancelled)
        {
            if (this.telegramBot == null || this.telegramUsernameTo == null) return;
            StringBuilder message = new StringBuilder();
            message.AppendLine("Trade " + status.ToString().ToLower() + ".");
            message.AppendLine("ID: " + tradeCancelled.Id.ToString().Substring(0, 6));
            if (tradeCancelled.IdReference != null)
                message.AppendLine("IdRef: " + tradeCancelled.IdReference.ToString().Substring(0, 6));
            if (tradeCancelled.Reason != null)
                message.AppendLine("Reason: " + tradeCancelled.Reason);
            telegramBot.SendTextMessageAsync(this.telegramUsernameTo, message.ToString());
        }

        private void onTradeCompleted(MyTradeCompleted tradeCompleted, decimal newPrice, decimal? newUpDown = null)
        {
            if (this.telegramBot == null || this.telegramUsernameTo == null) return;
            StringBuilder message = new StringBuilder();
            message.AppendLine("Trade completed. ");
            message.AppendLine("ID: " + tradeCompleted.OrderId);
            message.AppendLine("Action: " + tradeCompleted.Action);
            message.AppendLine("Price: " + tradeCompleted.Price.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency));
            message.AppendLine("Amount: " + tradeCompleted.Amount.ToPrecision(tradeCompleted.Settings, TypeCoin.Asset));
            message.AppendLine("Cost: " + tradeCompleted.Cost.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency));
            message.AppendLine("Balance: " + tradeCompleted.Balance.ToStringRound(2));
            message.AppendLine("Effective price: " + tradeCompleted.EffectivePrice.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency));
            message.AppendLine("New price: " + newPrice.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency));
            //if (newUpDown.HasValue)
            //{
            //    if (tradeCompleted.Action == TradeAction.Long)
            //        message += "\nNew stopLoss: " + newUpDown.Value.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency);
            //    else if (tradeCompleted.Action == TradeAction.Short)
            //        message += "\nNew sellUp: " + newUpDown.Value.ToPrecision(tradeCompleted.Settings, TypeCoin.Currency);
            //}
            LogStrategy.AppendLog(message.ToString(), LogEventLevel.Verbose, LogStrategy.Destination.All);
            telegramBot.SendTextMessageAsync(this.telegramUsernameTo, message.ToString());
        }

        private void onCandleUpdateCompleted(string message)
        {
            if (this.telegramBot == null || this.telegramUsernameTo == null) return;
            if (Misc.GetLogStrategyOnCandleUpdate)
                telegramBot.SendTextMessageAsync(this.telegramUsernameTo, message);
        }

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
                if (days >= int.Parse(Misc.GetParameterValue("timeoutDays", NameStrategy)))
                {
                    LogStrategy.AppendLog("-> Signaling advice RESET - days: " + days + " > timeoutDays", LogEventLevel.Information, LogStrategy.Destination.All);
                    LogStrategy.AppendLog("CandleClose         : " + myCandle.Close.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.All);
                    LogStrategy.AppendLog("Last date           : " + this.Current.LastOpDate.ToShortDateTimeString(), LogEventLevel.Information, LogStrategy.Destination.All);
                    LogStrategy.AppendLog("Day Passed          : " + days.ToStringRound(2), LogEventLevel.Information, LogStrategy.Destination.All);
                    LogStrategy.AppendLog("LastLow             : " + this.Current.LastLow.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.All);
                    LogStrategy.AppendLog("LastHigh            : " + this.Current.LastHigh.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.All);
                    this.Current = new Values();
                    this.Current.PreviousAction = ActionType.Reset;
                    // telegram
                    this.onTradeReset(this.Current.LastOpDate, myCandle.Date);
                }
            }
        }

        private void ResetTradeAborted(TradeStatus status, MyTradeCancelled tradeAborted)
        {
            // telegram
            if (status == TradeStatus.Cancelled)
            {
                LogStrategy.AppendLog("-> Signaling advice TRADE CANCELLED");
                LogStrategy.AppendLog("Reason: Order not filled.");
            }
            else
            {
                LogStrategy.AppendLog("-> Signaling advice TRADE ABORTED");
                LogStrategy.AppendLog("Reason: " + tradeAborted.Reason);
            }
            this.onTradeCancelled(status, tradeAborted);

            // stoploss exception
            //if (this.Current.PreviousAction == ActionType.SospLoss)
            //{
            //    LogStrategy.AppendLog("Method: Re-initiate stopless strategy.");
            //    events.MyTradeRequest(tradeAborted.Settings, TradeAction.Short,
            //        int.Parse(Misc.GetParameterValue("stopLossPercentage", NameStrategy)));
            //    return;
            //}

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
            using (BrokerDBContext db = new BrokerDBContext())
            {
                MyMACD oldMacd = db.MyMACDs.OrderByDescending(s => s.Id).Skip(1).FirstOrDefault();
                MyStrategy.MACDAverage.Value(out macdValue, out signalValue, out hist);
                if (oldMacd != null && MyStrategy.MACDAverage.isPrimed())
                {
                    Line macdLine = new Line() { x1 = 0, x2 = 1, y1 = oldMacd.MACD, y2 = macdValue };
                    Line signalLine = new Line() { x1 = 0, x2 = 1, y1 = oldMacd.SignalValue, y2 = signalValue };
                    Point c = LineIntersection.FindIntersection(macdLine, signalLine);
                    if (!c.Equals(default(Point)))
                    {
                        LogStrategy.AppendLog("-> Signaling crossover line", LogEventLevel.Information, LogStrategy.Destination.All);
                        LogStrategy.AppendLog("Point Y: " + c.y.ToStringRound(2), LogEventLevel.Debug, LogStrategy.Destination.All);
                        LogStrategy.AppendLog("MACD Y: " + macdLine.y2.ToStringRound(2), LogEventLevel.Debug, LogStrategy.Destination.All);
                        if (macdLine.y2 > c.y)
                        {
                            LogStrategy.AppendLog("Signaling crossover line - MarketState: Bullish - Direction: Up", LogEventLevel.Information, LogStrategy.Destination.All);
                            this.Current.MarketState = MarketType.Bullish;
                        }
                        else
                        {
                            LogStrategy.AppendLog("Signaling crossover line - MarketState: Bearish - Direction: Down", LogEventLevel.Information, LogStrategy.Destination.All);
                            this.Current.MarketState = MarketType.Bearish;
                        }
                    }
                }
            }
        }

        public void Events_onIamAlive()
        {
            if (this.telegramBot == null || this.telegramUsernameTo == null) return;
            StringBuilder message = new StringBuilder();
            message.AppendLine(DateTime.Now.ToShortDateTimeString());
            message.AppendLine("Broker Alive");
            telegramBot.SendTextMessageAsync(this.telegramUsernameTo, message.ToString());
        }

        //private void CheckLastTradesType(MyWebAPISettings settings, out double ResultVolume, out double ResultVolumePrice, out MarketType marketTypeTrades)
        //{
        //    List<MyTrade> list;
        //    events.MyTradesListRequest(settings, out list);
        //    ResultVolume = 0;
        //    ResultVolumePrice = 0;
        //    foreach (var item in list)
        //    {
        //        if (item.action == Enumerator.TradeAction.Long)
        //        {
        //            ResultVolume += Convert.ToDouble(item.qty);
        //            ResultVolumePrice += Convert.ToDouble(item.quoteQty);
        //        }
        //        else
        //        {
        //            ResultVolume += (Convert.ToDouble(item.qty) * Convert.ToDouble(-1));
        //            ResultVolumePrice += (Convert.ToDouble(item.quoteQty) * Convert.ToDouble(-1));
        //        }
        //    }
        //    if (ResultVolume > 0)
        //        marketTypeTrades = MarketType.Bullish;
        //    else
        //        marketTypeTrades = MarketType.Bearish;
        //}

        //private void onTradeStopCompleted(decimal price, decimal newStopless)
        //{
        //    if (this.telegramBot == null || this.telegramUsernameTo == null) return;
        //    MyWebAPISettings settings = Misc.GenerateMyWebAPISettings();
        //    string message = "StopLoss exited. " +
        //        "\nPrice: " + price.ToPrecision(settings, TypeCoin.Currency) +
        //        "\nNew stopLoss: " + newStopless.ToPrecision(settings, TypeCoin.Currency) +
        //        "\nLimit low: " + this.Current.LastLow.ToPrecision(settings, TypeCoin.Currency) +
        //        "\nLimit high: " + this.Current.LastHigh.ToPrecision(settings, TypeCoin.Currency);
        //    telegramBot.SendTextMessageAsync(this.telegramUsernameTo, message);
        //}

        //private void onTradeCandle(CandleType type, decimal price, decimal? newPrice = null)
        //{
        //    if (this.telegramBot == null || this.telegramUsernameTo == null) return;
        //    MyWebAPISettings settings = Misc.GenerateMyWebAPISettings();
        //    string message = "Limit changed. " +
        //        "\nDirection: " + (type == CandleType.High ? "High" : "Low") +
        //        "\nPrice: " + price.ToPrecision(settings, TypeCoin.Currency);
        //    if (newPrice.HasValue && newPrice.Value > 0)
        //        message += "\nNew stopLoss: " + newPrice.Value.ToPrecision(settings, TypeCoin.Currency);
        //    telegramBot.SendTextMessageAsync(this.telegramUsernameTo, message);
        //}

        //private decimal ComputeStopless()
        //{
        //    decimal high = this.Current.PreviousActionPrice > this.Current.LastHigh ?
        //        this.Current.PreviousActionPrice : this.Current.LastHigh;
        //    decimal stopLossPerc = high * this.Current.StopLoss;
        //    decimal stopLossMin = high - Misc.GetParameterValue("stopLossMin", "stoploss").ToDecimal();
        //    return stopLossPerc < stopLossMin ? stopLossPerc : stopLossMin;
        //}

        //private bool DetectStopLoss(MyCandle myCandle, decimal stopLoss)
        //{
        //    if (this.Current.PreviousAction == ActionType.None ||
        //        this.Current.PreviousAction == ActionType.WarmUp ||
        //        this.Current.PreviousAction == ActionType.Sell) return false;

        //    // stoploss strategy
        //    if (myCandle.Close < stopLoss)
        //    {

        //        // log values
        //        LogStrategy.AppendLog("-> Inside STOPLOSS strategy", LogEventLevel.Debug, LogStrategy.Destination.All);
        //        LogStrategy.AppendLog("CandleClose: " + myCandle.Close.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Debug, LogStrategy.Destination.All);
        //        LogStrategy.AppendLog("StopLoss parameter: " + this.Current.StopLoss.ToStringRound(4), LogEventLevel.Debug, LogStrategy.Destination.All);
        //        LogStrategy.AppendLog("StopLoss: " + stopLoss.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Debug, LogStrategy.Destination.All);
        //        LogStrategy.AppendLog("PreviousActionPrice: " + this.Current.PreviousActionPrice.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Debug, LogStrategy.Destination.All);
        //        LogStrategy.AppendLog("PreviousAction: " + this.Current.PreviousAction, LogEventLevel.Debug, LogStrategy.Destination.All);

        //        // first stoploss signal
        //        if (this.Current.PreviousAction != ActionType.StopLoss)
        //        {

        //            // save old values
        //            this.Saved = this.Current.DeepClone();

        //            // new parameters
        //            LogStrategy.AppendLog("-> Signaling advice STOPLOSS - Sell by StopLoss", LogEventLevel.Information, LogStrategy.Destination.All);
        //            LogStrategy.AppendLog("CandleClose: " + myCandle.Close.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.All);
        //            LogStrategy.AppendLog("StopLoss parameter: " + this.Current.StopLoss.ToStringRound(4), LogEventLevel.Information, LogStrategy.Destination.All);
        //            LogStrategy.AppendLog("StopLoss: " + stopLoss.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.All);
        //            LogStrategy.AppendLog("PreviousAction: " + this.Current.PreviousAction, LogEventLevel.Information, LogStrategy.Destination.All);
        //            LogStrategy.AppendLog("PreviousActionPrice: " + this.Current.PreviousActionPrice.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.All);

        //            this.Current.PreviousActionPrice = myCandle.Close;
        //            this.Current.PreviousAction = ActionType.SospLoss;

        //            events.MyTradeRequest(myCandle.Settings, TradeAction.Short,
        //                int.Parse(Misc.GetParameterValue("stopLossPercentage", NameStrategy)));

        //            return true;

        //        }

        //        // second or more stoploss signal
        //        else if (myCandle.Close <= this.Current.PreviousActionPrice)
        //        {
        //            LogStrategy.AppendLog("-> Signaling advice STOPLOSS - FREEFALL", LogEventLevel.Information, LogStrategy.Destination.All);
        //            LogStrategy.AppendLog("CandleClose: " + myCandle.Close.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.All);
        //            LogStrategy.AppendLog("StopLoss parameter: " + this.Current.StopLoss.ToStringRound(4), LogEventLevel.Information, LogStrategy.Destination.All);
        //            LogStrategy.AppendLog("StopLoss: " + stopLoss.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.All);
        //            LogStrategy.AppendLog("PreviousActionPrice: " + this.Current.PreviousActionPrice.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.All);
        //            LogStrategy.AppendLog("PreviousAction: " + this.Current.PreviousAction, LogEventLevel.Information, LogStrategy.Destination.All);
        //            this.Current.PreviousActionPrice = myCandle.Close;
        //            LogStrategy.AppendLog("Update PreviousActionPrice: " + this.Current.PreviousActionPrice, LogEventLevel.Information, LogStrategy.Destination.All);
        //            return true;
        //        }

        //    }

        //    // stoploss exit on price go up, enable buy
        //    if (this.Current.PreviousAction == ActionType.StopLoss &&
        //        (myCandle.Close >= stopLoss ||
        //            this.Current.MarketState == MarketType.Bullish))
        //    {
        //        // new parameters
        //        LogStrategy.AppendLog("-> Signaling advice EXIT STOPLOSS - stoploss exit on price go up, enable buy", LogEventLevel.Information, LogStrategy.Destination.All);
        //        LogStrategy.AppendLog("CandleClose: " + myCandle.Close.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.All);
        //        LogStrategy.AppendLog("StopLoss parameter: " + this.Current.StopLoss.ToStringRound(4), LogEventLevel.Information, LogStrategy.Destination.All);
        //        LogStrategy.AppendLog("StopLoss: " + stopLoss.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.All);
        //        LogStrategy.AppendLog("PreviousActionPrice: " + this.Current.PreviousActionPrice.ToPrecision(myCandle.Settings, TypeCoin.Currency), LogEventLevel.Information, LogStrategy.Destination.All);
        //        this.Current.PreviousAction = ActionType.Sell;
        //        this.Current.PreviousActionPrice = myCandle.Close;
        //        this.Current.LastHigh = myCandle.High;
        //        this.Current.LastLow = myCandle.Low;

        //        // telegram
        //        this.onTradeStopCompleted(myCandle.Close, stopLoss);
        //    }

        //    return false;
        //}
    }
}