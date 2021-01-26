using Moesif.Api;
using Moesif.Api.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using ApimEventProcessor.Helpers;
using System.Threading;


namespace ApimEventProcessor
{
    public class MoesifHttpMessageProcessor : IHttpMessageProcessor
    {
        private readonly string RequestTimeName = "MoRequestTime";
        private readonly string ReqHeadersName = "ReqHeaders";
        private readonly string UserIdName = "UserId";
        private readonly string CompanyIdName = "CompanyId";
        private readonly string MetadataName = "Metadata";
        private MoesifApiClient _MoesifClient;
        private ILogger _Logger;
        private string _SessionTokenKey;
        private string _ApiVersion;
        // Initialize config dictionary
        private AppConfig appConfig = new AppConfig();
        // Initialized config response
        private Moesif.Api.Http.Response.HttpStringResponse config;
        // App Config samplingPercentage
        private int samplingPercentage;
        // App Config configETag
        private string configETag;
        // App Config lastUpdatedTime
        private DateTime lastUpdatedTime;
        private DateTime lastWorkerRun = DateTime.MinValue;
        ConcurrentDictionary<Guid, HttpMessage> requestsCache = new ConcurrentDictionary<Guid, HttpMessage>();
        ConcurrentDictionary<Guid, HttpMessage> responsesCache = new ConcurrentDictionary<Guid, HttpMessage>();
        private readonly object qLock = new object();
        
        public MoesifHttpMessageProcessor(ILogger logger)
        {
            var appId = Environment.GetEnvironmentVariable("APIMEVENTS-MOESIF-APPLICATION-ID", EnvironmentVariableTarget.Process);
            _MoesifClient = new MoesifApiClient(appId);
            _SessionTokenKey = Environment.GetEnvironmentVariable("APIMEVENTS-MOESIF-SESSION-TOKEN", EnvironmentVariableTarget.Process);
            _ApiVersion = Environment.GetEnvironmentVariable("APIMEVENTS-MOESIF-API-VERSION", EnvironmentVariableTarget.Process);
            _Logger = logger;
            ScheduleWorkerToFetchConfig();
        }

        private void ScheduleWorkerToFetchConfig()
        {
            try {
                new Thread(async () =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    lastWorkerRun = DateTime.UtcNow;
                    // Get Application config
                    config = await appConfig.getConfig(_MoesifClient, _Logger);
                    if (!string.IsNullOrEmpty(config.ToString()))
                    {
                        (configETag, samplingPercentage, lastUpdatedTime) = appConfig.parseConfiguration(config, _Logger);
                    }
                }).Start();
            } catch (Exception ex) {
                _Logger.LogError("Error while parsing application configuration on initialization - " + ex.ToString());
            }
        }

        public async Task ProcessHttpMessage(HttpMessage message)
        {
            // message will probably contain either HttpRequestMessage or HttpResponseMessage
            // So we cache both request and response and cache them. 
            // Note, response message might be processed before request
            if (message.HttpRequestMessage != null){
                _Logger.LogDebug("Received req: " + message.MessageId);
                message.HttpRequestMessage.Properties.Add(RequestTimeName, DateTime.UtcNow);
                requestsCache.TryAdd(message.MessageId, message);
            }
            if (message.HttpResponseMessage != null){
                _Logger.LogDebug("Received resp: " + message.MessageId);
                responsesCache.TryAdd(message.MessageId, message);
            }
            await SendCompletedMessagesToMoesif();
        }

        /*
        From requestCache and responseCache, find all messages that have request and response
        Send them to Moesif asynchronously.
        */
        public async Task SendCompletedMessagesToMoesif()
        {
            var completedMessages = RemoveCompletedMessages();
            _Logger.LogDebug("Sending completed Messages to Moesif. Count: " + completedMessages.Count);
            if (completedMessages.Count > 0)
            {
                var moesifEvents = await BuildMoesifEvents(completedMessages);
                // Send async to Moesif. To send synchronously, use CreateEventsBatch instead
                await _MoesifClient.Api.CreateEventsBatchAsync(moesifEvents);
            }
        }

        public Dictionary<Guid, KeyValuePair<HttpMessage, HttpMessage>> RemoveCompletedMessages(){
            Dictionary<Guid, KeyValuePair<HttpMessage, HttpMessage>> messages = new Dictionary<Guid, KeyValuePair<HttpMessage, HttpMessage>>();
            lock(qLock){
                foreach(Guid messageId in requestsCache.Keys.Intersect(responsesCache.Keys))
                {
                    HttpMessage reqm, respm;
                    requestsCache.TryRemove(messageId, out reqm);
                    responsesCache.TryRemove(messageId, out respm);
                    messages.Add(messageId, new KeyValuePair<HttpMessage, HttpMessage>(reqm, respm));
                }
            }
            return messages;
        }

