using OrderProcessing.Repositories;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Services;

public class OrderProcessingService
{
	private readonly OrderRepository _orderRepository;
	private readonly CustomerService _customerService;
	private readonly InventoryService _inventoryService;
	private readonly PricingService _pricingService;
	private readonly PaymentService _paymentService;
	private readonly EmailService _emailService;

	public OrderProcessingService(
		OrderRepository orderRepository,
		CustomerService customerService,
		InventoryService inventoryService,
		PricingService pricingService,
		PaymentService paymentService,
		EmailService emailService)
	{
		_orderRepository = orderRepository;
		_customerService = customerService;
		_inventoryService = inventoryService;
		_pricingService = pricingService;
		_paymentService = paymentService;
		_emailService = emailService;
	}

	public async Task<bool> ProcessOrderAsync(
		Order order,
		CancellationToken ct)
	{
		bool stockReserved = false;
		bool paymentCaptured = false;
		string? transactionId = null;
		Customer? customer = null;

		Log.Info($"OrderProcessor -> Starting Order #{order.Id}...");

		try
		{
			ct.ThrowIfCancellationRequested();

			customer = await _customerService.GetCustomerAsync(
				order.CustomerId,
				ct);

			ct.ThrowIfCancellationRequested();

			stockReserved = await _inventoryService.ReserveStockAsync(
				order,
				ct);

			if (!stockReserved)
			{
				order.Status = OrderStatus.Rejected;
				order.FailureReason = "Insufficient stock";

				await _orderRepository.SaveAsync(order, ct);

				return false;
			}

			await _pricingService.CalculateTotalAsync(order, ct);

			ct.ThrowIfCancellationRequested();

			PaymentResult paymentResult =
				await _paymentService.ProcessPaymentAsync(order, ct);

			if (!paymentResult.Success)
			{
				await _inventoryService.ReleaseStockAsync(order, ct);
				stockReserved = false;

				order.Status = OrderStatus.PaymentFailed;
				order.FailureReason =
					paymentResult.Message ?? "Payment declined";

				await _orderRepository.SaveAsync(order, ct);

				return false;
			}

			paymentCaptured = true;
			transactionId = paymentResult.TransactionId;
			order.TransactionId = transactionId;

			ct.ThrowIfCancellationRequested();

			order.Status = OrderStatus.Completed;
			order.FailureReason = null;

			await _orderRepository.SaveAsync(order, ct);

			paymentCaptured = false;
			stockReserved = false;

			try
			{
				await _emailService.SendConfirmationAsync(
					order,
					customer,
					ct);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				// Email failure must not undo a completed, paid order.
				Log.Error(
					$"OrderProcessor -> Email failed for Order #{order.Id}: {ex.Message}");
			}

			Log.Success($"OrderProcessor -> Order #{order.Id} completed.");

			return true;
		}
		catch (OperationCanceledException)
		{
			Log.Info(
				$"OrderProcessor -> Cancellation requested for Order #{order.Id}.");

			await CleanupAfterCancellationAsync(
				order,
				stockReserved,
				paymentCaptured,
				transactionId);

			order.Status = OrderStatus.Pending;
			order.FailureReason = "Cancelled";

			await _orderRepository.SaveAsync(
				order,
				CancellationToken.None);

			throw;
		}
		catch (TimeoutException ex)
		{
			Log.Error(
				$"OrderProcessor -> Payment timeout for Order #{order.Id}.");

			if (stockReserved)
			{
				await _inventoryService.ReleaseStockAsync(
					order,
					CancellationToken.None);

				stockReserved = false;
			}

			order.Status = OrderStatus.PaymentFailed;
			order.FailureReason = "Payment gateway timeout";

			await _orderRepository.SaveAsync(
				order,
				CancellationToken.None);

			Log.Error(ex.Message);

			return false;
		}
		catch (CustomerNotFoundException ex)
		{
			return await RejectOrderAsync(order, ex.Message);
		}
		catch (InvalidOperationException ex)
		{
			return await RejectOrderAsync(order, ex.Message);
		}
		catch (InvalidCouponException ex)
		{
			if (stockReserved)
			{
				await _inventoryService.ReleaseStockAsync(
					order,
					CancellationToken.None);

				stockReserved = false;
			}

			return await RejectOrderAsync(order, ex.Message);
		}
		catch (ShippingProviderUnavailableException ex)
		{
			if (stockReserved)
			{
				await _inventoryService.ReleaseStockAsync(
					order,
					CancellationToken.None);

				stockReserved = false;
			}

			order.Status = OrderStatus.Failed;
			order.FailureReason = ex.Message;

			await _orderRepository.SaveAsync(
				order,
				CancellationToken.None);

			Log.Error(
				$"OrderProcessor -> Order #{order.Id} failed: {ex.Message}");

			return false;
		}
		catch (DatabaseWriteException ex)
		{
			if (paymentCaptured && !string.IsNullOrWhiteSpace(transactionId))
			{
				try
				{
					await _paymentService.RefundAsync(
						transactionId,
						CancellationToken.None);

					order.FailureReason =
						$"Database save failed after payment; payment {transactionId} refunded";
				}
				catch (Exception refundException)
				{
					order.FailureReason =
						$"Database save failed after payment; refund failed: {refundException.Message}";
				}
			}
			else
			{
				order.FailureReason = ex.Message;
			}

			if (stockReserved)
			{
				await _inventoryService.ReleaseStockAsync(
					order,
					CancellationToken.None);
			}

			order.Status = OrderStatus.Failed;

			// The repository may simulate another failure for this order.
			// Do not hide the original database error by retrying the same save.
			Log.Error(
				$"OrderProcessor -> Order #{order.Id} could not be saved: {order.FailureReason}");

			return false;
		}
	}

	private async Task<bool> RejectOrderAsync(
		Order order,
		string reason)
	{
		order.Status = OrderStatus.Rejected;
		order.FailureReason = reason;

		await _orderRepository.SaveAsync(
			order,
			CancellationToken.None);

		Log.Error(
			$"OrderProcessor -> Order #{order.Id} rejected: {reason}");

		return false;
	}

	private async Task CleanupAfterCancellationAsync(
		Order order,
		bool stockReserved,
		bool paymentCaptured,
		string? transactionId)
	{
		if (paymentCaptured && !string.IsNullOrWhiteSpace(transactionId))
		{
			try
			{
				await _paymentService.RefundAsync(
					transactionId,
					CancellationToken.None);
			}
			catch (Exception ex)
			{
				Log.Error(
					$"OrderProcessor -> Refund during cancellation failed for Order #{order.Id}: {ex.Message}");
			}
		}

		if (stockReserved)
		{
			try
			{
				await _inventoryService.ReleaseStockAsync(
					order,
					CancellationToken.None);
			}
			catch (Exception ex)
			{
				Log.Error(
					$"OrderProcessor -> Stock release during cancellation failed for Order #{order.Id}: {ex.Message}");
			}
		}
	}
}