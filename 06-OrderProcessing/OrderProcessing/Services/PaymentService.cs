
using OrderProcessing.Repositories;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Services;

public class PaymentService : IPaymentGateway
{
	private readonly PaymentRepository _paymentRepository;
	private readonly HashSet<int> _timeoutOrderIds;
	private readonly HashSet<int> _declineOrderIds;

	public PaymentService(
		PaymentRepository paymentRepository,
		IEnumerable<int>? timeoutOrderIds = null,
		IEnumerable<int>? declineOrderIds = null)
	{
		_paymentRepository = paymentRepository;
		_timeoutOrderIds = new HashSet<int>(
			timeoutOrderIds ?? Array.Empty<int>());
		_declineOrderIds = new HashSet<int>(
			declineOrderIds ?? Array.Empty<int>());
	}

	public async Task<PaymentResult> ProcessPaymentAsync(
		int orderId,
		decimal amount,
		CancellationToken ct)
	{
		Log.Info($"PaymentGateway -> Authorizing Order #{orderId}...");
		await Task.Delay(800, ct);

		Log.Info($"PaymentGateway -> Running fraud check for Order #{orderId}...");
		await Task.Delay(500, ct);

		ct.ThrowIfCancellationRequested();

		if (_declineOrderIds.Contains(orderId))
		{
			PaymentTransaction declinedTransaction = new()
			{
				OrderId = orderId,
				Amount = amount,
				Status = PaymentStatus.Declined,
				TransactionId = null,
				CreatedAt = DateTime.Now
			};

			await _paymentRepository.SaveAsync(declinedTransaction, ct);

			Log.Error($"PaymentGateway -> Payment declined for Order #{orderId}.");

			return new PaymentResult
			{
				Success = false,
				TransactionId = null,
				Status = PaymentStatus.Declined,
				Message = "Card declined by issuer"
			};
		}

		Log.Info($"PaymentGateway -> Capturing payment for Order #{orderId}...");

		int captureDelay = _timeoutOrderIds.Contains(orderId) ? 5000 : 700;
		await Task.Delay(captureDelay, ct);

		ct.ThrowIfCancellationRequested();

		string transactionId =
			"TXN-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

		PaymentTransaction capturedTransaction = new()
		{
			OrderId = orderId,
			Amount = amount,
			Status = PaymentStatus.Captured,
			TransactionId = transactionId,
			CreatedAt = DateTime.Now
		};

		await _paymentRepository.SaveAsync(capturedTransaction, ct);

		Log.Success(
			$"PaymentGateway -> Payment captured for Order #{orderId}. Transaction: {transactionId}");

		return new PaymentResult
		{
			Success = true,
			TransactionId = transactionId,
			Status = PaymentStatus.Captured,
			Message = "Payment captured successfully"
		};
	}

	public async Task RefundAsync(string transactionId)
	{
		Log.Info($"PaymentGateway -> Starting refund for {transactionId}...");
		await Task.Delay(300);

		await _paymentRepository.RefundAsync(
			transactionId,
			CancellationToken.None);

		Log.Success($"PaymentGateway -> Refund completed for {transactionId}.");
	}
}