        public async Task<List<EventModel>> BuildMoesifEvents(Dictionary<Guid, KeyValuePair<HttpMessage, HttpMessage>> completedMessages)
        {
            List<EventModel> events = new List<EventModel>();
            foreach(KeyValuePair<HttpMessage, HttpMessage> kv in completedMessages.Values)
            {
                var moesifEvent = await BuildMoesifEvent(kv.Key, kv.Value);
                try {
                    // Get Sampling percentage
                    samplingPercentage = appConfig.getSamplingPercentage(config, moesifEvent.UserId, moesifEvent.CompanyId);
                    Random random = new Random();
                    double randomPercentage = random.NextDouble() * 100;
                    if (samplingPercentage >= randomPercentage)
                    {
                        moesifEvent.Weight = appConfig.calculateWeight(samplingPercentage);
                        // Add event to the batch
                        events.Add(moesifEvent);
                        if (lastWorkerRun.AddMinutes(5) < DateTime.UtcNow)
                        {
                            _Logger.LogDebug("Scheduling worker thread. lastWorkerRun=" + lastWorkerRun.ToString());
                            ScheduleWorkerToFetchConfig();
                        }
                    }
                    else
                    {
                        _Logger.LogDebug("Skipped Event due to sampling percentage: " + samplingPercentage.ToString() + " and random percentage: " + randomPercentage.ToString());
                    }
                } catch (Exception ex) {
                    _Logger.LogError("Error adding event to the batch - " + ex.ToString());
                }
            }
            return events;
        }

        /**
        From Http request and response, construct the moesif EventModel
        */
        public async Task<EventModel> BuildMoesifEvent(HttpMessage request, HttpMessage response){
            _Logger.LogDebug("Building Moesif event");
            EventRequestModel moesifRequest = new EventRequestModel()
            {
                Time = (DateTime) request.HttpRequestMessage.Properties[RequestTimeName],
                Uri = request.HttpRequestMessage.RequestUri.OriginalString,
                Verb = request.HttpRequestMessage.Method.ToString(),
                Headers = request.HttpRequestMessage.Properties[ReqHeadersName] != null ? ToRequestHeaders((string) request.HttpRequestMessage.Properties[ReqHeadersName]) : new Dictionary<string, string>(),
                ApiVersion = _ApiVersion,
                IpAddress = null,
                Body = request.HttpRequestMessage.Content != null ? await request.HttpRequestMessage.Content.ReadAsStringAsync() : null,
                TransferEncoding = "base64"
            };

            EventResponseModel moesifResponse = new EventResponseModel()
            {
                Time = DateTime.UtcNow,
                Status = (int) response.HttpResponseMessage.StatusCode,
                IpAddress = Environment.MachineName,
                Headers = ToResponseHeaders(response.HttpResponseMessage.Headers, response.HttpResponseMessage.Content.Headers),
                Body = response.HttpResponseMessage.Content != null ? await response.HttpResponseMessage.Content.ReadAsStringAsync() : null,
                TransferEncoding = "base64"
            };

            Dictionary<string, object> metadata = new Dictionary<string, object>();
            metadata = request.HttpRequestMessage.Properties[MetadataName] != null ? (Dictionary<string, object>) request.HttpRequestMessage.Properties[MetadataName] : metadata;
            metadata.Add("ApimMessageId", request.MessageId.ToString());

            String skey = null;
            if((_SessionTokenKey != null) && (request.HttpRequestMessage.Headers.Contains(_SessionTokenKey)) )
                skey = request.HttpRequestMessage.Headers.GetValues(_SessionTokenKey).FirstOrDefault();

            // UserId
            string userId = request.HttpRequestMessage.Properties[UserIdName] != null ? (string) request.HttpRequestMessage.Properties[UserIdName] : null;

            // Company Id
            string companyId = request.HttpRequestMessage.Properties[CompanyIdName] != null ? (string) request.HttpRequestMessage.Properties[CompanyIdName] : null;
            
            EventModel moesifEvent = new EventModel()
            {
                Request = moesifRequest,
                Response = moesifResponse,
                SessionToken = skey,
                Tags = null,
                UserId = userId,
                CompanyId = companyId,
                Metadata = metadata,
                Direction = "Incoming"
            };
            return moesifEvent;
        }

        private static Dictionary<string, string> ToRequestHeaders(string headerString) 
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            string[] splitHeaders = headerString.Split(new string[] { ";;" }, StringSplitOptions.None);

            foreach (var h in splitHeaders)
            {
                if (!string.IsNullOrEmpty(h)) {
                    var kv = h.Trim().Split(':');
                    headers.Add(kv[0], kv[1].Trim());   
                }
            }
            return headers;
        }

        private static Dictionary<string, string> ToResponseHeaders(HttpHeaders headers, HttpContentHeaders contentHeaders)
        {
            IEnumerable<KeyValuePair<string, IEnumerable<string>>> enumerable = headers.GetEnumerator().ToEnumerable();
            Dictionary<string, string> responseHeaders = enumerable.ToDictionary(p => p.Key, p => p.Value.GetEnumerator()
                                                             .ToEnumerable()
                                                             .ToList()
                                                             .Aggregate((i, j) => i + ", " + j));
            
            IEnumerable<KeyValuePair<string, IEnumerable<string>>> enumerableContent = contentHeaders.GetEnumerator().ToEnumerable();
            Dictionary<string, string> responseContentHeaders = enumerableContent.ToDictionary(p => p.Key, p => p.Value.GetEnumerator()
                                                             .ToEnumerable()
                                                             .ToList()
                                                             .Aggregate((i, j) => i + ", " + j));
            
            return responseHeaders.Concat(responseContentHeaders.Where( x=> !responseHeaders.Keys.Contains(x.Key))).ToDictionary(k => k.Key, v => v.Value);
        }
    }
}
