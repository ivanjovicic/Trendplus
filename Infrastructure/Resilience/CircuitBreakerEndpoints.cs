using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Application.Common.Interfaces;
using Infrastructure.Services;

namespace Infrastructure.Resilience;

public static class CircuitBreakerEndpoints
{
    public static IEndpointRouteBuilder MapCircuitBreakerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/circuit-breaker");

        // Get circuit breaker status
        group.MapGet("/status", (IMessageBroker messageBroker) =>
        {
            var rabbitMq = messageBroker as RabbitMqMessageBroker;
            
            var status = new
            {
                RabbitMq = new
                {
                    Enabled = messageBroker.IsEnabled,
                    CircuitOpen = rabbitMq?.IsCircuitOpen ?? false,
                    Status = rabbitMq?.IsCircuitOpen == true ? "OPEN" : "CLOSED"
                },
                Timestamp = DateTime.UtcNow
            };

            return Results.Ok(status);
        });

        return endpoints;
    }
}
