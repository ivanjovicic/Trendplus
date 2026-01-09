namespace Infrastructure.Configuration
{
    public class RabbitMqSettings
    {
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string UserName { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public string VirtualHost { get; set; } = "/";
        public string ExchangeName { get; set; } = "trendplus.events";
        public string ExchangeType { get; set; } = "topic";
        public bool UseSsl { get; set; } = false;
        public bool Enabled { get; set; } = false;
    }
}
