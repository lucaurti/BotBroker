using Broker.Common.Strategies;
using Broker.Common.Utility;
using Broker.Common.WebAPI.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using static Broker.Common.Strategies.Enumerator;

namespace Broker.Common.WebAPI.Cobinhood
{
    public class WebAPI : IWebAPI
    {
        // variables
        MyAuthentication authentication;


        // properties
        Uri BaseEndpoint { get; set; }
        public MyAuthentication Authentication { get => authentication; set => authentication = value; }


        // init
        public WebAPI()
        {
            BaseEndpoint = new Uri("https://api.cobinhood.com/v1/");
        }


        // interface functions
        public bool GetTicker(MyWebAPISettings settings, out List<MyTicker> tickers)
        {
            // client 
            var response = BaseEndpoint.GetAsync<Tickers.Tickers>(HttpMethod.Get, "market/tickers", settings.Pair);
            tickers = new List<MyTicker>();

            // save
            MyTicker ticker = new MyTicker
            {
                Ask = response.result.ticker.lowest_ask.ToDecimal(),
                Bid = response.result.ticker.highest_bid.ToDecimal(),
                Timestamp = long.Parse(response.result.ticker.timestamp.ToString()) / 1000,
                LastTrade = response.result.ticker.last_trade_price.ToDecimal(),
                Volume = response.result.ticker.volume.ToDecimal(),
                Settings = settings
            };
            tickers.Add(ticker);

            return (tickers.Count > 0);
        }
        public bool GetBalance(MyWebAPISettings settings, out List<MyBalance> balances)
        {
            // client 
            var response = BaseEndpoint.GetAsync<Balances.Balances>(HttpMethod.Get, "wallet/balances", authentication);
            balances = new List<MyBalance>();

            // save
            foreach (var x in response.result.balances)
            {
                MyBalance balance = new MyBalance
                {
                    Date = DateTime.Now,
                    Amount = x.total.ToDecimal(),
                    Asset = x.currency,
                    Reserved = x.on_order.ToDecimal()
                };
                balances.Add(balance);
            }

            return (balances.Count > 0);
        }
        public bool PostNewOrder(MyWebAPISettings settings, Enumerator.TradeAction tradeAction, decimal volume, decimal price, out string orderID)
        {
            // client
            var response = BaseEndpoint.GetAsync<Orders.Order>(HttpMethod.Post, "trading/orders", authentication,
                "trading_pair_id=" + settings.Pair, "side=" + (tradeAction == Enumerator.TradeAction.Long ? "bid" : "ask"),
                "type=limit", "price=" + price.ToPrecision(settings, TypeCoin.Currency), "size=" + volume.ToPrecision(settings, TypeCoin.Asset));


            // return
            orderID = response.result.order.id;
            return response.success;
        }
        public bool GetOrder(MyWebAPISettings settings, string orderID, out MyOrder order)
        {
            // variable
            DateTime dataParse;

            // client 
            var response = BaseEndpoint.GetAsync<Orders.Order>(HttpMethod.Get, "trading/orders", authentication, orderID);
            order = new MyOrder
            {

                // save
                Completed = (DateTime.TryParse(response.result.order.completed_at, out dataParse) ? dataParse : DateTime.Now).ToEpochTime(),
                Creation = long.Parse(response.result.order.timestamp.ToString()) / 1000,
                Fee = 0,
                OrderId = response.result.order.id,
                Price = response.result.order.price.ToDecimal(),
                State = ToOrderState(response.result.order.state),
                Type = (response.result.order.type == "bid" ? Enumerator.TradeAction.Long : Enumerator.TradeAction.Short),
                Volume = response.result.order.size.ToDecimal(),
                Settings = settings

            };

            // return
            return response.success;
        }
        public bool PostCancelOrder(MyWebAPISettings settings, string orderID)
        {
            // client 
            var response = BaseEndpoint.GetAsync<StatusOrders.StatusOrder>(HttpMethod.Delete, "trading/orders", authentication, orderID);

            // return
            return response.success;
        }
        public bool GetOrderBook(MyWebAPISettings settings, out List<MyOrderBook> orderBook)
        {
            // client 
            var response = BaseEndpoint.GetAsync<OrderBooks.OrderBook>(
                HttpMethod.Get, "market/orderbooks", authentication, settings.Pair);
            orderBook = new List<MyOrderBook>();

            // long
            foreach (var x in response.result.orderbook.asks)
            {
                ulong i = 0;
                MyOrderBook order = new MyOrderBook
                {
                    Action = Enumerator.TradeAction.Long,
                    Price = x[0].ToDecimal(),
                    Timestamp = DateTime.Now.ToEpochTime() + i++,
                    Volume = x[2].ToDecimal(),
                    Settings = settings
                };
                orderBook.Add(order);
            }

            // short
            foreach (var x in response.result.orderbook.bids)
            {
                ulong i = 0;
                MyOrderBook order = new MyOrderBook
                {
                    Action = Enumerator.TradeAction.Short,
                    Price = x[0].ToDecimal(),
                    Timestamp = DateTime.Now.ToEpochTime() + i++,
                    Volume = x[2].ToDecimal(),
                    Settings = settings
                };
                orderBook.Add(order);
            }

            return (orderBook.Count > 0);
        }


        // private functions
        private Enumerator.TradeState ToOrderState(string state)
        {
            switch (state)
            {
                case "filled": return Enumerator.TradeState.Completed;
                default: return Enumerator.TradeState.Pending;
            }
        }

        public bool GetTrades(MyWebAPISettings settings, out List<MyTrade> trades)
        {
            throw new NotImplementedException();
        }
    }
}
