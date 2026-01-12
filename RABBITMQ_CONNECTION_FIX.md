# RabbitMQ Connection Failure Fix

## Problem

The application was crashing during startup when RabbitMQ was unavailable with the following error:

```
System.TimeoutException: Connection to RabbitMQ timed out
```

This happened because:
1. `RabbitMqMessageBroker` constructor was throwing exceptions during initialization
2. The DI container couldn't resolve the service, causing the entire application to crash
3. `OutboxProcessorWorker` couldn't start because it depends on `IMessageBroker`

## Solution

Made RabbitMQ connection failures **non-fatal** during application startup:

### 1. Graceful Connection Failure Handling

**File**: `Infrastructure/Services/RabbitMqMessageBroker.cs`

```csharp
// Constructor now catches initialization exceptions
if (_settings.Enabled)
{
    try
    {
        InitializeConnection();
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, 
            "Failed to initialize RabbitMQ connection on startup. " +
            "Will retry on first publish attempt.");
        IsCircuitOpen = true;
    }
}
```

### 2. Connection Timeouts

Added explicit timeout settings to prevent long hangs:

```csharp
var factory = new ConnectionFactory
{
    // ...existing settings...
    RequestedConnectionTimeout = TimeSpan.FromSeconds(10),
    SocketReadTimeout = TimeSpan.FromSeconds(10),
    SocketWriteTimeout = TimeSpan.FromSeconds(10)
};
```

### 3. Automatic Reconnection

The message broker will automatically attempt to reconnect when publishing:

```csharp
if (_channel == null || !_channel.IsOpen)
{
    _logger.LogInformation("Attempting to reconnect to RabbitMQ...");
    InitializeConnection();
}
```

## Benefits

? **Application starts successfully** even when RabbitMQ is unavailable  
? **Outbox pattern still works** - messages are queued in the database  
? **Automatic recovery** - will reconnect when RabbitMQ becomes available  
? **Circuit breaker protection** - prevents cascading failures  
? **Fast failure** - 10-second timeout instead of hanging indefinitely  

## Behavior

### When RabbitMQ is Unavailable

1. Application starts normally with a warning log
2. Outbox messages are processed and saved to the database
3. RabbitMQ publish attempts fail gracefully
4. Circuit breaker opens after 3 failures
5. Messages remain in outbox for retry

### When RabbitMQ Becomes Available

1. Next publish attempt reconnects automatically
2. Circuit breaker closes
3. Queued outbox messages are published
4. Normal operation resumes

## Configuration

You can disable RabbitMQ entirely in `appsettings.json`:

```json
{
  "RabbitMq": {
    "Enabled": false
  }
}
```

## Monitoring

Check RabbitMQ status via the health endpoint:

```bash
GET /health
```

Response includes:
- `RabbitMq.Enabled`: Whether RabbitMQ is configured
- `RabbitMq.CircuitOpen`: Whether the circuit breaker is open

## Testing

1. **Start application without RabbitMQ running**
   - ? Application should start successfully
   - ? Warning log should appear

2. **Create a sale**
   - ? Sale is saved to database
   - ? Outbox message is created
   - ? Analytics are updated

3. **Start RabbitMQ**
   - ? Next outbox processing cycle should publish pending messages
   - ? Log shows "Attempting to reconnect to RabbitMQ..."
   - ? Messages are published successfully

## Related Files

- `Infrastructure/Services/RabbitMqMessageBroker.cs` - Main message broker implementation
- `Workers/OutboxProcessorWorker.cs` - Background worker that processes outbox
- `Infrastructure/Resilience/CircuitBreakerPolicies.cs` - Circuit breaker configuration
- `appsettings.json` - RabbitMQ configuration

## Migration Notes

This is a **backward-compatible** change. No database migrations or configuration changes are required.

## Future Improvements

Consider:
- Health check endpoint specifically for RabbitMQ
- Retry with exponential backoff
- Dead letter queue for failed messages
- Metrics/telemetry for connection state
