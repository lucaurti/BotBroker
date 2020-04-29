using System.Collections.Generic;
using Broker.Common.Utility;
using Broker.Common.WebAPI.Models;
using static Broker.Common.Strategies.Enumerator;

namespace Broker.Common.Events
{
    public class MyEvents : IEvents
    {

        // handler
        public event MyWarmUpEndedEventHandler onWarmUpEnded;
        public event MyTickerEventHandler onTickerUpdate;
        public event MyCandleEventHandler onCandleUpdate;
        public event MyTradeCompletedEventHandler onTradeCompleted;
        public event MyTradeAbortedEventHandler onTradeAborted;
        public event MyTradeCancelledEventHandler onTradeCancelled;
        public event MyTradeErroredEventHandler onTradeErrored;
        public event MyTradeRequestHandler myTradeRequest;
        public event MyWarmUpRequestHandler myWarmUpRequest;
        public event MyTradesListRequestHandler onTradesListRequest;


        // events
        public void OnWarmUpEnded()
        {
            onWarmUpEnded?.Invoke();
        }
        public void OnTickerUpdate(List<MyTicker> myTickers)
        {
            onTickerUpdate?.Invoke(myTickers);
        }
        public void OnCandleUpdate(MyCandle myCandle)
        {
            onCandleUpdate?.Invoke(myCandle);
        }
        public void OnTradeCompleted(MyTradeCompleted tradeCompleted)
        {
            onTradeCompleted?.Invoke(tradeCompleted);
        }
        public void OnTradeAborted(MyTradeCancelled tradeAborted)
        {
            onTradeAborted?.Invoke(tradeAborted);
        }
        public void OnTradeCancelled(MyTradeCancelled traceCancelled)
        {
            onTradeCancelled?.Invoke(traceCancelled);
        }
        public void OnTradeErrored(MyTradeCancelled tradeErrored)
        {
            onTradeErrored?.Invoke(tradeErrored);
        }
        public void MyTradeRequest(MyWebAPISettings settings, TradeAction tradeType, int? percentage = null, decimal? price = null, decimal? priceLimit = null)
        {
            myTradeRequest?.Invoke(settings, tradeType, percentage, price, priceLimit);
        }
        public void MyWarmUpRequest()
        {
            myWarmUpRequest?.Invoke();
        }
        public void MyTradesListRequest(MyWebAPISettings settings, out List<MyTrade> tradesList)
        {
            tradesList = new List<MyTrade>();
            onTradesListRequest?.Invoke(settings, out tradesList);
        }
    }
}
