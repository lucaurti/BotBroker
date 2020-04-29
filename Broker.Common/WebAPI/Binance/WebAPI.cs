using Broker.Common.Events;
using Broker.Common.Strategies;
using Broker.Common.Utility;
using Broker.Common.WebAPI.Models;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Websocket.Client;
using static Broker.Common.Strategies.Enumerator;

namespace Broker.Common.WebAPI.Binance
{
    public class WebAPI : IWebAPI
    {
        // variables
        MyAuthentication authentication;
        int secondsWebSocket;


        // properties
        MyAuthentication Autentication { get; set; }
        Uri BaseEndpoint { get; set; }
        public MyAuthentication Authentication { get => authentication; set => authentication = value; }


        // init
        public WebAPI(IWebSocket WebSocket)
        {
            BaseEndpoint = new Uri("https://api.binance.com/api/");
            var urlSocket = new Uri("wss://stream.binance.com:9443/");
            if (Extension.UseWebSocketTickers)
            {
                secondsWebSocket = int.Parse(Misc.GetParameterValue("secondsWebSocket", Misc.GetExchange)) * 1000;
                Task.Run(() => WebSocketManager(Misc.GenerateMyWebAPISettings(), urlSocket, WebSocket));
            }
        }


        // overrided functions
        private HttpClient CreateHttpClient(Uri baseUri)
        {
            HttpClient httpClient;

            // set proxy if needed
            if (Extension.GetProxyHost != null && Extension.GetProxyPort.HasValue)
            {
                HttpClientHandler httpClientHandler = new HttpClientHandler()
                {
                    Proxy = new WebProxy(Extension.GetProxyHost, Extension.GetProxyPort.Value),
                    UseDefaultCredentials = true
                };
                httpClient = new HttpClient(httpClientHandler);
            }
            else
                httpClient = new HttpClient();

            // autentication

            // return
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Broker C#");
            httpClient.DefaultRequestHeaders.Accept
                    .Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.BaseAddress = baseUri;
            return httpClient;
        }

        private T GetAsync<T>(Uri baseUri, HttpMethod method, bool isSigned, string requestUrl, params string[] parametersUrl)
        {

            // firm message
            if (isSigned)
            {
                Time time = baseUri.GetAsync<Time>(HttpMethod.Get, "v1/time");
                List<string> paramList = parametersUrl.ToList();
                paramList.Add("timestamp=" + time.serverTime);
                paramList.Add("recvWindow=60000");
                string uriSign = string.Empty;
                foreach (string param in paramList) uriSign += param + "&";
                uriSign = uriSign.Substring(0, uriSign.Length - 1);
                string firm = SignMessage(Misc.GetParameterValue("keySecret", Misc.GetExchange), uriSign);
                paramList.Add("signature=" + firm); parametersUrl = paramList.ToArray();
            }

            // get client
            HttpClient httpClient = CreateHttpClient(baseUri);
            string uri = httpClient.BaseAddress.ComposeURI(HttpMethod.Get, requestUrl, parametersUrl);
            if (isSigned)
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-MBX-APIKEY",
                    Misc.GetParameterValue("keyId", Misc.GetExchange));

            // call and response
            using (var request = new HttpRequestMessage(method, uri))
            {
                var response = httpClient.SendAsync(request).Result;
                if (response.IsSuccessStatusCode)
                {
                    var data = response.Content.ReadAsStringAsync().Result;
                    httpClient.Dispose();
                    if (typeof(T) == typeof(Boolean))
                        return (T)Convert.ChangeType(true, typeof(T));
                    return JsonConvert.DeserializeObject<T>(data);
                }
                else
                {
                    httpClient.Dispose();
                    string message = (response.Content != null) ? " - " + response.Content.ReadAsStringAsync().Result : "";
                    throw new Exception(response.ReasonPhrase + message);
                }
            }

        }

        // interface functions
        public bool GetTicker(MyWebAPISettings settings, out List<MyTicker> tickers)
        {
            // client 
            var lastTrade = GetAsync<SymbolPrice>(BaseEndpoint, HttpMethod.Get, false, "v3/ticker/price", "symbol=" + settings.Pair);
            var response = GetAsync<Ticker>(BaseEndpoint, HttpMethod.Get, false, "v3/ticker/bookTicker", "symbol=" + settings.Pair);
            tickers = new List<MyTicker>();

            // save
            MyTicker ticker = new MyTicker
            {
                Ask = response.askPrice.ToDecimal(),
                Bid = response.bidPrice.ToDecimal(),
                Timestamp = DateTime.Now.ToEpochTime(),
                LastTrade = lastTrade.price.ToDecimal(),
                Volume = (response.askQty.ToDecimal() + response.bidQty.ToDecimal()),
                Settings = settings
            };
            tickers.Add(ticker);

            return (tickers.Count > 0);
        }
        public bool GetBalance(MyWebAPISettings settings, out List<MyBalance> balances)
        {
            // client 
            var response = GetAsync<AccountInfo>(BaseEndpoint, HttpMethod.Get, true, "v3/account");
            balances = new List<MyBalance>();

            // save
            foreach (var x in response.balances)
            {
                MyBalance balance = new MyBalance
                {
                    Date = DateTime.Now,
                    Amount = x.free.ToDecimal(),
                    Asset = x.asset,
                    Reserved = x.locked.ToDecimal()
                };
                balances.Add(balance);
            }

            return (balances.Count > 0);
        }

