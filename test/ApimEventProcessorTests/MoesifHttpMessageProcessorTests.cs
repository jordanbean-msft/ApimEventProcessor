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
                RequestUri = new Uri("https://api.github.com"),
                Content = new StringContent("{'key_a': 40}")
            };

            var httpMessage = new HttpMessage()
            {
                MessageId = new Guid(),
                IsRequest = true,
                HttpRequestMessage = httpRequestMessage
            };

            var consoleLogger = new ConsoleLogger();

            var message = new MoesifHttpMessageProcessor(consoleLogger);

            // Act
            await message.ProcessHttpMessage(httpMessage);

            var httpResponseMessage = new HttpResponseMessage()
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent("{'key_b': 25}")
            };

            var responseMessage = new HttpMessage()
            {
                HttpRequestMessage = httpRequestMessage,
                MessageId = httpMessage.MessageId,
                IsRequest = false,
                HttpResponseMessage = httpResponseMessage
            };

            await message.ProcessHttpMessage(responseMessage);

            // Assert
            Assert.NotNull(responseMessage);
            Assert.Equal("api.github.com", responseMessage.HttpRequestMessage.RequestUri.Host);
        }
    }
}
