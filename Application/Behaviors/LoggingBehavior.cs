using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Behaviors
{
    public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    {
        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

        public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(
    TRequest request,
    CancellationToken cancellationToken,
    RequestHandlerDelegate<TResponse> next)
        {
            // Pre-handler log
            _logger.LogInformation("➡️ Handling {RequestName}: {@Request}",
                typeof(TRequest).Name, request);

            var response = await next(); // poziva sledeći behavior ili handler

            // Post-handler log
            _logger.LogInformation("✔️ Completed {RequestName}, response: {@Response}",
                typeof(TRequest).Name, response);

            return response;
        }
    }
}
