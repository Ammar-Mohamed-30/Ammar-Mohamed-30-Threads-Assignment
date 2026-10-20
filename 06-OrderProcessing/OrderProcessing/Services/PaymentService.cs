using OrderProcessing.Repositories;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Services;

public class PaymentService
{
	private readonly PaymentRepository _paymentRepository;

	public PaymentService(PaymentRepository paymentRepository)
	{
		_paymentRepository = paymentRepository;
	}

	public async Task<PaymentResult> ProcessPaymentAsync(
		Order order,
		CancellationToken ct)
	{
		Log.Info($"PaymentService -> Authorizing payment for Order #{order.Id}...");
		await Task.Delay(800, ct);

		Log.Info($"PaymentService -> Running fraud check for Order #{order.Id}...");
		await Task.Delay(500, ct);

		ct.ThrowIfCancellationRequested();

		if (order.Id == 5)
		{
			Log.Error($"PaymentService -> Payment timeout for Order #{order.Id}.");
			throw new TimeoutException($"Payment timed out for Order #{order.Id}.");
		}

		Log.Info($"PaymentService -> Capturing payment for Order #{order.Id}...");
		await Task.Delay(700, ct);

		ct.ThrowIfCancellationRequested();

		if (order.Id == 8)
		{
			PaymentTransaction declinedTransaction = new()
			{
				OrderId = order.Id,
				Amount = order.TotalAmount,
				Status = PaymentStatus.Declined,
				TransactionId = null,
				CreatedAt = DateTime.Now
			};

			await _paymentRepository.SaveAsync(declinedTransaction, ct);

			Log.Error($"PaymentService -> Payment declined for Order #{order.Id}.");

			return new PaymentResult
			{
				Success = false,
				TransactionId = null,
				Status = PaymentStatus.Declined,
				Message = "Card declined by issuer"
			};
		}

		string transactionId =
			"TXN-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

		PaymentTransaction capturedTransaction = new()
		{
			OrderId = order.Id,
			Amount = order.TotalAmount,
			Status = PaymentStatus.Captured,
			TransactionId = transactionId,
			CreatedAt = DateTime.Now
		};

		await _paymentRepository.SaveAsync(capturedTransaction, ct);

		Log.Success(
			$"PaymentService -> Payment captured for Order #{order.Id}. Transaction: {transactionId}");

		return new PaymentResult
		{
			Success = true,
			TransactionId = transactionId,
			Status = PaymentStatus.Captured,
			Message = "Payment captured successfully"
		};
	}

	public async Task RefundAsync(
		string transactionId,
		CancellationToken ct)
	{
		Log.Info($"PaymentService -> Starting refund for Transaction {transactionId}...");
		await Task.Delay(300, ct);

		await _paymentRepository.RefundAsync(transactionId, ct);

		Log.Success($"PaymentService -> Refund completed for Transaction {transactionId}.");
	}
}