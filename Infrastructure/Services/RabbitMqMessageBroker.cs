using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Infrastructure.Configuration;
using Infrastructure.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using RabbitMQ.Client;

namespace Infrastructure.Services
{
    public class RabbitMqMessageBroker : IMessageBroker, IDisposable
    {
        private readonly RabbitMqSettings _settings;
        private readonly ILogger<RabbitMqMessageBroker> _logger;
        private readonly ResiliencePipeline _circuitBreaker;
        private IConnection? _connection;
        private IModel? _channel;
        private readonly object _lock = new();
        private bool _disposed;

        public bool IsEnabled => _settings.Enabled;
        
        // Circuit breaker state for health checks
        public bool IsCircuitOpen { get; private set; }

        private bool HasMinimumConfiguration =>
            !string.IsNullOrWhiteSpace(_settings.HostName)
            && _settings.Port > 0
            && !string.IsNullOrWhiteSpace(_settings.ExchangeName);

        public RabbitMqMessageBroker(
            IOptions<RabbitMqSettings> settings,
            ILogger<RabbitMqMessageBroker> logger)
        {
            _settings = settings.Value;
            _logger = logger;

            _settings.HostName = _settings.HostName?.Trim() ?? string.Empty;
            _settings.UserName = _settings.UserName?.Trim() ?? string.Empty;
            _settings.Password = _settings.Password?.Trim() ?? string.Empty;
            _settings.VirtualHost = string.IsNullOrWhiteSpace(_settings.VirtualHost)
                ? "/"
                : _settings.VirtualHost.Trim();
            _settings.ExchangeName = _settings.ExchangeName?.Trim() ?? "trendplus.events";
            _settings.ExchangeType = string.IsNullOrWhiteSpace(_settings.ExchangeType)
                ? "topic"
                : _settings.ExchangeType.Trim();

            // Initialize circuit breaker
            _circuitBreaker = CircuitBreakerPolicies.CreateAsyncPipeline(
                logger: _logger,
                name: "RabbitMQ",
                failureThreshold: 3,
                breakDuration: TimeSpan.FromSeconds(60));

            if (_settings.Enabled && !HasMinimumConfiguration)
            {
                _logger.LogWarning(
                    "RabbitMQ is enabled but configuration is incomplete. Disabling broker. " +
                    "Host: {Host}, Port: {Port}, Exchange: {Exchange}",
                    _settings.HostName,
                    _settings.Port,
                    _settings.ExchangeName);

                _settings.Enabled = false;
                IsCircuitOpen = true;
                return;
            }

            if (_settings.Enabled)
            {
                try
                {
                    InitializeConnection();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, 
                        "Failed to initialize RabbitMQ connection on startup. Will retry on first publish attempt. " +
                        "Host: {Host}:{Port}, SSL: {UseSsl}",
                        _settings.HostName,
                        _settings.Port,
                        _settings.UseSsl);
                    IsCircuitOpen = true;
                }
            }
        }

        private void InitializeConnection()
        {
            if (!HasMinimumConfiguration)
            {
                throw new InvalidOperationException(
                    $"RabbitMQ configuration is incomplete. Host='{_settings.HostName}', Port='{_settings.Port}', Exchange='{_settings.ExchangeName}'.");
            }

            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _settings.HostName,
                    Port = _settings.Port,
                    UserName = _settings.UserName,
                    Password = _settings.Password,
                    VirtualHost = _settings.VirtualHost,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                    RequestedConnectionTimeout = TimeSpan.FromSeconds(10),
                    SocketReadTimeout = TimeSpan.FromSeconds(10),
                    SocketWriteTimeout = TimeSpan.FromSeconds(10)
                };

                // Enable SSL/TLS if configured
                if (_settings.UseSsl)
                {
                    factory.Ssl = new RabbitMQ.Client.SslOption
                    {
                        Enabled = true,
                        ServerName = _settings.HostName,
                        AcceptablePolicyErrors = System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch |
                                                  System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors
                    };
                }

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // Declare exchange (topic exchange for routing)
                _channel.ExchangeDeclare(
                    exchange: _settings.ExchangeName,
                    type: _settings.ExchangeType,
                    durable: true,
                    autoDelete: false);

                _logger.LogInformation(
                    "RabbitMQ connection established - Host: {Host}:{Port}, Exchange: {Exchange}, SSL: {UseSsl}",
                    _settings.HostName,
                    _settings.Port,
                    _settings.ExchangeName,
                    _settings.UseSsl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to establish RabbitMQ connection");
                throw;
            }
        }

        public async Task PublishAsync<T>(string eventType, T payload, string? routingKey = null, CancellationToken ct = default)
        {
            if (!_settings.Enabled)
            {
                _logger.LogWarning("RabbitMQ is disabled - Message not published: {EventType}", eventType);
                return;
            }

            try
            {
                // Execute through circuit breaker
                await _circuitBreaker.ExecuteAsync(async token =>
                {
                    await PublishInternalAsync(eventType, payload, routingKey);
                }, ct);
                
                IsCircuitOpen = false;
            }
            catch (BrokenCircuitException ex)
            {
                IsCircuitOpen = true;
                _logger.LogWarning(
                    "?? RabbitMQ Circuit Breaker OPEN - Message queued for later: {EventType}",
                    eventType);
                throw new InvalidOperationException(
                    $"RabbitMQ is temporarily unavailable. Circuit breaker is open.", ex);
            }
        }

        private Task PublishInternalAsync<T>(string eventType, T payload, string? routingKey)
        {
            if (_channel == null || !_channel.IsOpen)
            {
                lock (_lock)
                {
                    if (_channel == null || !_channel.IsOpen)
                    {
                        _logger.LogInformation("Attempting to reconnect to RabbitMQ...");
                        InitializeConnection();
                    }
                }
            }

            var message = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var body = Encoding.UTF8.GetBytes(message);

            var properties = _channel!.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Type = eventType;
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            var effectiveRoutingKey = routingKey ?? eventType.ToLowerInvariant();

            _channel.BasicPublish(
                exchange: _settings.ExchangeName,
                routingKey: effectiveRoutingKey,
                basicProperties: properties,
                body: body);

            _logger.LogInformation(
                "? Message published to RabbitMQ - EventType: {EventType}, RoutingKey: {RoutingKey}",
                eventType,
                effectiveRoutingKey);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _channel?.Close();
            _channel?.Dispose();
            _connection?.Close();
            _connection?.Dispose();

            _disposed = true;
            _logger.LogInformation("RabbitMQ connection closed");
        }
    }
}
