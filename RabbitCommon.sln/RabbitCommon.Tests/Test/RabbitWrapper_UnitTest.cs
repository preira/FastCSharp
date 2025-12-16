using Moq;
using RabbitMQ.Client;
using FastCSharp.Pool;

namespace FastCSharp.RabbitCommon.Tests
{
    public class RabbitChannelTest
    {
        private class TestIndividual : Individual<IChannel>
        {
            public TestIndividual(IChannel channel) : base(channel) { }
        }

        [Fact]
        public void Constructor_CallsExchangeAndQueueDeclare()
        {
            var mockChannel = new Mock<IChannel>();
            var queueDeclareOk = new QueueDeclareOk("q", 0, 0);
            mockChannel.Setup(c => c.ExchangeDeclarePassiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            mockChannel.Setup(c => c.QueueDeclarePassiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(queueDeclareOk));

            var channel = new RabbitChannel(mockChannel.Object, "ex", "q", "rk");
            mockChannel.Verify(c => c.ExchangeDeclarePassiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            mockChannel.Verify(c => c.QueueDeclarePassiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void Constructor_ThrowsIfExchangeDeclareFails()
        {
            var mockChannel = new Mock<IChannel>();
            mockChannel.Setup(c => c.ExchangeDeclarePassiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new System.Exception("fail"));

            Assert.ThrowsAny<System.AggregateException>(() =>
            {
                var channel = new RabbitChannel(mockChannel.Object, "ex", "q", "rk");
            });
        }


        [Fact]
        public void IsStalled_Property_Works()
        {
            var mockChannel = new Mock<IChannel>();
            var queueDeclareOk = new QueueDeclareOk("q", 0, 0);
            mockChannel.Setup(c => c.ExchangeDeclarePassiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            mockChannel.Setup(c => c.QueueDeclarePassiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(queueDeclareOk));

            var channel = new RabbitChannel(mockChannel.Object, "ex", "q", "rk");
            IRabbitChannel irc = channel;
            irc.IsStalled = true;
            Assert.True(irc.IsStalled);
            irc.IsStalled = false;
            Assert.False(irc.IsStalled);
        }
    }
}