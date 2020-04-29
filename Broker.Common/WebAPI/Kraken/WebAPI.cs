using Broker.Common.Events;
using Broker.Common.Strategies;
using Broker.Common.Utility;
using Broker.Common.WebAPI.Models;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Websocket.Client;

namespace Broker.Common.WebAPI.Kraken
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
            BaseEndpoint = new Uri("https://api.kraken.com");
            var urlSocket = new Uri("wss://ws.kraken.com");
            if (Extension.UseWebSocketTickers)
            {
                secondsWebSocket = int.Parse(Misc.GetParameterValue("secondsWebSocket", Misc.GetExchange)) * 1000;
                Task.Run(() => WebSocketManager(Misc.GenerateMyWebAPISettings(), urlSocket, WebSocket));
            }
        }

        public bool GetTicker(MyWebAPISettings settings, out List<MyTicker> tickers)
        {
            // client 
            var response = PublicAsync<Tickers.Ticker>(settings, BaseEndpoint.AbsoluteUri, "0", "Ticker", "pair=" + settings.Pair);
            tickers = new List<MyTicker>();

            // save
            MyTicker ticker = new MyTicker
            {
                Ask = response.result.TickerValue.Ask.ToDecimal(),
                Bid = response.result.TickerValue.Bid.ToDecimal(),
                Timestamp = DateTime.Now.ToEpochTime(),
                LastTrade = response.result.TickerValue.LastTrade.ToDecimal(),
                Volume = response.result.TickerValue.Volume.ToDecimal(),
                Settings = settings
            };
            tickers.Add(ticker);

            return (tickers.Count > 0);
        }
        public bool GetBalance(MyWebAPISettings settings, out List<MyBalance> balances)
        {
            // client 
            var response = PrivateAsync<Balances.Balance>(settings, BaseEndpoint.AbsoluteUri, "0", "Balance", "");
            balances = new List<MyBalance>();
            // save
            foreach (var x in response.result)
            {
                MyBalance balance = new MyBalance
                {
                    Date = DateTime.Now,
                    Amount = x.Value.ToDecimal(),
                    Asset = x.Key,
                    Reserved = 0
                };
                balances.Add(balance);
            }

            return (balances.Count > 0);
        }
        public bool PostNewOrder(MyWebAPISettings settings, Enumerator.TradeAction tradeAction, decimal volume, decimal price, out string orderID)
        {
            Order o = new Order(settings.Pair, tradeAction == Enumerator.TradeAction.Long ? "buy" : "sell", "limit", volume, price);
            var reqs = CreateOrder(o);
            OrderResult response = PrivateAsync<OrderResult>(settings, BaseEndpoint.AbsoluteUri, "0", "AddOrder", reqs);
            if (response.error.Count == 0)
            {
                orderID = response.result.txid.FirstOrDefault();
                return true;
            }
            orderID = "";
            return false;
        }
        public bool PostCancelOrder(MyWebAPISettings settings, string orderID)
        {
            string reqs = string.Format("&txid={0}", orderID);
            CancelledOrders.CancelledOrder response = PrivateAsync<CancelledOrders.CancelledOrder>(settings, BaseEndpoint.AbsoluteUri, "0", "CancelOrder", reqs);
            if (response.error.Count == 0 && response.result.count == 1)
                return true;
            return false;
        }
        public bool GetOrder(MyWebAPISettings settings, string orderID, out MyOrder order)
        {
            string userref = "";
            string reqs = string.Format("&trades={0}", true);
            if (!string.IsNullOrEmpty(userref))
                reqs += string.Format("&userref={0}", userref);
            OpenedOrders.OpenedOrder response = PrivateAsync<OpenedOrders.OpenedOrder>(settings, BaseEndpoint.AbsoluteUri, "0", "OpenOrders", reqs);
            if (response.error.Count == 0)
            {
                var requestOrder = response.result.open.orders.Where(item => item.id == orderID).FirstOrDefault();
                if (requestOrder != null)
                {
                    order = new MyOrder
                    {
                        // save
                        Completed = DateTime.MinValue.ToEpochTime(),
                        Creation = DateTime.Parse(requestOrder.opentm.ToString()).ToEpochTime(),
                        Fee = requestOrder.fee.ToDecimal(),
                        OrderId = requestOrder.id,
                        Price = requestOrder.price.ToDecimal(),
                        State = ToOrderState(requestOrder.status),
                        Type = (requestOrder.descr.type == "buy" ? Enumerator.TradeAction.Long : Enumerator.TradeAction.Short),
                        Volume = requestOrder.vol.ToDecimal(),
                        Settings = settings
                    };
                    return true;
                }
            }
            order = null;
            return false;
        }
        public bool GetOrderBook(MyWebAPISettings settings, out List<MyOrderBook> orderBook)
        {
            orderBook = new List<MyOrderBook>();
            string reqs = string.Format("pair={0}", settings.Pair);
            int? count = 5;
            if (count.HasValue)
                reqs += string.Format("&count={0}", count.Value.ToString());
            var response = PublicAsync<OrdersBook.OrderBook>(settings, BaseEndpoint.AbsoluteUri, "0", "Depth", reqs);
            if (response.error.Count == 0)
            {
                var askList = response.result.ListOrderBooks.asks;
                var bidList = response.result.ListOrderBooks.asks;
                var list = askList.Union(bidList);
                foreach (var x in askList)
                {
                    MyOrderBook order = new MyOrderBook
                    {
                        Action = Enumerator.TradeAction.Long,
                        Price = x[0].ToString().ToDecimal(),
                        Timestamp = Convert.ToUInt64(x[2].ToString()),
                        Volume = x[1].ToString().ToDecimal(),
                        Settings = settings
                    };
                    orderBook.Add(order);
                }
                foreach (var x in bidList)
                {
                    MyOrderBook order = new MyOrderBook
                    {
                        Action = Enumerator.TradeAction.Short,
                        Price = x[0].ToString().ToDecimal(),
                        Timestamp = Convert.ToUInt64(x[2].ToString()),
                        Volume = x[1].ToString().ToDecimal(),
                        Settings = settings
                    };
                    orderBook.Add(order);
                }
                orderBook = orderBook.OrderByDescending(s => s.Timestamp).ThenBy(n => n.Action).ToList();
            }
            return (orderBook.Count > 0);
        }

        private HttpWebRequest CreateHttpWebRequest(string requestUri, HttpMethod method)
        {
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(requestUri);
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.Method = method.Method;
            webRequest.Headers.Add("API-Key", Misc.GetParameterValue("keySign", Misc.GetExchange));
            if (Extension.GetProxyHost != null && Extension.GetProxyPort.HasValue)
            {
                webRequest.Proxy = new WebProxy(Extension.GetProxyHost, Extension.GetProxyPort.Value);
                webRequest.UseDefaultCredentials = true;
            }
            return webRequest;
        }
        private T PublicAsync<T>(MyWebAPISettings settings, string baseUri, string version, string methodUri, string props)
        {
            string address = string.Format("{0}{1}/public/{2}", baseUri, version, methodUri);
            HttpWebRequest webRequest = CreateHttpWebRequest(address, HttpMethod.Post);
            if (props != null)
            {
                using (var writer = new StreamWriter(webRequest.GetRequestStream()))
                {
                    writer.Write(props);
                }
            }
            var json = GetResponseResult<T>(settings, webRequest);
            var generic = JsonConvert.DeserializeObject<T>(json);
            return generic;
        }
        private T PrivateAsync<T>(MyWebAPISettings settings, string baseUri, string version, string methodUri, string props)
        {
            string path = string.Format("/{0}/private/{1}", version, methodUri);
            string address = baseUri + (path.Remove(0, 1));
            // generate a 64 bit nonce using a timestamp at tick resolution
            Int64 nonce = DateTime.Now.Ticks;
            props = "nonce=" + nonce + props;
            HttpWebRequest webRequest = CreateHttpWebRequest(address, HttpMethod.Post);
            var np = nonce + Convert.ToChar(0) + props;
            SignMessageAndAddToHeader(path, np, ref webRequest);
            AddPropsToWebRequest(props, ref webRequest);
            var json = GetResponseResult<T>(settings, webRequest);
            var generic = JsonConvert.DeserializeObject<T>(json);
            return generic;
        }
        private string GetResponseResult<T>(MyWebAPISettings settings, HttpWebRequest webRequest)
        {
            using (WebResponse webResponse = webRequest.GetResponse())
            {
                using (Stream str = webResponse.GetResponseStream())
                {
                    using (StreamReader sr = new StreamReader(str))
                    {
                        var json = sr.ReadToEnd();
                        if (typeof(T).GetMethod("ToCorrectJson") != null)
                        {
                            ConstructorInfo constructor = typeof(T).GetConstructor(Type.EmptyTypes);
                            object instance = constructor.Invoke(new object[] { });
                            json = (string)typeof(T).GetMethod("ToCorrectJson").Invoke(instance, new object[] { settings, json });
                        }
                        return json;
                    }
                }
            }
        }
        private void AddPropsToWebRequest(string props, ref HttpWebRequest webRequest)
        {
            if (props != null)
            {
                using (var writer = new StreamWriter(webRequest.GetRequestStream()))
                {
                    writer.Write(props);
                }
            }
        }

        private void SignMessageAndAddToHeader(string path, string npProps, ref HttpWebRequest webRequest)
        {
            byte[] base64DecodedSecred = Convert.FromBase64String(Misc.GetParameterValue("keySecret", Misc.GetExchange));
            var pathBytes = Encoding.UTF8.GetBytes(path);
            var hash256Bytes = sha256_hash(npProps);
            var z = new byte[pathBytes.Count() + hash256Bytes.Count()];
            pathBytes.CopyTo(z, 0);
            hash256Bytes.CopyTo(z, pathBytes.Count());
            var signature = getHash(base64DecodedSecred, z);
            webRequest.Headers.Add("API-Sign", Convert.ToBase64String(signature));
        }
        private Enumerator.TradeState ToOrderState(string state)
        {
            switch (state)
            {
                case "done": return Enumerator.TradeState.Completed;
                case "open": return Enumerator.TradeState.Pending;
                default: return Enumerator.TradeState.Pending;
            }
        }
        private byte[] sha256_hash(String value)
        {
            using (SHA256 hash = SHA256Managed.Create())
            {
                Encoding enc = Encoding.UTF8;

                Byte[] result = hash.ComputeHash(enc.GetBytes(value));

                return result;
            }
        }
        private byte[] getHash(byte[] keyByte, byte[] messageBytes)
        {
            using (var hmacsha512 = new HMACSHA512(keyByte))
            {

                Byte[] result = hmacsha512.ComputeHash(messageBytes);

                return result;

            }
        }
        public string CreateOrder(Order order)
        {
            string strVolume = order.Volume.ToString().Replace(",", ".");
            string reqs = string.Format("&pair={0}&type={1}&ordertype={2}&volume={3}&leverage={4}", order.Pair, order.Type, order.OrderType, order.Volume, order.Leverage);
            if (!String.IsNullOrWhiteSpace(order.Price))
                reqs += string.Format("&price={0}", order.Price);
            if (!String.IsNullOrWhiteSpace(order.Price2))
                reqs += string.Format("&price2={0}", order.Price2);
            if (!string.IsNullOrEmpty(order.Position))
                reqs += string.Format("&position={0}", order.Position);
            if (!string.IsNullOrEmpty(order.Starttm))
                reqs += string.Format("&starttm={0}", order.Starttm);
            if (!string.IsNullOrEmpty(order.Expiretm))
                reqs += string.Format("&expiretm={0}", order.Expiretm);
            if (!string.IsNullOrEmpty(order.OFlags))
                reqs += string.Format("&oflags={0}", order.OFlags);
            if (!string.IsNullOrEmpty(order.Userref))
                reqs += string.Format("&userref={0}", order.Userref);
            if (order.Validate)
                reqs += "&validate=true";
            if (order.Close != null)
            {
                string closeString = string.Format("&close[ordertype]={0}&close[price]={1}&close[price2]={2}", order.Close["ordertype"], order.Close["price"], order.Close["price2"]);
                reqs += closeString;
            }
            return reqs;
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
                            SocketSubscribeStatus SocketSubscribeStatus;
                            try
                            {
                                SocketSubscribeStatus = JsonConvert.DeserializeObject<SocketSubscribeStatus>(msg.Text);
                            }
                            catch (JsonSerializationException)
                            {
                                SocketSubscribeStatus = null;
                            }
                            if (SocketSubscribeStatus == null)
                            {
                                string json = SocketTicker.ToCorrectJson(settings, msg.Text);
                                SocketTicker socketTicker = JsonConvert.DeserializeObject<SocketTicker>(json);
                                if (socketTicker != null)
                                {
                                    MyTicker ticker = new MyTicker
                                    {
                                        Ask = socketTicker.Ask.ToDecimal(),
                                        Bid = socketTicker.Bid.ToDecimal(),
                                        Timestamp = DateTime.Now.ToEpochTime(),
                                        LastTrade = socketTicker.LastTrade.ToDecimal(),
                                        Volume = socketTicker.Volume.ToDecimal(),
                                        Settings = settings
                                    };
                                    Log.Debug("Socket ticker          : " + ticker.LastTrade.ToString("F2"));
                                    websocket.OnSocketTickerUpdate(ticker);
                                }
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
            List<string> pairs = new List<string>();
            pairs.Add(settings.Asset.Substring(1) + "/" + settings.Currency.Substring(1));
            Subscription subscription = new Subscription()
            {
                name = "ticker"
            };
            SocketSubscribe subscribe = new SocketSubscribe()
            {
                @event = "subscribe",
                pair = pairs,
                subscription = subscription
            };
            string json = JsonConvert.SerializeObject(subscribe);
            client.Send(JsonConvert.SerializeObject(subscribe));
        }

        public bool GetTrades(MyWebAPISettings settings, out List<MyTrade> trades)
        {
            throw new NotImplementedException();
        }
    }
}
