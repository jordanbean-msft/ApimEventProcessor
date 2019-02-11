using ApimEventProcessorTests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using System.Threading;
using ApimEventProcessor;

namespace ApimEventProcessorTests
{
    public class MoesifHttpMessageProcessorTests
    {
        [Fact]
        public async Task SendHttpRequest()
        {

            // Arrange
            var httpRequestMessage = new HttpRequestMessage() {
                RequestUri = new Uri("http://example.com/foo")
            };

            var httpMessage = new HttpMessage()
            {
                IsRequest = true,
                HttpRequestMessage = httpRequestMessage
            };

            var consoleLogger = new ConsoleLogger();
            var fakeMessageHandler = new FakeMessageHandler();

            var message = new MoesifHttpMessageProcessor(consoleLogger);

            // Act
            await message.ProcessHttpMessage(httpMessage);

            // Assert
            Assert.NotNull(fakeMessageHandler.LastResponseMessage);
            Assert.Equal("api.moesif.net", fakeMessageHandler.LastResponseMessage.RequestMessage.RequestUri.Host);
        }
    }


    public class FakeMessageHandler : DelegatingHandler
    {
        public HttpResponseMessage LastResponseMessage { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastResponseMessage = new HttpResponseMessage()
            {
                RequestMessage = request
            };
            return LastResponseMessage;
        }
    }
}