        public bool PostNewOrder(MyWebAPISettings settings, Enumerator.TradeAction tradeAction, decimal volume, decimal price, out string orderID)
        {
            // return
            orderID = null;

            // check min order volume
            var responseEx = GetAsync<ExchangeInfo>(BaseEndpoint, HttpMethod.Get, false, "v1/exchangeInfo");
            var symbol = responseEx.symbols.Where(s => s.symbol.ToUpper() == settings.Pair).FirstOrDefault();
            if (symbol == null) throw new Exception("Symbol not found.");
            var filter = symbol.filters.Where(s => s.filterType == "MIN_NOTIONAL").FirstOrDefault();
            if (filter == null) throw new Exception("Filter MIN_NOTIONAL not found.");
            decimal minOrder = filter.minNotional.ToDecimal();
            if ((volume * price) < minOrder)
                throw new Exception("Minimum volume (" + minOrder.ToPrecision(settings, TypeCoin.Asset) + ") not reachable.");

            // manage price
            var responseAvg = GetAsync<AvgPrice>(BaseEndpoint, HttpMethod.Get, false, "v3/avgPrice", "symbol=" + settings.Pair);
            if (responseAvg == null) throw new Exception("Avg price not found.");
            filter = symbol.filters.Where(s => s.filterType == "PERCENT_PRICE").FirstOrDefault();
            if (filter == null) throw new Exception("Filter PERCENT_PRICE not found.");
            decimal minPrice = responseAvg.price.ToDecimal() * filter.multiplierDown.ToDecimal();
            decimal maxPrice = responseAvg.price.ToDecimal() * filter.multiplierUp.ToDecimal();
            if (price < minPrice || price > maxPrice)
            {
                if (tradeAction == TradeAction.Long) price = minPrice;
                else price = maxPrice;
            }

            // client
            var response = GetAsync<PostOrder>(BaseEndpoint, HttpMethod.Post, true, "v3/order",
                "symbol=" + settings.Pair, "side=" + (tradeAction == Enumerator.TradeAction.Long ? "BUY" : "SELL"),
                "type=LIMIT", "quantity=" + volume.ToPrecision(settings, TypeCoin.Asset), "timeInForce=GTC",
                "price=" + price.ToPrecision(settings, TypeCoin.Currency));

            // return
            orderID = response.clientOrderId;
            return true;
        }
        public bool GetOrder(MyWebAPISettings settings, string orderID, out MyOrder order)
        {
            // client 
            var response = GetAsync<Order>(BaseEndpoint, HttpMethod.Get, true, "v3/order",
                "symbol=" + settings.Pair, "origClientOrderId=" + orderID);

            // get fees
            decimal makerFee = 0;
            TradeState state = ToOrderState(response.status);
            if (state == TradeState.Completed)
            {
                try
                {
                    var responseFee = GetAsync<AccountInfo>(BaseEndpoint, HttpMethod.Get, true, "v3/account");
                    makerFee = ("0." + responseFee.makerCommission).ToDecimal();
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message + Environment.NewLine + ex.ToString());
                }
            }
            order = new MyOrder
            {
                // save
                Completed = (
                    response.updateTime > 0 ?
                    ((long)response.updateTime / 1000) :
                    DateTime.Now.ToEpochTime()
                ),
                Creation = ((long)response.time / 1000),
                Fee = (response.executedQty.ToDecimal() * makerFee * response.price.ToDecimal()) / 100,
                OrderId = response.clientOrderId,
                Price = response.price.ToDecimal(),
                State = ToOrderState(response.status),
                Type = (response.side == "BUY" ? Enumerator.TradeAction.Long : Enumerator.TradeAction.Short),
                Volume = response.executedQty.ToDecimal(),
                Settings = settings

            };

