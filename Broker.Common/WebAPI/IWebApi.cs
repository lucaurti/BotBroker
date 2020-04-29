using Broker.Common.Utility;
using Broker.Common.WebAPI.Models;
using System.Collections.Generic;
using static Broker.Common.Strategies.Enumerator;

namespace Broker.Common.WebAPI
{
    public interface IWebAPI
    {

        // properties
        MyAuthentication Authentication { get; set; }


        // functions
        bool GetTicker(MyWebAPISettings settings, out List<MyTicker> candles);
        bool GetBalance(MyWebAPISettings settings, out List<MyBalance> balance);
        bool PostNewOrder(MyWebAPISettings settings, TradeAction tradeAction, decimal volume, decimal price, out string orderID);
        bool GetOrder(MyWebAPISettings settings, string orderID, out MyOrder order);
        bool PostCancelOrder(MyWebAPISettings settings, string orderID);
        bool GetOrderBook(MyWebAPISettings settings, out List<MyOrderBook> orderBook);
        bool GetTrades(MyWebAPISettings settings, out List<MyTrade> trades);
    }
}
