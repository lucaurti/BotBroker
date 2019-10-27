using System.Collections.Generic;
using Broker.Common.WebAPI.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Broker.Common.WebAPI.Kraken.Balances
{

    internal class Balance
    {
        public List<string> error { get; set; }
        public List<Result> result { get; set; }

        public string ToCorrectJson(MyWebAPISettings settings, string json)
        {
            var obj1 = JObject.Parse(json).First;
            var obj = JObject.Parse(json)["result"];
            List<KeyValuePair<string,string>> lista = new List<KeyValuePair<string, string>>(); 
            foreach (JToken item in obj.Children())
            {
                string[] keyValue= item.ToString().Split(':');
                lista.Add(new KeyValuePair<string, string>(keyValue[0].Replace("\"","").Trim(),keyValue[1].Replace("\"","").Trim()));
            }
            string s = JsonConvert.SerializeObject(lista);
            string str = "{"+obj1.ToString()+",\"result\": "+s+"}";
            return str;
        }
    }

    internal class Result
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

}
