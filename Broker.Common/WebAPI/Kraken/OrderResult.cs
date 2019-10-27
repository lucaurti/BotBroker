using System.Collections.Generic;

namespace Broker.Common.WebAPI.Kraken
{
    public class Descr
    {
        public string order { get; set; }
    }

    public class Result
    {
        public Descr descr { get; set; }
        public List<string> txid { get; set; }
    }

    public class OrderResult
    {
        public List<object> error { get; set; }
        public Result result { get; set; }
    }
}