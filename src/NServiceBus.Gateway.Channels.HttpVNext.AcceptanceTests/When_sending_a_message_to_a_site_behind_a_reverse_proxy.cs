namespace NServiceBus.Gateway.Channels.HttpVNext.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NUnit.Framework;

    public class When_sending_a_message_to_a_site_behind_a_reverse_proxy : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_be_able_to_reply_to_the_message()
        {
            var proxy = new Proxy();
            proxy.Start("http://localhost:25999/ProxyA/", "http://localhost:25999/SiteA/");

            var context = await Scenario.Define<Context>()
                .WithEndpoint<Headquarters>(b => b.When(async (bus, c) => await bus.SendToSites(new[]
                {
                    "SiteA"
                }, new MyRequest())))
                .WithEndpoint<SiteA>()
                .Done(c => c.GotResponseBack)
                .Run();

            proxy.Stop();

            Assert.IsTrue(context.GotResponseBack);
        }

        public class Context : ScenarioContext
        {
            public bool GotResponseBack { get; set; }
        }

        public class Headquarters : EndpointConfigurationBuilder
        {
            public Headquarters()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var gatewaySettings = c.Gateway();
                    gatewaySettings.ChannelFactories(s => new HttpVNextChannelSender(), s => new HttpVNextChannelReceiver());

                    gatewaySettings.AddReceiveChannel("http://localhost:25999/Headquarters/", "httpVNext");
                    gatewaySettings.AddSite("SiteA", "http://localhost:25999/ProxyA/", "httpVNext");
                });
            }

            public class MyResponseHandler : IHandleMessages<MyResponse>
            {
                public Context Context { get; set; }

                public Task Handle(MyResponse response, IMessageHandlerContext context)
                {
                    Context.GotResponseBack = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class SiteA : EndpointConfigurationBuilder
        {
            public SiteA()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var gatewaySettings = c.Gateway();
                    gatewaySettings.ChannelFactories(s => new HttpVNextChannelSender(), s => new HttpVNextChannelReceiver());

                    gatewaySettings.AddReceiveChannel("http://localhost:25999/SiteA/", "httpVNext");
                });
            }

            public class MyRequestHandler : IHandleMessages<MyRequest>
            {
                public Task Handle(MyRequest request, IMessageHandlerContext context)
                {
                    return context.Reply(new MyResponse());
                }
            }
        }

        public class MyRequest : IMessage
        {
        }

        public class MyResponse : IMessage
        {
        }
    }
}
