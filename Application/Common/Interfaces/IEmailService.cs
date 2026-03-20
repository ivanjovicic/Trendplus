using Application.Inventory.Models;

namespace Application.Common.Interfaces;

public interface IEmailService
{
    bool IsEnabled { get; }

    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
