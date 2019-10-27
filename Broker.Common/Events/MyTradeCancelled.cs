using System;
using Broker.Common.WebAPI.Models;

namespace Broker.Common.Events
{

    public class MyTradeCancelled
    {

        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Date { get; set; } = DateTime.Now;
        public Guid? IdReference { get; set; } = null;
        public string OrderId { get; set; }
        public string Reason { get; set; } = null;
        public MyWebAPISettings Settings { get; set; }

    }

}
