using Broker.Common.Events;
using Broker.Common.Utility;
using System.Collections.Generic;
using Telegram.Bot;

namespace Broker.Common.Strategies
{
    public interface IStrategy
    {

        // properties
        TelegramBotClient TelegramBotClient { get; set; }
        string TelegramBotPassword { get; set; }
        string TelegramBotUsernameTo { get; set; }


        // events
        void Events_onWarmUpEnded();
        void Events_onTickerUpdate(List<MyTicker> myTickers);
        void Events_onCandleUpdate(MyCandle myCandle);
        void Events_onTradeCompleted(MyTradeCompleted tradeCompleted);
        void Events_onTradeAborted(MyTradeCancelled tradeAborted);
        void Events_onTradeCancelled(MyTradeCancelled traceCancelled);
        void Events_onTradeErrored(MyTradeCancelled tradeErrored);
        void Events_onTelegramMessage(object sender, Telegram.Bot.Args.MessageEventArgs e);
        void Events_onIamAlive();
    }

}
