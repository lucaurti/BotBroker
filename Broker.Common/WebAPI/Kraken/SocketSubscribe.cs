using System.Collections.Generic;

namespace Broker.Common.WebAPI.Kraken
{

    public class Subscription
    {
        public string name { get; set; }
    }

    public class SocketSubscribe
    {
        public string @event { get; set; }
        public List<string> pair { get; set; }
        public Subscription subscription { get; set; }
    }

    public class SocketSubscribeStatus
    {
        public string @event { get; set; }
        public string pair { get; set; }
        public Subscription subscription { get; set; }
        public int channelID { get; set; }
        public string channelName { get; set; }
        public string status { get; set; }
    }

}
