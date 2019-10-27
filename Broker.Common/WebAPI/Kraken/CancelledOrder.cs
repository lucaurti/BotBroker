using System.Collections.Generic;

namespace Broker.Common.WebAPI.Kraken.CancelledOrders
{
    public class Result
    {
        public int count { get; set; }
    }

    public class CancelledOrder
    {
        public List<object> error { get; set; }
        public Result result { get; set; }
    }
}