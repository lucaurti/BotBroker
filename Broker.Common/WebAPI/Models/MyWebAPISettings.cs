using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Broker.Common.WebAPI.Models
{

    public class MyWebAPISettings
    {

        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Pair { get { return Asset + Separator + Currency; } }
        public string Asset { get; private set; }
        public string Currency { get; private set; }
        public string Separator { get; private set; }
        public int PrecisionAsset { get; private set; }
        public int PrecisionCurrency { get; private set; }

        public MyWebAPISettings(string Asset, string Currency, string Separator, int PrecisionAsset,int PrecisionCurrency)
        {
            this.Asset = Asset;
            this.Currency = Currency;
            this.PrecisionAsset = PrecisionAsset;
            this.PrecisionCurrency = PrecisionCurrency;
            this.Separator = Separator;
        }

    }

}