            return true;
        }
        public bool PostCancelOrder(MyWebAPISettings settings, string orderID)
        {
            // client 
            var response = GetAsync<CancelOrder>(BaseEndpoint, HttpMethod.Delete, true, "v3/order",
                "symbol=" + settings.Pair, "origClientOrderId=" + orderID);

            // return
            return (response.status == "CANCELED");
        }

        public bool GetOrderBook(MyWebAPISettings settings, out List<MyOrderBook> orderBook)
        {
            // client 
            var response = GetAsync<OrderBooks>(BaseEndpoint, HttpMethod.Get, false, "v1/depth", "symbol=" + settings.Pair, "limit=1000");
            orderBook = new List<MyOrderBook>();

            // ask
            foreach (var x in response.asks)
            {
                MyOrderBook order = new MyOrderBook
                {
                    Action = Enumerator.TradeAction.Long,
                    Price = x[0].ToDecimal(),
                    Timestamp = DateTime.Now.ToEpochTime(),
                    Volume = x[1].ToDecimal(),
                    Settings = settings
                };
                orderBook.Add(order);
            }

            // bid
            foreach (var x in response.bids)
            {
                MyOrderBook order = new MyOrderBook
                {
                    Action = Enumerator.TradeAction.Short,
                    Price = x[0].ToDecimal(),
                    Timestamp = DateTime.Now.ToEpochTime(),
                    Volume = x[1].ToDecimal(),
                    Settings = settings
                };
                orderBook.Add(order);
            }
            //orderBook = orderBook.OrderBy(s => s.Action).ToList();

            return (orderBook.Count > 0);
        }


        // private functions
        private Enumerator.TradeState ToOrderState(string state)
        {
            switch (state)
            {
                case "FILLED": return Enumerator.TradeState.Completed;
                default: return Enumerator.TradeState.Pending;
            }
        }
        private string SignMessage(string apiSecret, string message)
        {
            var key = Encoding.UTF8.GetBytes(apiSecret);
            string stringHash;
            using (var hmac = new HMACSHA256(key))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                stringHash = BitConverter.ToString(hash).Replace("-", "");
            }
            return stringHash;
        }

        // websocket
        private async Task WebSocketManager(MyWebAPISettings settings, Uri urlSocket, IWebSocket websocket)
        {
            var exitEvent = new ManualResetEvent(false);
            var uriSocket = new Uri(urlSocket.ComposeURI(HttpMethod.Get, "ws", settings.Pair.ToLower() + "@ticker"));
            try
            {
                using (WebsocketClient client = new WebsocketClient(uriSocket))
                {
                    client.ReconnectTimeoutMs = (int)TimeSpan.FromSeconds(Misc.GetTickerTime).TotalMilliseconds;
                    client.ReconnectionHappened.Subscribe(type =>
                    {
                        Log.Debug("Socket restart         : " + type.ToString());
                    });
                    client.MessageReceived.Subscribe(
                        msg =>
                        {
                            SocketTicker response = JsonConvert.DeserializeObject<SocketTicker>(msg.Text);
                            if (response.e == "24hrTicker")
                            {
                                MyTicker ticker = new MyTicker
                                {
                                    Ask = response.a.ToDecimal(),
                                    Bid = response.b.ToDecimal(),
                                    Timestamp = DateTime.Now.ToEpochTime(),
                                    LastTrade = response.c.ToDecimal(),
                                    Volume = response.A.ToDecimal() + response.B.ToDecimal(),
                                    Settings = settings
                                };
                                Log.Debug("Socket ticker          : " + ticker.LastTrade.ToPrecision(settings, TypeCoin.Currency));
                                websocket.OnSocketTickerUpdate(ticker);
                            }
                            if (secondsWebSocket > 0)
                                System.Threading.Thread.Sleep(secondsWebSocket);
                        }
                    );
                    await client.Start();
                    Log.Information("-> WebSocket        : Active");
                    exitEvent.WaitOne();
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex.Message + Environment.NewLine + ex.ToString());
            }
        }

        public bool GetTrades(MyWebAPISettings settings, out List<MyTrade> trades)
        {
            // client 
            var response = GetAsync<List<Trade>>(BaseEndpoint, HttpMethod.Get, false, "v3/trades", "symbol=" + settings.Pair, "limit=5000");
            trades = new List<MyTrade>();
            DateTime startDateTime = DateTime.Now;
            while (true)
            {
                if (startDateTime.Minute % Misc.GetCandleTime == 0)
                    break;
                startDateTime = startDateTime.AddMinutes(-1);
            }
            DateTime endDateTime = startDateTime.AddMinutes(Misc.GetCandleTime);
            startDateTime = new DateTime(startDateTime.Year, startDateTime.Month, startDateTime.Day, startDateTime.Hour, startDateTime.Minute, 0);
            endDateTime = new DateTime(endDateTime.Year, endDateTime.Month, endDateTime.Day, endDateTime.Hour, endDateTime.Minute, 0);
            long start = Convert.ToInt64(startDateTime.ToUnixTimeStamp());
            long end = Convert.ToInt64(endDateTime.ToUnixTimeStamp());
            foreach (var x in response)
            {
                if (x.time >= start && x.time <= end)
                {
                    MyTrade myTrade = new MyTrade();
                    myTrade.id = x.id;
                    myTrade.isBestMatch = x.isBestMatch;
                    if (x.isBuyerMaker)
                        myTrade.action = Enumerator.TradeAction.Long;
                    else
                        myTrade.action = Enumerator.TradeAction.Short;
                    myTrade.price = x.price;
                    myTrade.qty = x.qty;
                    myTrade.quoteQty = x.quoteQty;
                    myTrade.time = x.time;
                    trades.Add(myTrade);
                }
            }
            return (trades.Count > 0);
        }
    }
}
