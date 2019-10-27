namespace Broker.Common.Indicators
{

    public class RSIAverage
    {
        private int tickcount, periods;
        private decimal emav, avrg, avrl;
        private decimal pricePrec;

        public RSIAverage(int pPeriods)
        {
            periods = pPeriods;
            avrg = 0; avrl = 0; pricePrec = 0;
        }

        public void ReceiveTick(decimal Val)
        {
            if (tickcount > periods)
            {
                decimal diff = Val - pricePrec;
                if (diff >= 0)
                {
                    avrg = ((avrg * (periods - 1)) + diff) / periods;
                    avrl = (avrl * (periods - 1)) / periods;
                }
                else
                {
                    avrl = ((avrl * (periods - 1)) - diff) / periods;
                    avrg = (avrg * (periods - 1)) / periods;
                }
                if (avrl != 0)
                    emav = 100 - (100 / (1 + (avrg / avrl)));
            }
            else
                tickcount++;

            pricePrec = Val;
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