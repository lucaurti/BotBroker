using Broker.Common.Utility;

namespace Broker.Common.Events
{

    // delegates
    public delegate void MySocketTickerEventHandler(MyTicker myTicker);


    public interface IWebSocket
    {

        // events
        event MySocketTickerEventHandler onSocketTickerUpdate;


        // invokers
        void OnSocketTickerUpdate(MyTicker myTicker);

    }

}
