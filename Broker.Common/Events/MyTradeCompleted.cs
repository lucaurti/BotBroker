using System;
using Broker.Common.WebAPI.Models;
using static Broker.Common.Strategies.Enumerator;

namespace Broker.Common.Events
{

    public class MyTradeCompleted
    {

        public Guid Id { get; set; } = Guid.NewGuid();
        public string OrderId { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public TradeAction Action { get; set; } = TradeAction.Long;
        public decimal Price { get; set; } = 0;
        public decimal Amount { get; set; } = 0;
        public decimal Cost { get; set; } = 0;
        public decimal Balance { get; set; } = 0;
        public decimal EffectivePrice { get; set; } = 0;
        public decimal? LimitPrice { get; set; } = 0;
        public int? Percentage { get; set; } = 0;
        public MyWebAPISettings Settings { get; set; }

    }

}
