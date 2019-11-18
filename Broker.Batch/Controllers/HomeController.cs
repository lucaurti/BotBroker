using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Broker.Batch.Models;
using Broker.Common.Utility;
using System.IO;
using static Broker.Common.Strategies.Enumerator;

namespace Broker.Batch.Controllers
{
    public class HomeController : Controller
    {
        // actions
        public IActionResult Index()
        {
            return View();
        }


        // jsons
        public JsonResult GetHeader()
        {
            using (BrokerDBContext db = new BrokerDBContext())
            {
                List<string> result = new List<string>();
                int tickerNumber = db.MyTickers.Count();
                int candleNumber = db.MyCandles.Count();
                int orderNumber = db.MyOrders.Count();
                var lastBalance = db.MyBalances.OrderByDescending(s => s.Date);
                MyCandle lastCandle = db.MyCandles.OrderByDescending(s => s.Date).FirstOrDefault();
                MyTicker lastTicker = db.MyTickers.OrderByDescending(s => s.Timestamp).FirstOrDefault();
                MyOrder lastOrder = db.MyOrders.OrderByDescending(s => s.Completed).FirstOrDefault();
                var balanceGroup = lastBalance.GroupBy(s => s.Date.ToString("dd/MM/yyyy HH:mm:00"));

                string balanceString = string.Empty, tickerString = string.Empty;
                if (lastBalance != null)
                    balanceString = "- <u>Balance</u>: <b>" + balanceGroup.First().Sum(s => s.ToEuro) + "</b>";
                if (lastTicker != null)
                    tickerString = "<u>Ticker</u>: <b>" + lastTicker.LastTrade + "</b> -";
                result.Add(tickerString + " Tot tickers: <b>" + tickerNumber + "</b> - Tot candles: <b>" + candleNumber +
                    "</b> " + balanceString);
                if (lastCandle != null)
                    result.Add("<u>Candle</u> Open: <b>" + lastCandle.Open + "</b> - Low: <b>" +
                        lastCandle.Low + "</b> - High: <b>" + lastCandle.High + "</b> - <u>Close</u>: <b>" +
                        lastCandle.Close + "</b>");
                if (lastOrder != null)
                    result.Add("<u>Order</u> Type: <b>" + lastOrder.Type + "</b> - Price: <b>" +
                        lastOrder.Price + "</b> - Volume: <b>" + lastOrder.Volume + "</b> - <u>Tot order</u>: <b>" + orderNumber + "</b>");
                return Json(result);
            }
        }

        public JsonResult GetTickers()
        {
            using (BrokerDBContext db = new BrokerDBContext())
            {
                var fromTime = DateTime.Now.AddHours(-12).ToEpochTime();
                List<WebTickers> result = new List<WebTickers>();
                var tickers = db.MyTickers.Where(s => s.Timestamp >= fromTime);
                var tickGroup = tickers.GroupBy(s => s.Timestamp.ToDateTime().ToString("dd/MM/yyyy HH:mm:00"));
                foreach (var tick in tickGroup)
                    result.Add(new WebTickers()
                    {
                        Data = DateTime.ParseExact(tick.Key, "dd/MM/yyyy HH:mm:ss", null),
                        Price = tick.OrderByDescending(s => s.Timestamp).First().LastTrade
                    });
                return Json(result);
            }
        }

        public JsonResult GetCandles()
        {
            using (BrokerDBContext db = new BrokerDBContext())
            {
                var fromTime = DateTime.Now.AddDays(-3);
                List<MyCandle> candles = db.MyCandles
                    .Where(s => s.Date >= fromTime)
                    .OrderBy(s => s.Date).ToList();
                WebCandles result = new WebCandles();
                MyOrder lastOrder = db.MyOrders
                    .Where(s => s.Type == TradeAction.Long)
                    .OrderByDescending(s => s.Completed).FirstOrDefault();
                result.Candles = new List<WebCandles.Items>();
                if (candles.Count > 0)
                {
                    decimal lastBuy, lastSell, high = candles.Max(s => s.High);
                    if (lastOrder != null) if (lastOrder.Price > high) high = lastOrder.Price;
                    result.Stoploss = Models.Utility.CalculateStopLoss(high);
                    result.BuyAtUp = Models.Utility.CalculateBuyAtUp(out lastBuy, out lastSell);
                    result.LastBuy = lastBuy; result.LastSell = lastSell;
                    foreach (var candle in candles)
                        result.Candles.Add(new WebCandles.Items()
                        {
                            Data = candle.Date,
                            Price = candle.Close
                        });
                }
                return Json(result);
            }
        }

        public JsonResult GetMACDs()
        {
            using (BrokerDBContext db = new BrokerDBContext())
            {
                var fromTime = DateTime.Now.AddDays(-3);
                List<MyMACD> macds = db.MyMACDs.Where(s => s.Timestamp >= fromTime.ToEpochTime()).ToList();
                List<MyCandle> candles = db.MyCandles.Where(s => s.Date >= fromTime).ToList();
                List<WebMACDs> result = new List<WebMACDs>();
                foreach (var macd in macds)
                {
                    DateTime macdTime = macd.Timestamp.ToDateTime();
                    MyCandle candle = candles.Where(s =>
                        s.Date >= macdTime.AddSeconds(-10) && s.Date > macdTime.AddSeconds(-10)).FirstOrDefault();
                    decimal close = (candle == null) ? 0 : candle.Close;
                    result.Add(new WebMACDs()
                    {
                        Data = macd.Timestamp.ToDateTime(),
                        MACD = macd.MACD,
                        Signal = macd.SignalValue,
                        Close = close
                    });
                }
                return Json(result);
            }
        }

