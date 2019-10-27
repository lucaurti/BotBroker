namespace Broker.Common.Indicators
{

    public class EMAAverage
    {
        private int tickcount, periods;
        private decimal dampen, emav;

        public EMAAverage(int pPeriods)
        {
            periods = pPeriods;
            dampen  = 2 / ((decimal)1.0 + periods);
        }

        public void ReceiveTick(decimal Val)
        {
            if (tickcount < periods)
                emav += Val;
            if (tickcount == periods)
                emav /= periods;
            if (tickcount > periods)
                emav = (dampen * (Val - emav)) + emav;
            if (tickcount <= (periods + 1))
                tickcount++;
        }
        public decimal Value()
        {
            return isPrimed() ? emav : 0;
        }
        public bool isPrimed()
        {    
            return tickcount > periods;
        }
        public int PeriodElaborated() 
        {
            return tickcount;
        }

    }

}