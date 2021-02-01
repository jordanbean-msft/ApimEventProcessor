using System;

namespace ApimEventProcessor.Helpers
{
    // Environment varilables for configuring Moesif
    public static class MoesifAppParamNames
    {
        public const string APP_ID = "APIMEVENTS-MOESIF-APPLICATION-ID";
        public const string SESSION_TOKEN = "APIMEVENTS-MOESIF-SESSION-TOKEN";
        public const string API_VERSION = "APIMEVENTS-MOESIF-API-VERSION";
    }

    // Environment varilables for configuring Azure Eventhub and storage account
    public static class AzureAppParamNames
    {
        public const string EVENTHUB_CONN_STRING = "APIMEVENTS-EVENTHUB-CONNECTIONSTRING";
        public const string EVENTHUB_NAME = "APIMEVENTS-EVENTHUB-NAME";
        public const string STORAGEACCOUNT_NAME = "APIMEVENTS-STORAGEACCOUNT-NAME";
        public const string STORAGEACCOUNT_KEY = "APIMEVENTS-STORAGEACCOUNT-KEY";
    }

    public static class RunParams
    {
        // Frequency at which events are checkpointed to Azure Storage.
        public const int CHECKPOINT_MINIMUM_INTERVAL_MINUTES = 5;
        
        // Frequency at which Moesif configuration is fetched.
        public const int CONFIG_FETCH_INTERVAL_MINUTES = 5;
    }    class ParamConfig
    {
        public static string load(string v)
        {
            return Environment.GetEnvironmentVariable(v,
                    EnvironmentVariableTarget.Process);
        }

        public static string loadDefaultEmpty(string v)
        {
            var val = load(v);
            if (string.IsNullOrWhiteSpace(val))
                val = "";
            return val.Trim();
        }

        public static string loadNonEmpty(string varName)
        {
            string val = loadDefaultEmpty(varName);
            if (string.IsNullOrWhiteSpace(val))
                throw new ArgumentException("Required parameter not found: " + varName);
            return val.Trim();
        }
    }

}