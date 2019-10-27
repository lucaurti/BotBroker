using System;

namespace Broker.Batch.Models
{
    public class WebMACDs
    {
        public DateTime Data { get; set; }
        public decimal MACD { get; set; }
        public decimal Signal { get; set; }
        public decimal Close { get; set; }
        
    }
}