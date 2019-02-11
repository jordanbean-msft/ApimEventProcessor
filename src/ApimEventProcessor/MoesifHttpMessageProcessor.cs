using Moesif.Api;
using Moesif.Api.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace ApimEventProcessor
{
    public class MoesifHttpMessageProcessor : IHttpMessageProcessor
    {
        private readonly string RequestTimeName = "MoRequestTime";
        private MoesifApiClient _MoesifClient;
        private ILogger _Logger;
        private string _SessionTokenKey;
        private string _ApiVersion;
        public MoesifHttpMessageProcessor(ILogger logger)
        {
            var appId = Environment.GetEnvironmentVariable("APIMEVENTS-MOESIF-APP-ID", EnvironmentVariableTarget.Process);
            var client = new MoesifApiClient(appId);
            _SessionTokenKey = Environment.GetEnvironmentVariable("APIMEVENTS-MOESIF-SESSION-TOKEN", EnvironmentVariableTarget.Process);
            _SessionTokenKey = Environment.GetEnvironmentVariable("APIMEVENTS-MOESIF-API-VERSION", EnvironmentVariableTarget.Process);
            _Logger = logger;
        }

        public async Task ProcessHttpMessage(HttpMessage message)
        {
            if (message.IsRequest)
            {
                message.HttpRequestMessage.Properties.Add(RequestTimeName, DateTime.UtcNow);
                return;
            }
            
            EventRequestModel moesifRequest = new EventRequestModel()
            {
                Time = (DateTime) message.HttpRequestMessage.Properties[RequestTimeName],
                Uri = message.HttpRequestMessage.RequestUri.OriginalString,
                Verb = message.HttpRequestMessage.Method.ToString(),
                Headers = ToHeaders(message.HttpRequestMessage.Headers),
                ApiVersion = _ApiVersion,
                IpAddress = null,
                Body = System.Convert.ToBase64String(await message.HttpRequestMessage.Content.ReadAsByteArrayAsync()),
                TransferEncoding = "base64"
            };

            EventResponseModel moesifResponse = new EventResponseModel()
            {
                Time = DateTime.UtcNow,
                Status = (int) message.HttpResponseMessage.StatusCode,
                IpAddress = Environment.MachineName,
                Headers = ToHeaders(message.HttpResponseMessage.Headers),
                Body = System.Convert.ToBase64String(await message.HttpResponseMessage.Content.ReadAsByteArrayAsync()),
                TransferEncoding = "base64"
            };

            Dictionary<string, string> metadata = new Dictionary<string, string>();
            metadata.Add("ApimMessageId", message.MessageId.ToString());

            EventModel moesifEvent = new EventModel()
            {
                Request = moesifRequest,
                Response = moesifResponse,
                SessionToken = message.HttpRequestMessage.Headers.GetValues(_SessionTokenKey).FirstOrDefault(),
                Tags = null,
                UserId = null,
                Metadata = metadata
            };

            Dictionary<string, string> response = await _MoesifClient.Api.CreateEventAsync(moesifEvent);

            _Logger.LogDebug("Message forwarded to Moesif");
        }

        private static Dictionary<string, string> ToHeaders(HttpHeaders headers)
        {
            IEnumerable<KeyValuePair<string, IEnumerable<string>>> enumerable = headers.GetEnumerator().ToEnumerable();
            return enumerable.ToDictionary(p => p.Key, p => p.Value.GetEnumerator()
                                                             .ToEnumerable()
                                                             .ToList()
                                                             .Aggregate((i, j) => i + ", " + j));
        }
    }
}
