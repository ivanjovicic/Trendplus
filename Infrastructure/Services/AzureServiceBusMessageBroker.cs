using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class AzureServiceBusMessageBroker : IMessageBroker, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;
    private readonly ILogger<AzureServiceBusMessageBroker> _logger;
    private readonly bool _enabled;

    public bool IsEnabled => _enabled;

    public AzureServiceBusMessageBroker(
        IConfiguration configuration,
        ILogger<AzureServiceBusMessageBroker> logger)
    {
        _logger = logger;
        _enabled = configuration.GetValue<bool>("ServiceBus:Enabled");

        if (_enabled)
        {
            var connectionString = configuration["ServiceBus:ConnectionString"]
                ?? throw new InvalidOperationException("ServiceBus:ConnectionString missing");
            
            var topicName = configuration["ServiceBus:TopicName"] ?? "trendplus-events";

            _client = new ServiceBusClient(connectionString);
            _sender = _client.CreateSender(topicName);

            _logger.LogInformation("Azure Service Bus connected - Topic: {Topic}", topicName);
        }
    }

    public async Task PublishAsync<T>(
        string eventType,
        T payload,
        string? routingKey = null,
        CancellationToken ct = default)
    {
        if (!_enabled)
        {
            _logger.LogWarning("Service Bus is disabled - Message not published: {EventType}", eventType);
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var message = new ServiceBusMessage(json)
            {
                ContentType = "application/json",
                Subject = eventType,
                MessageId = Guid.NewGuid().ToString(),
                ApplicationProperties =
                {
                    ["EventType"] = eventType,
                    ["Timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                }
            };

            if (!string.IsNullOrWhiteSpace(routingKey))
            {
                message.ApplicationProperties["RoutingKey"] = routingKey;
            }

            await _sender.SendMessageAsync(message, ct);

            _logger.LogInformation(
                "Message published to Service Bus - EventType: {EventType}",
                eventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to Service Bus - EventType: {EventType}", eventType);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_sender != null)
            await _sender.DisposeAsync();
        
        if (_client != null)
            await _client.DisposeAsync();

        _logger.LogInformation("Service Bus connection closed");
    }
}
