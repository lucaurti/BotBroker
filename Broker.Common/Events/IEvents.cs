using Broker.Common.Strategies;
using Broker.Common.Utility;
using Broker.Common.WebAPI.Models;
using System.Collections.Generic;
using static Broker.Common.Strategies.Enumerator;

namespace Broker.Common.Events
{

    // delegates
    public delegate void MyWarmUpEndedEventHandler();
    public delegate void MyTickerEventHandler(List<MyTicker> myTickers);
    public delegate void MyCandleEventHandler(MyCandle myCandle);
    public delegate void MyTradeCompletedEventHandler(MyTradeCompleted tradeCompleted);
    public delegate void MyTradeAbortedEventHandler(MyTradeCancelled tradeAborted);
    public delegate void MyTradeCancelledEventHandler(MyTradeCancelled traceCancelled);
    public delegate void MyTradeErroredEventHandler(MyTradeCancelled tradeErrored);
    public delegate void MyTradeRequestHandler(MyWebAPISettings settings,
        TradeAction tradeType, int? percentage = null, decimal? price = null, decimal? priceLimit = null);
    public delegate void MyWarmUpRequestHandler();
    public delegate void MyTradesListRequestHandler(MyWebAPISettings settings, out List<MyTrade> tradesList);


    public interface IEvents
    {

        // events
        event MyWarmUpEndedEventHandler onWarmUpEnded;
        event MyTickerEventHandler onTickerUpdate;
        event MyCandleEventHandler onCandleUpdate;
        event MyTradeRequestHandler myTradeRequest;
        event MyWarmUpRequestHandler myWarmUpRequest;
        event MyTradeCompletedEventHandler onTradeCompleted;
        event MyTradeAbortedEventHandler onTradeAborted;
        event MyTradeCancelledEventHandler onTradeCancelled;
        event MyTradeErroredEventHandler onTradeErrored;
        event MyTradesListRequestHandler onTradesListRequest;


        // invokers
        void OnWarmUpEnded();
        void OnTickerUpdate(List<MyTicker> myTickers);
        void OnCandleUpdate(MyCandle myCandle);
        void OnTradeCompleted(MyTradeCompleted tradeCompleted);
        void OnTradeAborted(MyTradeCancelled tradeAborted);
        void OnTradeCancelled(MyTradeCancelled traceCancelled);
        void OnTradeErrored(MyTradeCancelled tradeErrored);
        void MyTradeRequest(MyWebAPISettings settings,
            TradeAction tradeType, int? percentage = null, decimal? price = null, decimal? priceLimit = null);
        void MyWarmUpRequest();
        void MyTradesListRequest(MyWebAPISettings settings, out List<MyTrade> tradesList);
    }

}
