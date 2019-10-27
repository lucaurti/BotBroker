using System;
using System.Collections.Generic;

namespace Broker.Batch.Models
{
    public class WebCandles
    {

        public class Items 
        {
            public DateTime Data { get; set; }
            public decimal Price { get; set; }
        }

        public List<Items> Candles { get; set; }
        public decimal Stoploss { get; set; }
        public decimal BuyAtUp { get; set; }
        public decimal LastBuy { get; set; }
        public decimal LastSell { get; set; }

        
    }
}