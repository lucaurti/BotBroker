using Broker.Common.Strategies;
using Broker.Common.Utility;
using Broker.Common.WebAPI.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using static Broker.Common.Strategies.Enumerator;

namespace Broker.Common.WebAPI.Luno
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
            BaseEndpoint = new Uri("https://api.mybitx.com/api/1/");
        }


        // interface functions
        public bool GetTicker(MyWebAPISettings settings, out List<MyTicker> tickers)
        {
            // client 
            var response = BaseEndpoint.GetAsync<Tickers>(HttpMethod.Get, "tickers", "pair=" + settings.Pair);
            tickers = new List<MyTicker>();

            // save
            foreach (var x in response.tickers)
            {
                MyTicker ticker = new MyTicker
                {
                    Ask = x.ask.ToDecimal(),
                    Bid = x.bid.ToDecimal(),
                    Timestamp = long.Parse(x.timestamp.ToString()) / 1000,
                    LastTrade = x.last_trade.ToDecimal(),
                    Volume = x.rolling_24_hour_volume.ToDecimal(),
                    Settings = settings
                };
                tickers.Add(ticker);
            }

            return (tickers.Count > 0);
        }
        public bool GetBalance(MyWebAPISettings settings, out List<MyBalance> balances)
        {
            // client 
            var response = BaseEndpoint.GetAsync<Balance>(HttpMethod.Get, "balance", authentication);
            balances = new List<MyBalance>();

            // save
            foreach (var x in response.balance)
            {
                MyBalance balance = new MyBalance
                {
                    Date = DateTime.Now,
                    Amount = x.balance.ToDecimal(),
                    Asset = x.asset,
                    Reserved = x.reserved.ToDecimal()
                };
                balances.Add(balance);
            }

            return (balances.Count > 0);
        }
        public bool PostNewOrder(MyWebAPISettings settings, Enumerator.TradeAction tradeAction, decimal volume, decimal price, out string orderID)
        {
            // client
            var response = BaseEndpoint.GetAsync<PostOrder>(HttpMethod.Post, "postorder", authentication,
                "pair=" + settings.Pair, "type=" + (tradeAction == Enumerator.TradeAction.Long ? "BID" : "ASK"),
                "volume=" + volume.ToPrecision(settings, TypeCoin.Asset), "price=" + price.ToPrecision(settings, TypeCoin.Currency));

            // return
            orderID = response.order_id;
            return true;
        }
        public bool GetOrder(MyWebAPISettings settings, string orderID, out MyOrder order)
        {
            // client 
            var response = BaseEndpoint.GetAsync<Order>(HttpMethod.Get, "orders", authentication, orderID);
            order = new MyOrder
            {

                // save
                Completed = long.Parse(response.completed_timestamp.ToString()) / 1000,
                Creation = long.Parse(response.creation_timestamp.ToString()) / 1000,
                Fee = response.fee_base.ToDecimal(),
                OrderId = response.order_id,
                Price = response.limit_price.ToDecimal(),
                State = ToOrderState(response.state),
                Type = (response.type == "BID" ? Enumerator.TradeAction.Long : Enumerator.TradeAction.Short),
                Volume = response.based.ToDecimal(),
                Settings = settings

            };

            return true;
        }
        public bool PostCancelOrder(MyWebAPISettings settings, string orderID)
        {
            // client 
            var response = BaseEndpoint.GetAsync<StatusOrder>(HttpMethod.Post, "stoporder", authentication, "order_id=" + orderID);

            // return
            return response.success;
        }
        public bool GetOrderBook(MyWebAPISettings settings, out List<MyOrderBook> orderBook)
        {
            // client 
            var response = BaseEndpoint.GetAsync<OrderBooks>(
                HttpMethod.Get, "orderbook_top", authentication, "pair=" + settings.Pair);
            orderBook = new List<MyOrderBook>();

            // long
            foreach (var x in response.asks)
            {
                MyOrderBook order = new MyOrderBook
                {
                    Action = Enumerator.TradeAction.Long,
                    Price = x.price.ToDecimal(),
                    Timestamp = UInt64.Parse(response.timestamp.ToString()) / 1000,
                    Volume = x.volume.ToDecimal(),
                    Settings = settings
                };
                orderBook.Add(order);
            }

            // short
            foreach (var x in response.bids)
            {
                MyOrderBook order = new MyOrderBook
                {
                    Action = Enumerator.TradeAction.Short,
                    Price = x.price.ToDecimal(),
                    Timestamp = UInt64.Parse(response.timestamp.ToString()) / 1000,
                    Volume = x.volume.ToDecimal(),
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
                case "PENDING": return Enumerator.TradeState.Pending;
                case "COMPLETE": return Enumerator.TradeState.Completed;
                default: return Enumerator.TradeState.Pending;
            }
        }

        public bool GetTrades(MyWebAPISettings settings, out List<MyTrade> trades)
        {
            throw new NotImplementedException();
        }
    }
}
