using System.Collections.Generic;

namespace Broker.Common.WebAPI.Kraken
{
    public class Order
    {
        //Asset pair
        public string Pair { get; set; }
        //Type of order (buy or sell)
        public string Type { get; set; }
        //Execution type
        public string OrderType { get; set; }
        //Price. Optional. Dependent upon order type
        public string Price { get; set; }
        //Secondary price. Optional. Dependent upon order type
        public string Price2 { get; set; }
        //Order volume in lots
        public string Volume { get; set; }
        //Amount of leverage required. Optional. default none
        public string Leverage { get; set; }
        //Position tx id to close (optional.  used to close positions)
        public string Position { get; set; }
        //list of order flags (optional):
        public string OFlags { get; set; }
        //Scheduled start time. Optional
        public string Starttm { get; set; }
        //Expiration time. Optional
        public string Expiretm { get; set; }
        //User ref id. Optional
        public string Userref { get; set; }
        //Validate inputs only. do not submit order. Optional
        public bool Validate { get; set; }
        //Closing order details
        public Dictionary<string, string> Close { get; set; }

        public Order(string pair, string type, string ordertype, decimal volume, decimal? price, decimal? price2 = null, 
            string leverage = "none", string position = "",  string oflags = "", string starttm = "", string expiretm = "", 
            string userref = "",  bool validate = false,  Dictionary<string, string> close = null)
            {
                this.Pair = pair;
                this.Type = type;
                this.OrderType = ordertype;
                this.Volume = volume.ToString().Replace(",",".");
                this.Price = price.HasValue ? price.ToString().Replace(",","."): null;
                this.Price2 = price2.HasValue ? price2.ToString().Replace(",","."): null;
                this.Leverage =leverage;
                this.Position =position;
                this.OFlags =oflags;
                this.Starttm = starttm;
                this.Expiretm =expiretm;
                this.Userref =userref;
                this.Validate = validate;
                this.Close =close;
            }
    }
}
