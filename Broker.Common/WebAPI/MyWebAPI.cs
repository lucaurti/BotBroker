using Broker.Common.Events;
using Broker.Common.Utility;
using Broker.Common.WebAPI.Models;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using static Broker.Common.Strategies.Enumerator;

namespace Broker.Common.WebAPI
{
    public class MyWebAPI
    {

        // variables
        private readonly IWebAPI webAPI;
        private readonly IEvents events;
        private readonly IWebSocket socket;


        // properties
        private MyOrder PaperOrder { get; set; } = new MyOrder();
        public MyWebAPISettings Settings { get; private set; }


        // init
        public MyWebAPI(MyWebAPISettings Settings, IWebAPI WebAPI, IEvents Events, IWebSocket Socket)
        {
            this.Settings = Settings;
            webAPI = WebAPI;
            events = Events;
            socket = Socket;
            WebAPI.Authentication = Extension.GetAuthentication;
            Socket.onSocketTickerUpdate += WebSocket_OnTickerUpdate;
        }


        // functions
        public bool GetTicker(out List<MyTicker> tickers, MyWebAPISettings Settings = null, bool saveAndEvent = true)
        {
            BrokerDBContext db = new BrokerDBContext();
            try
            {
                int _upd = 0; MyWebAPISettings app = (Settings ?? this.Settings);
                if (webAPI.GetTicker(app, out tickers))
                {
                    Log.Debug("WebAbi tickers       : " + tickers.Count);
                    if (saveAndEvent)
                    {
                        foreach (var _ticker in tickers)
                        {
                            _ticker.Settings = app.GenerateMyWebAPISettings(db);
                            db.MyTickers.Add(_ticker);
                        }
                        _upd = db.SaveChanges();
                        events.OnTickerUpdate(tickers);
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return (tickers != null);
        }

        public bool GetCandle(out MyCandle myCandle)
        {
            myCandle = null;
            BrokerDBContext db = new BrokerDBContext();
            DateTime data = DateTime.Now;
            long end = (long)data.ToEpochTime();
            long start = (long)data.AddMinutes(Misc.GetCandleTime * -1).ToEpochTime();
            List<MyTicker> myTickers = db.MyTickers
                .Where(s => s.Timestamp >= start && s.Timestamp <= end)
                .OrderBy(s => s.Timestamp)
                .ToList();

            Log.Debug("Found candle        : " + myTickers.Count);
            if (myTickers.Count > 0)
            {
                myCandle = new MyCandle
                {
                    Date = data,
                    High = myTickers.Max(s => s.LastTrade),
                    Low = myTickers.Min(s => s.LastTrade),
                    Open = myTickers.First().LastTrade,
                    Close = myTickers.Last().LastTrade,
                    Settings = Settings.GenerateMyWebAPISettings(db)
                };
                try
                {
                    db.MyCandles.Add(myCandle);
                    db.SaveChanges();
                    events.OnCandleUpdate(myCandle);
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message + Environment.NewLine + ex.ToString());
                    throw ex;
                }
            }

            return (myCandle != null);
        }

        public void RemoveOldTicker()
        {
            BrokerDBContext db = new BrokerDBContext();
            DateTime data = DateTime.Now.AddDays(-1);
            long dataTimeStamp = (long)data.ToEpochTime();
            List<MyTicker> myTickers = db.MyTickers
                .Where(s => s.Timestamp <= dataTimeStamp)
                .ToList();
            Log.Debug("Found old tickers        : " + myTickers.Count);
            if (myTickers.Count > 0)
            {
                try
                {
                    db.MyTickers.RemoveRange(myTickers);
                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message + Environment.NewLine + ex.ToString());
                    throw ex;
                }
            }
        }

        public void RemoveOldRSI()
        {
            BrokerDBContext db = new BrokerDBContext();
            DateTime data = DateTime.Now.AddDays(-7);
            long dataTimeStamp = (long)data.ToEpochTime();
            List<MyRSI> myRSI = db.MyRSIs
                .Where(s => s.Timestamp <= dataTimeStamp)
                .ToList();
            Log.Debug("Found old rsi        : " + myRSI.Count);
            if (myRSI.Count > 0)
            {
                try
                {
                    db.MyRSIs.RemoveRange(myRSI);
                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message + Environment.NewLine + ex.ToString());
                    throw ex;
                }
            }
        }

