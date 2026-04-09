namespace SMEFLOWSystem.Infrastructure.Options;

public class RabbitMqOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string VirtualHost { get; set; } = "/";

    public string Exchange { get; set; } = string.Empty;
    public string ExchangeType { get; set; } = "topic";
    public bool Durable { get; set; } = true;

    public int RequestedHeartbeat { get; set; } = 30;
    public bool AutomaticRecoveryEnabled { get; set; } = true;
    public int NetworkRecoveryIntervalSeconds { get; set; } = 5;
    public ushort PrefetchCount { get; set; } = 20;

    public Dictionary<string, string> Queues { get; set; } = new();
    public Dictionary<string, string> RoutingKeys { get; set; } = new();
}