        public JsonResult GetEMAs()
        {
            using (BrokerDBContext db = new BrokerDBContext())
            {
                var fromTime = DateTime.Now.AddDays(-3);
                List<MyMACD> emas = db.MyMACDs.Where(s => s.Timestamp >= fromTime.ToEpochTime()).ToList();
                List<MyCandle> candles = db.MyCandles.Where(s => s.Date >= fromTime).ToList();
                List<WebMACDs> result = new List<WebMACDs>();
                foreach (var ema in emas)
                {
                    DateTime macdTime = ema.Timestamp.ToDateTime();
                    MyCandle candle = candles.Where(s =>
                        s.Date >= macdTime.AddSeconds(-10) && s.Date > macdTime.AddSeconds(-10)).FirstOrDefault();
                    decimal close = (candle == null) ? 0 : candle.Close;
                    result.Add(new WebMACDs()
                    {
                        Data = ema.Timestamp.ToDateTime(),
                        MACD = ema.FastValue,
                        Signal = ema.SlowValue,
                        Close = close
                    });
                }
                return Json(result);
            }
        }

        public JsonResult GetMomentums()
        {
            using (BrokerDBContext db = new BrokerDBContext())
            {
                var fromTime = DateTime.Now.AddDays(-3);
                List<MyMomentum> momentum = db.MyMomentums.Where(s => s.Timestamp >= fromTime.ToEpochTime()).ToList();
                List<MyCandle> candles = db.MyCandles.Where(s => s.Date >= fromTime).ToList();
                List<WebMACDs> result = new List<WebMACDs>();
                foreach (var mom in momentum)
                {
                    DateTime macdTime = mom.Timestamp.ToDateTime();
                    MyCandle candle = candles.Where(s =>
                        s.Date >= macdTime.AddSeconds(-10) && s.Date > macdTime.AddSeconds(-10)).FirstOrDefault();
                    decimal close = (candle == null) ? 0 : candle.Close;
                    result.Add(new WebMACDs()
                    {
                        Data = mom.Timestamp.ToDateTime(),
                        MACD = mom.MomentumValue,
                        Close = close
                    });
                }
                return Json(result);
            }
        }

        public JsonResult GetRSIs()
        {
            using (BrokerDBContext db = new BrokerDBContext())
            {
                var fromTime = DateTime.Now.AddDays(-3);
                List<MyRSI> rsi = db.MyRSIs.Where(s => s.Timestamp >= fromTime.ToEpochTime()).ToList();
                List<MyCandle> candles = db.MyCandles.Where(s => s.Date >= fromTime).ToList();
                List<WebMACDs> result = new List<WebMACDs>();
                foreach (var mom in rsi)
                {
                    DateTime macdTime = mom.Timestamp.ToDateTime();
                    MyCandle candle = candles.Where(s =>
                        s.Date >= macdTime.AddSeconds(-10) && s.Date > macdTime.AddSeconds(-10)).FirstOrDefault();
                    decimal close = (candle == null) ? 0 : candle.Close;
                    result.Add(new WebMACDs()
                    {
                        Data = mom.Timestamp.ToDateTime(),
                        MACD = mom.RSIValue,
                        Close = close
                    });
                }
                return Json(result);
            }
        }

        public JsonResult GetBalance()
        {
            using (BrokerDBContext db = new BrokerDBContext())
            {
                var fromTime = DateTime.Now.AddDays(-5);
                var balances = db.MyBalances.Where(s => s.Date >= fromTime);
                var balanceGroup = balances.GroupBy(s => s.Date.ToString("dd/MM/yyyy HH:mm:00"));
                List<WebTickers> result = new List<WebTickers>();
                foreach (var balance in balanceGroup)
                    result.Add(new WebTickers()
                    {
                        Data = DateTime.ParseExact(balance.Key, "dd/MM/yyyy HH:mm:ss", null),
                        Price = balance.Sum(s => s.ToEuro)
                    });
                return Json(result);
            }
        }

        public JsonResult GetOrder()
        {
            using (BrokerDBContext db = new BrokerDBContext())
            {
                List<MyOrder> orders = db.MyOrders.OrderBy(s => s.Completed).ToList();
                List<WebTickers> result = new List<WebTickers>();
                decimal progression = 0;

                // origin point
                if (orders.Count > 0)
                {
                    result.Add(new WebTickers()
                    {
                        Data = orders.First().Completed.ToDateTime().AddHours(-1),
                        Price = 0
                    });
                }

                // orders
                for (int i = 1; i < orders.Count; i++)
                {
                    var order = orders[i];
                    if (order.Type == TradeAction.Short)
                    {
                        progression += ((order.Price * order.Volume) - (orders[i - 1].Price * order.Volume));
                        result.Add(new WebTickers()
                        {
                            Data = order.Completed.ToDateTime(),
                            Price = progression
                        });
                    }
                }

                // return
                return Json(result);
            }
        }

        public JsonResult GetLogs()
        {
            string pathExposed = Path.Combine(AppContext.BaseDirectory, "Exposed");
            List<string> log = new List<string>();
            log = System.IO.File.ReadLines(
                Path.Combine(pathExposed, "broker.log"))
                .Reverse().Take(100).ToList();
            return Json(log);
        }

        // errors
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

    }
}