        public void RemoveOldMomentums()
        {
            BrokerDBContext db = new BrokerDBContext();
            DateTime data = DateTime.Now.AddDays(-7);
            long dataTimeStamp = (long)data.ToEpochTime();
            List<MyMomentum> myMomentums = db.MyMomentums
                .Where(s => s.Timestamp <= dataTimeStamp)
                .ToList();
            Log.Debug("Found old momentums        : " + myMomentums.Count);
            if (myMomentums.Count > 0)
            {
                try
                {
                    db.MyMomentums.RemoveRange(myMomentums);
                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message + Environment.NewLine + ex.ToString());
                    throw ex;
                }
            }
        }

        public void RemoveOldMacd()
        {
            BrokerDBContext db = new BrokerDBContext();
            DateTime data = DateTime.Now.AddDays(-7);
            long dataTimeStamp = (long)data.ToEpochTime();
            List<MyMACD> myMacd = db.MyMACDs
                .Where(s => s.Timestamp <= dataTimeStamp)
                .ToList();
            Log.Debug("Found old macd        : " + myMacd.Count);
            if (myMacd.Count > 0)
            {
                try
                {
                    db.MyMACDs.RemoveRange(myMacd);
                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message + Environment.NewLine + ex.ToString());
                    throw ex;
                }
            }
        }

        public void RemoveUnnecessaryBalanceAndCandle()
        {
            BrokerDBContext db = new BrokerDBContext();
            try
            {
                //recupero gli ordini effettuati
                List<MyOrder> myOrderCreations = db.MyOrders.ToList();
                List<int> balancesOrder = new List<int>();
                //recupero il balance legato all'ordine
                foreach (var myOrderCreation in myOrderCreations)
                {
                    //recupero il balance legato all'ordine al momento della creazione
                    DateTime dateTimeCreation = myOrderCreation.Creation.ToDateTime();
                    balancesOrder.AddRange(db.MyBalances
                        .Where(b => b.Date.Year == dateTimeCreation.Year
                            && b.Date.Month == dateTimeCreation.Month
                            && b.Date.Day == dateTimeCreation.Day
                            && b.Date.Hour == dateTimeCreation.Hour
                            && b.Date.Minute == dateTimeCreation.Minute).Select(n => n.Id)
                        .ToList());

                    //recupero il balance legato all'ordine al momento del completamento
                    DateTime dateTimeCompleted = myOrderCreation.Completed.ToDateTime();
                    while (dateTimeCompleted.Minute % 5 != 0)
                    {
                        dateTimeCompleted = dateTimeCompleted.AddMinutes(1);
                    }
                    balancesOrder.AddRange(db.MyBalances
                        .Where(b => b.Date.Year == dateTimeCompleted.Year
                            && b.Date.Month == dateTimeCompleted.Month
                            && b.Date.Day == dateTimeCompleted.Day
                            && b.Date.Hour == dateTimeCompleted.Hour
                            && b.Date.Minute == dateTimeCompleted.Minute).Select(n => n.Id)
                        .ToList());
                }
                //effettuo la distinct
                balancesOrder = balancesOrder.Distinct().ToList();
                //recupero le candele lagate al balance
                List<int> myCandlesNecessary = (from b in db.MyBalances
                                                where balancesOrder.Contains(b.Id)
                                                select b.Candle.Id).Distinct().ToList();
                //recupero tutte le candele non necessarie minore di 8 giorni per evitare conflitti di foreign key con altre tabella
                List<MyCandle> myCandlesUnnecessary = (from c in db.MyCandles
                                                       where !myCandlesNecessary.Contains(c.Id)
                                                            && c.Date < DateTime.Now.AddDays(-8)
                                                       select c).ToList();
                //recupero tutte i balance non necessari
                List<MyBalance> myBalancesUnnecessary = (from b in db.MyBalances
                                                         where !balancesOrder.Contains(b.Id)
                                                         select b).ToList();
                //elimino i balance non necessari
                db.MyBalances.RemoveRange(myBalancesUnnecessary);
                //elimino le candele non necessarie
                db.MyCandles.RemoveRange(myCandlesUnnecessary);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + Environment.NewLine + ex.ToString());
                throw ex;
            }
        }

        public bool GetBalance(out List<MyBalance> balance)
        {
            try
            {
                return webAPI.GetBalance(Settings, out balance);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + Environment.NewLine + ex.ToString());
                throw ex;
            }
        }

        public bool PostNewOrder(TradeAction tradeAction, decimal volume, decimal price, out string orderID)
        {
            try
            {
                if (Misc.IsPaperTrade)
                {
                    PaperOrder = new MyOrder()
                    {
                        Completed = DateTime.Now.ToEpochTime(),
                        Creation = PaperOrder.Completed,
                        Fee = 0,
                        OrderId = Guid.NewGuid().ToString(),
                        Price = price,
                        State = TradeState.Completed,
                        Type = tradeAction,
                        Volume = volume,
                        Settings = this.Settings
                    };
                    orderID = PaperOrder.OrderId;
                    return true;
                }
                else
                {
                    PaperOrder = null;
                    return webAPI.PostNewOrder(Settings, tradeAction, volume, price, out orderID);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + Environment.NewLine + ex.ToString());
                throw ex;
            }
        }

        public bool GetOrder(string orderID, out MyOrder myOrder)
        {
            try
            {
                if (Misc.IsPaperTrade)
                {
                    myOrder = PaperOrder;
                    return true;
                }
                else
                {
                    webAPI.GetOrder(Settings, orderID, out myOrder);
                    if (myOrder == null) return false;
                    return (myOrder.OrderId == orderID);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + Environment.NewLine + ex.ToString());
                throw ex;
            }
        }

        public bool PostCancelOrder(string orderID)
        {
            try
            {
                if (Misc.IsPaperTrade)
                    return true;
                else
                    return webAPI.PostCancelOrder(Settings, orderID);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + Environment.NewLine + ex.ToString());
                throw ex;
            }
        }

        public bool GetOrderBook(out List<MyOrderBook> orderBooks)
        {
            try
            {
                return webAPI.GetOrderBook(Settings, out orderBooks);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + Environment.NewLine + ex.ToString());
                throw ex;
            }
        }

        public bool GetTrades(out List<MyTrade> tradesList)
        {
            try
            {
                return webAPI.GetTrades(Settings, out tradesList);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + Environment.NewLine + ex.ToString());
                throw ex;
            }
        }

        // without parameters
        public bool GetTicker()
        {
            List<MyTicker> tickers;
            return GetTicker(out tickers);
        }

        public bool GetCandle()
        {
            MyCandle myCandle;
            return GetCandle(out myCandle);
        }

        // events
        private void WebSocket_OnTickerUpdate(MyTicker myTicker)
        {
            BrokerDBContext db = new BrokerDBContext();
            List<MyTicker> tickers = new List<MyTicker>();
            try
            {
                // check last ticker
                MyTicker last = db.MyTickers.LastOrDefault();
                if (last != null)
                {
                    if (myTicker.Timestamp == last.Timestamp)
                        return;
                    if (myTicker.Ask == last.Ask &&
                        myTicker.Bid == last.Bid &&
                        myTicker.LastTrade == last.LastTrade)
                        return;
                }

                myTicker.Settings = myTicker.Settings.GenerateMyWebAPISettings(db);
                db.MyTickers.Add(myTicker);
                db.SaveChanges();
                tickers.Add(myTicker);
                events.OnTickerUpdate(tickers);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + Environment.NewLine + ex.ToString());
                throw ex;
            }
        }

    }

    public static class Extension
    {

        // functions
        private static HttpClient CreateHttpClient(Uri baseUri, MyAuthentication authentication)
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
            if (authentication != null)
            {

                string base64authorization = authentication.KeyID;
                if (base64authorization.Length > 0) base64authorization += ":";
                base64authorization += authentication.KeySecret;
                if (Extension.MustBase64Enconding)
                {
                    base64authorization = Convert.ToBase64String(Encoding.ASCII.GetBytes(base64authorization));
                    base64authorization = $"Basic {base64authorization}";
                }
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("authorization", base64authorization);
            }

            // return
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Broker C#");
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("nonce", DateTime.Now.ToUnixTimeStamp());
            httpClient.DefaultRequestHeaders.Accept
                    .Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.BaseAddress = baseUri;
            return httpClient;
        }
        public static string ComposeURI(this Uri baseUri, HttpMethod method, string requestUrl, params string[] parameters)
        {
            string uri = baseUri.OriginalString;
            return uri.ComposeURI(method, requestUrl, parameters);
        }
        public static string ComposeURI(this string baseUri, HttpMethod method, string requestUrl, params string[] parameters)
        {
            StringBuilder builder = new StringBuilder(baseUri);
            bool isPresentKeyPair = true;

            builder.Append(requestUrl);
            if (method != HttpMethod.Post)
            {
                if (parameters.Length > 0)
                    builder.Append('?');
                foreach (string x in parameters)
                {
                    builder.Append(x + "&");
                    isPresentKeyPair = (x.Contains('='));
                }
                if (builder[builder.Length - 1] == '&')
                    builder.Remove(builder.Length - 1, 1);
            }
            return isPresentKeyPair ?
                builder.ToString() :
                builder.Replace('?', '/').ToString();
        }
        public static T GetAsync<T>(this Uri baseUri, HttpMethod method, string requestUrl, MyAuthentication authentication, params string[] parametersUrl)
        {
            HttpClient httpClient = CreateHttpClient(baseUri, authentication);
            string uri = httpClient.BaseAddress.ComposeURI(method, requestUrl, parametersUrl);
            using (var request = new HttpRequestMessage(method, uri))
            {
                if (method == HttpMethod.Post)
                {
                    var values = new Dictionary<string, string>();
                    foreach (string param in parametersUrl)
                        values.Add(
                            param.Substring(0, param.IndexOf('=')),
                            param.Substring(param.IndexOf('=') + 1)
                        );

                    if (IsJsonRequest)
                    {
                        var json = JsonConvert.SerializeObject(values);
                        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    }
                    else
                        request.Content = new FormUrlEncodedContent(values);
                }

                var response = httpClient.SendAsync(request).Result;
                if (response.IsSuccessStatusCode)
                {
                    var data = response.Content.ReadAsStringAsync().Result;
                    httpClient.Dispose();
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
        public static T GetAsync<T>(this Uri baseUri, HttpMethod method, string requestUrl, bool requestAuth, params string[] parametersUrl)
        {
            return GetAsync<T>(baseUri, method, requestUrl, (requestAuth ? GetAuthentication : null), parametersUrl);
        }
        public static T GetAsync<T>(this Uri baseUri, HttpMethod method, string requestUrl, params string[] parametersUrl)
        {
            return GetAsync<T>(baseUri, method, requestUrl, false, parametersUrl);
        }
        public static string ToPrecision(this decimal values, MyWebAPISettings settings, TypeCoin coin)
        {
            if (coin == TypeCoin.Currency)
                return values.ToStringRound(settings.PrecisionCurrency);
            else
                return values.ToStringRound(settings.PrecisionAsset);
        }


        // properties
        public static MyAuthentication GetAuthentication
        {
            get
            {
                string exchange = Misc.GetExchange;
                MyAuthentication auth = new MyAuthentication
                {
                    KeyID = Misc.GetParameterValue("keyId", exchange),
                    KeySecret = Misc.GetParameterValue("keySecret", exchange)
                };
                return auth;
            }
        }
        public static bool MustBase64Enconding
        {
            get
            {
                return bool.Parse(Misc.GetParameterValue("mustBase64Enconding", Misc.GetExchange));
            }
        }
        public static bool UseWebSocketTickers
        {
            get
            {
                return bool.Parse(Misc.GetParameterValue("useWebSocketTickers", Misc.GetExchange));
            }
        }
        public static bool IsJsonRequest
        {
            get
            {
                return bool.Parse(Misc.GetParameterValue("isJsonRequest", Misc.GetExchange));
            }
        }
        public static decimal GetSlipPage
        {
            get
            {
                return Misc.GetParameterValue("slippage").ToDecimal();
            }
        }
        public static decimal GetMaxSpread
        {
            get
            {
                return Misc.GetParameterValue("maxSpread").ToDecimal();
            }
        }
        public static int GetRetryTime
        {
            get
            {
                return int.Parse(Misc.GetParameterValue("secondsRetry"));
            }
        }
        public static int GetRetryLimit
        {
            get
            {
                return int.Parse(Misc.GetParameterValue("limitRetry"));
            }
        }
        public static string GetProxyHost
        {
            get
            {
                var x = Misc.GetParameterValue("proxyHost");
                return (x == "") ? null : x;
            }
        }
        public static int? GetProxyPort
        {
            get
            {
                var x = Misc.GetParameterValue("proxyPort");
                return (x == "") ? (int?)null : int.Parse(x);
            }
        }

    }

}
