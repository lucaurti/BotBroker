using System.Collections.Generic;

namespace Broker.Common.WebAPI.Binance
{

    internal class RateLimit
    {
        public string rateLimitType { get; set; }
        public string interval { get; set; }
        public int intervalNum { get; set; }
        public int limit { get; set; }
    }

    internal class Filter
    {
        public string filterType { get; set; }
        public string minPrice { get; set; }
        public string maxPrice { get; set; }
        public string tickSize { get; set; }
        public string multiplierUp { get; set; }
        public string multiplierDown { get; set; }
        public int? avgPriceMins { get; set; }
        public string minQty { get; set; }
        public string maxQty { get; set; }
        public string stepSize { get; set; }
        public string minNotional { get; set; }
        public bool? applyToMarket { get; set; }
        public int? limit { get; set; }
        public int? maxNumAlgoOrders { get; set; }
    }

    internal class Symbol
    {
        public string symbol { get; set; }
        public string status { get; set; }
        public string baseAsset { get; set; }
        public int baseAssetPrecision { get; set; }
        public string quoteAsset { get; set; }
        public int quotePrecision { get; set; }
        public IList<string> orderTypes { get; set; }
        public bool icebergAllowed { get; set; }
        public bool isSpotTradingAllowed { get; set; }
        public bool isMarginTradingAllowed { get; set; }
        public IList<Filter> filters { get; set; }
    }

    internal class ExchangeInfo
    {
        public string timezone { get; set; }
        public long serverTime { get; set; }
        public IList<RateLimit> rateLimits { get; set; }
        public IList<Filter> exchangeFilters { get; set; }
        public IList<Symbol> symbols { get; set; }
    }

}
