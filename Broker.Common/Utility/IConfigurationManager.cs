using System.Collections.Generic;

namespace Broker.Common.Utility
{
    public interface IConfigurationManager
    {

        string GetParameterValue(string parameterName, string sectionName = "configurations");
        List<string> GetParameterValueList(string parameterName, string sectionName = "configurations", string subSectionName = null, int? Index = null);
        bool ExistsParameter(string parameterName, string sectionName = "configurations", bool contains = false);
        string FindParameter(string parameterName, string sectionName = "configurations");
        
        
        bool isDebugLog { get; }
        bool mustStartBatch { get; }
        bool mustStartWeb { get; }
        bool mustStartTelegram { get; }

    }
}