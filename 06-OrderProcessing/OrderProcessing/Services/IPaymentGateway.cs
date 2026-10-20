
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Services;

public interface IPaymentGateway
{
    Task<PaymentResult> ProcessPaymentAsync(
        int orderId,
        decimal amount,
        CancellationToken ct);

    Task RefundAsync(string transactionId);
}
