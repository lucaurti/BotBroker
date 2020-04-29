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
using Websocket.Client;
using static Broker.Common.Strategies.Enumerator;

namespace Broker.Common.WebAPI.Coinbase
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
            BaseEndpoint = new Uri("https://api.pro.coinbase.com/");
            var urlSocket = new Uri("wss://ws-feed.pro.coinbase.com");
            if (Extension.UseWebSocketTickers)
            {
                secondsWebSocket = int.Parse(Misc.GetParameterValue("secondsWebSocket", Misc.GetExchange)) * 1000;
                Task.Run(() => WebSocketManager(Misc.GenerateMyWebAPISettings(), urlSocket, WebSocket));
            }
        }


        // overrided functions
        private HttpClient CreateHttpClient(Uri baseUri, string timestamp)
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
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("CB-ACCESS-KEY", authentication.KeyID);
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("CB-ACCESS-PASSPHRASE", authentication.KeySecret);
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Broker C#");

            // return
            httpClient.DefaultRequestHeaders.Add("CB-ACCESS-TIMESTAMP", timestamp);
            httpClient.DefaultRequestHeaders.Accept
                    .Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.BaseAddress = baseUri;
            return httpClient;
        }
        private T GetAsync<T>(Uri baseUri, HttpMethod method, string requestUrl, params string[] parametersUrl)
        {
            // get time
            Time time = baseUri.GetAsync<Time>(HttpMethod.Get, "time");
            string timestamp = time.epoch.ToString("F0");
            HttpClient httpClient = CreateHttpClient(baseUri, timestamp); string body = null;
            string uri = httpClient.BaseAddress.ComposeURI(method, requestUrl, parametersUrl);
            using (var request = new HttpRequestMessage(method, uri))
            {
                // body

                if (method == HttpMethod.Post)
                {
                    var values = new Dictionary<string, string>();
                    foreach (string param in parametersUrl)
                        values.Add(
                            param.Substring(0, param.IndexOf('=')),
                            param.Substring(param.IndexOf('=') + 1)
                        );
                    var json = JsonConvert.SerializeObject(values);
                    body = json.ToString();
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                // firm message
                string uriSign = "/".ComposeURI(method, requestUrl, parametersUrl);
                string text = String.Concat(timestamp, method.ToString().ToUpper(), uriSign, (body ?? ""));
                string firm = SignMessage(Misc.GetParameterValue("keySign", Misc.GetExchange), text);
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("CB-ACCESS-SIGN", firm);

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
            var response = GetAsync<Ticker>(BaseEndpoint, HttpMethod.Get, "products/" + settings.Pair + "/ticker");
            tickers = new List<MyTicker>();

            // save
            MyTicker ticker = new MyTicker
            {
                Ask = response.ask.ToDecimal(),
                Bid = response.bid.ToDecimal(),
                Timestamp = DateTime.Parse(response.time.ToString()).ToEpochTime(),
                LastTrade = response.price.ToDecimal(),
                Volume = response.volume.ToDecimal(),
                Settings = settings
            };
            tickers.Add(ticker);

            return (tickers.Count > 0);
        }
        public bool GetBalance(MyWebAPISettings settings, out List<MyBalance> balances)
        {
            // client 
            var response = GetAsync<List<Balance>>(BaseEndpoint, HttpMethod.Get, "accounts");
            balances = new List<MyBalance>();

            // save
            foreach (var x in response)
            {
                MyBalance balance = new MyBalance
                {
                    Date = DateTime.Now,
                    Amount = x.available.ToDecimal(),
                    Asset = x.currency,
                    Reserved = x.hold.ToDecimal()
                };
                balances.Add(balance);
            }

            return (balances.Count > 0);
        }
        public bool PostNewOrder(MyWebAPISettings settings, Enumerator.TradeAction tradeAction, decimal volume, decimal price, out string orderID)
        {
            // client
            var response = GetAsync<Order>(BaseEndpoint, HttpMethod.Post, "orders",
                "size=" + volume.ToPrecision(settings, TypeCoin.Asset), "price=" + price.ToPrecision(settings, TypeCoin.Currency),
                "side=" + (tradeAction == Enumerator.TradeAction.Long ? "buy" : "sell"), "product_id=" + settings.Pair);

            // return
            orderID = response.id;
            return true;
        }
        public bool GetOrder(MyWebAPISettings settings, string orderID, out MyOrder order)
        {
            // client 
            var response = GetAsync<Order>(BaseEndpoint, HttpMethod.Get, "orders", orderID);
            order = new MyOrder
            {

                // save
                Completed =
                    (response.done_at != null) ?
                    DateTime.Parse(response.done_at.ToString()).ToEpochTime() :
                    DateTime.Now.ToEpochTime(),
                Creation =
                    (response.created_at != null) ?
                    DateTime.Parse(response.created_at.ToString()).ToEpochTime() :
                    DateTime.Now.ToEpochTime(),
                Fee = response.fill_fees.ToDecimal(),
                OrderId = response.id,
                Price = response.funds.ToDecimal(),
                State = ToOrderState(response.status),
                Type = (response.side == "buy" ? Enumerator.TradeAction.Long : Enumerator.TradeAction.Short),
                Volume = response.size.ToDecimal(),
                Settings = settings

            };

            return true;
        }
        public bool PostCancelOrder(MyWebAPISettings settings, string orderID)
        {
            // client 
            var response = GetAsync<bool>(BaseEndpoint, HttpMethod.Delete, "orders", orderID);

            // return
            return response;
        }
        public bool GetOrderBook(MyWebAPISettings settings, out List<MyOrderBook> orderBook)
        {
            // client 
            var response = GetAsync<List<OrderBook>>(BaseEndpoint, HttpMethod.Get, "products/" + settings.Pair + "/trades");
            orderBook = new List<MyOrderBook>();

            foreach (var x in response)
            {
                MyOrderBook order = new MyOrderBook
                {
                    Action = (x.side == "buy" ? Enumerator.TradeAction.Long : Enumerator.TradeAction.Short),
                    Price = x.price.ToDecimal(),
                    Timestamp = DateTime.Parse(x.time).ToEpochTime(),
                    Volume = x.size.ToDecimal(),
                    Settings = settings
                };
                orderBook.Add(order);
            }
            orderBook = orderBook.OrderBy(s => s.Action).ToList();

            return (orderBook.Count > 0);
        }


        // private functions
        private Enumerator.TradeState ToOrderState(string state)
        {
            switch (state)
            {
                case "done": return Enumerator.TradeState.Completed;
                case "open": return Enumerator.TradeState.Pending;
                default: return Enumerator.TradeState.Pending;
            }
        }
        private string SignMessage(string key, string message)
        {
            ASCIIEncoding encoding = new ASCIIEncoding();
            byte[] secret = Convert.FromBase64String(key);
            byte[] bytes = encoding.GetBytes(message);
            using (HMACSHA256 hmaccsha = new HMACSHA256(secret))
                return Convert.ToBase64String(hmaccsha.ComputeHash(bytes));
        }


        // websocket
        private async Task WebSocketManager(MyWebAPISettings settings, Uri urlSocket, IWebSocket websocket)
        {
            var exitEvent = new ManualResetEvent(false);
            try
            {
                using (var client = new WebsocketClient(urlSocket))
                {
                    client.ReconnectTimeoutMs = (int)TimeSpan.FromSeconds(Misc.GetTickerTime).TotalMilliseconds;
                    client.ReconnectionHappened.Subscribe(type =>
                    {
                        Log.Debug("Socket restart         : " + type.ToString());
                        WebSocketSubscribe(settings, client);
                    });
                    client.MessageReceived.Subscribe(
                        msg =>
                        {
                            SocketTicker response = JsonConvert.DeserializeObject<SocketTicker>(msg.Text);
                            if (response.type == "ticker")
                            {
                                MyTicker ticker = new MyTicker
                                {
                                    Ask = response.best_ask.ToDecimal(),
                                    Bid = response.best_bid.ToDecimal(),
                                    Timestamp = DateTime.Now.ToEpochTime(),
                                    LastTrade = response.price.ToDecimal(),
                                    Volume = (response.last_size != null ? response.last_size.ToDecimal() : 0),
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
        private void WebSocketSubscribe(MyWebAPISettings settings, WebsocketClient client)
        {
            // subscribe
            List<string> product = new List<string>(); product.Add(settings.Pair);
            List<object> channels = new List<object>(); channels.Add("ticker");
            SocketSubscribe subscribe = new SocketSubscribe()
            {
                type = "subscribe",
                product_ids = product,
                channels = channels
            };
            string json = JsonConvert.SerializeObject(subscribe);
            client.Send(JsonConvert.SerializeObject(subscribe));
            Log.Debug("-> WebSocket        : Subscribed");
        }

        public bool GetTrades(MyWebAPISettings settings, out List<MyTrade> trades)
        {
            throw new NotImplementedException();
        }
    }
}
