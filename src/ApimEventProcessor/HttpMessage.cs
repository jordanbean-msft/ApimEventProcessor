using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ApimEventProcessor.Helpers;

namespace ApimEventProcessor
{
    public class MoesifRequest
    {
        public MoesifRequest(JObject request)
        {
            eventType = (string) request["event_type"];
            messageId = (string) request["message-id"];
            method = (string) request["method"];
            uri = (string) request["uri"];
            userId = (string) request["user_id"];
            companyId = (string) request["company_id"];
            requestHeaders = (string) request["request_headers"];
            requestBody = (string) request["request_body"];
            metadata = request["metadata"].ToObject<Dictionary<string, object>>();
        }

        public string eventType { get; set; }
        public string messageId { get; set; }
        public string method { get; set; }
        public string uri { get; set; }
        public string userId { get; set; }
        public string companyId { get; set; }
        public string requestHeaders { get; set; }
        public string requestBody { get; set; }
        public Dictionary<string, object> metadata { get; set; }
    }
    public class MoesifResponse
    {
        public MoesifResponse(JObject response)
        {
            eventType = (string) response["event_type"];
            messageId = (string) response["message-id"];
            statusCode = (string) response["status_code"];
            responseHeaders = (string) response["response_headers"];
            responseBody = (string) response["response_body"];
        }

        public string eventType { get; set; }
        public string messageId { get; set; }
        public string statusCode { get; set; }
        public string responseHeaders { get; set; }
        public string responseBody { get; set; }
    }
 
    /// <summary>
    /// Parser for format being sent from APIM logtoeventhub policy that contains a complete HTTP request or response message.
    /// </summary>
    /// <remarks>
    ///     Might want to add a version number property to the format before actually letting it out
    ///     in the wild.
    /// </remarks>
    public class HttpMessage
    {
        public Guid MessageId { get; set; }
        public bool IsRequest { get; set; }
        public HttpRequestMessage HttpRequestMessage { get; set; }
        public HttpResponseMessage HttpResponseMessage { get; set; }


        public static HttpMessage Parse(Stream stream)
        {
            using (var sr = new StreamReader(stream))
            {
                return Parse(sr.ReadToEnd());
            }
        }

        private static void TransformResponseHeaders(HttpResponseMessage response, string headerString) 
        {
            Dictionary<string, string> headers = HeadersUtils.deSerializeHeaders(headerString);
            foreach (var h in headers)
            {
                string n = h.Key.Trim();
                string v = h.Value.Trim();
                if(HeadersUtils.isContentTypeHeader(n))
                {
                    try {
                        response.Content.Headers.ContentType = new MediaTypeHeaderValue(v);
                    }
                    catch (Exception){
                        // Some content headers throw exception eg
                        //The format of value 'text/plain; charset=utf-8' is invalid.
                        response.Headers.TryAddWithoutValidation(n, v);
                    }
                }
                else
                    response.Headers.TryAddWithoutValidation(n, v);
            }
            return ;
        }

        public static HttpMessage Parse(string data)
        {
            var httpMessage = new HttpMessage();
            var request = new HttpRequestMessage();
            var response = new HttpResponseMessage();

            // Convert the data into json object
            dynamic jsonObject  = JsonConvert.DeserializeObject(data); 

            if (jsonObject.ContainsKey("event_type") && !String.IsNullOrEmpty((string) jsonObject["event_type"]) && 
                    jsonObject.ContainsKey("message-id") && !String.IsNullOrEmpty((string) jsonObject["message-id"])) {
			    
                if (jsonObject["event_type"] == "request") {
                    httpMessage.IsRequest = true;
                    MoesifRequest mo_req = new MoesifRequest(jsonObject);
                    httpMessage.MessageId = Guid.Parse(mo_req.messageId);
                    request.Method = new HttpMethod(mo_req.method.ToUpper());
                    request.RequestUri = new Uri(mo_req.uri);
                    request.Properties.Add("UserId", mo_req.userId);
                    request.Properties.Add("CompanyId", mo_req.companyId);
                    request.Properties.Add("ReqHeaders", mo_req.requestHeaders);
                    request.Properties.Add("Metadata", mo_req.metadata);
                    request.Content = new StringContent(mo_req.requestBody);
                } else {
                    httpMessage.IsRequest = false;
                    MoesifResponse mo_res = new MoesifResponse(jsonObject);
                    httpMessage.MessageId = Guid.Parse(mo_res.messageId);
                    response.StatusCode = (HttpStatusCode) Convert.ToInt32(mo_res.statusCode);
                    response.Content = new StringContent(mo_res.responseBody);
                    TransformResponseHeaders(response, mo_res.responseHeaders);
                }
		    } else {
                throw new ArgumentException("Invalid formatted event :" + data);
            }

            if (httpMessage.IsRequest)
            {
                httpMessage.HttpRequestMessage = request;
            }
            else
            {
                httpMessage.HttpResponseMessage = response;
            }
            return httpMessage;
        }
    }
}