using System;
using System.Linq;
using System.Collections.Generic;
using Moesif.Api;
using Moesif.Api.Exceptions;
using System.Threading.Tasks;

namespace ApimEventProcessor.Helpers
{
    public class AppConfig
    {
        public async Task<Moesif.Api.Http.Response.HttpStringResponse> getConfig(MoesifApiClient client, ILogger logger)
        {
            // Get Config
            Moesif.Api.Http.Response.HttpStringResponse appConfig;
            try
            {
                appConfig = await client.Api.GetAppConfigAsync();
                logger.LogDebug("Application Config fetched Successfully");
            }
            catch (APIException inst)
            {
                appConfig = null;
                if (401 <= inst.ResponseCode && inst.ResponseCode <= 403)
                {
                    logger.LogError("Unauthorized access getting application configuration. Please check your Appplication Id.");
                }
            }
            return appConfig;
        }

        public (String, int, DateTime) parseConfiguration(Moesif.Api.Http.Response.HttpStringResponse config, ILogger logger)
        {
            // Parse configuration object and return Etag, sample rate and last updated time
            try
            {
                var rspBody = ApiHelper.JsonDeserialize<Dictionary<string, object>>(config.Body);
                return (config.Headers.ToDictionary(k => k.Key.ToLower(), k => k.Value)["x-moesif-config-etag"], Int32.Parse(rspBody["sample_rate"].ToString()), DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                logger.LogError("Error while parsing the configuration object, setting the sample rate to default.");
                return (null, 100, DateTime.UtcNow);
            }
        }

        public async Task<(Moesif.Api.Http.Response.HttpStringResponse, string, int, DateTime)> GetAppConfig(string configETag, int samplingPercentage, DateTime lastUpdatedTime, MoesifApiClient client, ILogger logger)
        {
            Moesif.Api.Http.Response.HttpStringResponse config = null;
            try
            {
                // Get Application config
                config = await getConfig(client, logger);
                if (!string.IsNullOrEmpty(config.ToString()))
                {
                    (configETag, samplingPercentage, lastUpdatedTime) = parseConfiguration(config, logger);
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Error while parsing application configuration");
            }
            return (config, configETag, samplingPercentage, lastUpdatedTime);
        }

        public int getSamplingPercentage(Moesif.Api.Http.Response.HttpStringResponse config, string userId, string companyId)
        {
            // Get sampling percentage
            if (config == null)
            {
                return 100;
            }

            var userDefaultRate = new object();

            var companyDefaultRate = new object();

            var defaultRate = new object();

            var configBody = ApiHelper.JsonDeserialize<Dictionary<string, object>>(config.Body);

            Dictionary<string, object> userSampleRate = configBody.TryGetValue("user_sample_rate", out userDefaultRate)
                ? ApiHelper.JsonDeserialize<Dictionary<string, object>>(userDefaultRate.ToString())
                : null;

            Dictionary<string, object> companySampleRate = configBody.TryGetValue("company_sample_rate", out companyDefaultRate)
                ? ApiHelper.JsonDeserialize<Dictionary<string, object>>(companyDefaultRate.ToString())
                : null;

            if (userSampleRate != null && !string.IsNullOrEmpty(userId) && userSampleRate.Count > 0 && userSampleRate.ContainsKey(userId))
            {
                return Int32.Parse(userSampleRate[userId].ToString());
            }

            if (companySampleRate != null && !string.IsNullOrEmpty(companyId) && companySampleRate.Count > 0 && companySampleRate.ContainsKey(companyId))
            {
                return Int32.Parse(companySampleRate[companyId].ToString());
            }

            return configBody.TryGetValue("sample_rate", out defaultRate) ? Int32.Parse(defaultRate.ToString()) : 100;
        }

        public int calculateWeight(int sampleRate)
        {
            return (int)(sampleRate == 0 ? 1 : Math.Floor(100.00 / sampleRate));
        }
    }
}
