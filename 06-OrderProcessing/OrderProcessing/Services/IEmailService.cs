
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Services;

public interface IEmailService
{
    Task SendConfirmationAsync(
        Order order,
        Customer customer,
        CancellationToken ct);
}
