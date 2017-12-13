namespace NServiceBus.Gateway.Channels.HttpVNext.AcceptanceTests
{
    public class DefaultServer : DefaultServerWithNoStorage
    {
        public DefaultServer()
        {
            ConfigureStorage = true;
        }
    }
}
