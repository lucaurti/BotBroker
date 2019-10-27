using Broker.Common.Utility;

namespace Broker.Common.Events
{
    public class MyWebSocket : IWebSocket
    {

        // handler
        public event MySocketTickerEventHandler onSocketTickerUpdate;


        // events
        public void OnSocketTickerUpdate(MyTicker myTicker)
        {
            onSocketTickerUpdate?.Invoke(myTicker);
        }

    }
}
