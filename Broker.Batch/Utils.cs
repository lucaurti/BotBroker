using System;
using System.Text;
using Serilog;

namespace Broker.Batch
{
    public class Utils
    {
        internal static void LogError(Exception ex)
        {
            StringBuilder str = new StringBuilder();
            str.AppendLine(ex.Message);
            str.AppendLine(ex.Source);
            str.AppendLine(ex.StackTrace);
            if (ex.InnerException != null)
            {
                str.AppendLine("InnerException");
                str.AppendLine(ex.InnerException.Message);
                str.AppendLine(ex.InnerException.Source);
                str.AppendLine(ex.InnerException.StackTrace);
            }
            Log.Error(str.ToString());
        }
    }
}
