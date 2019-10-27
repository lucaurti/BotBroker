using System.Collections.Generic;
using Broker.Common.WebAPI.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Broker.Common.WebAPI.Kraken.OpenedOrders
{
    public class Descr
    {
        public string pair { get; set; }
        public string type { get; set; }
        public string ordertype { get; set; }
        public string price { get; set; }
        public string price2 { get; set; }
        public string leverage { get; set; }
        public string order { get; set; }
        public string close { get; set; }
    }

    public class Order
    {
        public string id { get; set; }
        public object refid { get; set; }
        public int userref { get; set; }
        public string status { get; set; }
        public double opentm { get; set; }
        public int starttm { get; set; }
        public int expiretm { get; set; }
        public Descr descr { get; set; }
        public string vol { get; set; }
        public string vol_exec { get; set; }
        public string cost { get; set; }
        public string fee { get; set; }
        public string price { get; set; }
        public string stopprice { get; set; }
        public string limitprice { get; set; }
        public string misc { get; set; }
        public string oflags { get; set; }
    }

    public class Open
    {
        public List<Order> orders { get; set; }
    }

    public class Result
    {
        public Open open { get; set; }
    }

    public class OpenedOrder
    {
        public List<object> error { get; set; }
        public Result result { get; set; }

        public string ToCorrectJson(MyWebAPISettings settings, string json)
        {
            var error = JObject.Parse(json).First;
            var result = JObject.Parse(json)["result"];
            List<string> orderOpenList = new List<string>(); 
            foreach (JToken open in result.Values())
            {
                foreach (JToken singleOrder in open.Children())
                {
                    List<string> lista = new List<string>(); 
                    string[] keyValue1 = singleOrder.ToString().Split(':');
                    lista.Add("'id': '" + (keyValue1[0].Replace("{","").Replace("\"","").Replace("\n ","")).Trim()+"'");
                    foreach (var item in singleOrder.Values())
                    {
                        lista.Add(item.ToString().Replace("\"","'"));
                    }
                    string ss = JsonConvert.SerializeObject(lista).Replace("[","{").Replace("]","}");
                    ss= ss.Replace("\"","").Replace("\\n","");
                    orderOpenList.Add(ss);
                }
            }
            string s = JsonConvert.SerializeObject(orderOpenList);
            s= s.Replace("\"","").Replace("\\n","");
            s = "{'orders':"+s+"}";
            string str = "{"+error.ToString()+",'result': { 'open': "+s+"}}";
            str= str.Replace("\"","").Replace("\\n","");
            return str;
        }
    }
}