
using System.Diagnostics;
using OrderProcessing.Repositories;
using OrderProcessing.Services;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase3;

public class OrderProcessor
{
    private readonly OrderRepository _orderRepository;
    private readonly CustomerService _customerService;
    private readonly InventoryService _inventoryService;
    private readonly PricingService _pricingService;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IEmailService _emailService;

    public OrderProcessor(
        OrderRepository orderRepository,
        CustomerService customerService,
        InventoryService inventoryService,
        PricingService pricingService,
        IPaymentGateway paymentGateway,
        IEmailService emailService)
    {
        _orderRepository = orderRepository;
        _customerService = customerService;
        _inventoryService = inventoryService;
        _pricingService = pricingService;
        _paymentGateway = paymentGateway;
        _emailService = emailService;
    }

    public async Task<bool> ProcessOrderAsync(
        Order order,
        CancellationToken ct = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        bool stockReserved = false;
        bool paymentCaptured = false;
        string? transactionId = null;
        Customer? customer = null;

        Log.Info($"OrderProcessor -> Starting Order #{order.Id}.");

        try
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                customer = await _customerService.GetCustomerAsync(
                    order.CustomerId,
                    ct);
            }
            catch (CustomerNotFoundException ex)
            {
                return await RejectOrderAsync(order, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return await RejectOrderAsync(order, ex.Message);
            }

            stockReserved = await _inventoryService.ReserveStockAsync(
                order,
                ct);

            if (!stockReserved)
            {
                return await RejectOrderAsync(
                    order,
                    "Insufficient stock");
            }

            try
            {
                await _pricingService.CalculateTotalAsync(order, ct);
            }
            catch (InvalidCouponException ex)
            {
                await _inventoryService.ReleaseStockAsync(
                    order,
                    CancellationToken.None);

                stockReserved = false;

                return await RejectOrderAsync(order, ex.Message);
            }

            ct.ThrowIfCancellationRequested();

            PaymentResult paymentResult;

            using (CancellationTokenSource paymentCts =
                   CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                paymentCts.CancelAfter(TimeSpan.FromSeconds(3));

                try
                {
                    paymentResult = await _paymentGateway.ProcessPaymentAsync(
                        order.Id,
                        order.TotalAmount,
                        paymentCts.Token);
                }
                catch (OperationCanceledException)
                    when (!ct.IsCancellationRequested)
                {
                    await _inventoryService.ReleaseStockAsync(
                        order,
                        CancellationToken.None);

                    stockReserved = false;

                    order.Status = OrderStatus.PaymentFailed;
                    order.FailureReason = "Payment gateway timeout (3s)";

                    await _orderRepository.SaveAsync(
                        order,
                        CancellationToken.None);

                    Log.Error(
                        $"OrderProcessor -> Payment timed out for Order #{order.Id}.");

                    return false;
                }
            }

            if (!paymentResult.Success)
            {
                await _inventoryService.ReleaseStockAsync(
                    order,
                    CancellationToken.None);

                stockReserved = false;

                order.Status = OrderStatus.PaymentFailed;
                order.FailureReason = paymentResult.Message;

                await _orderRepository.SaveAsync(
                    order,
                    CancellationToken.None);

                Log.Error(
                    $"OrderProcessor -> Payment failed for Order #{order.Id}: {order.FailureReason}");

                return false;
            }

            paymentCaptured = true;
            transactionId = paymentResult.TransactionId;
            order.TransactionId = transactionId;

            order.Status = OrderStatus.Completed;
            order.FailureReason = null;

            try
            {
                await _orderRepository.SaveAsync(
                    order,
                    CancellationToken.None);
            }
            catch (DatabaseWriteException)
            {
                string refundMessage;

                try
                {
                    if (!string.IsNullOrWhiteSpace(transactionId))
                    {
                        await _paymentGateway.RefundAsync(transactionId);
                    }

                    refundMessage =
                        $"Database save failed after payment; payment {transactionId} refunded";
                }
                catch (Exception ex)
                {
                    refundMessage =
                        $"Database save failed after payment; refund failed: {ex.Message}";
                }

                paymentCaptured = false;

                if (stockReserved)
                {
                    await _inventoryService.ReleaseStockAsync(
                        order,
                        CancellationToken.None);

                    stockReserved = false;
                }

                order.Status = OrderStatus.Failed;
                order.FailureReason = refundMessage;

                await _orderRepository.SaveAsync(
                    order,
                    CancellationToken.None);

                Log.Error(
                    $"OrderProcessor -> Order #{order.Id} failed during save.");

                return false;
            }

            // The order is saved and paid. Do not roll back its stock.
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
                // Email problems must not change a completed order.
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

            if (paymentCaptured &&
                !string.IsNullOrWhiteSpace(transactionId))
            {
                try
                {
                    await _paymentGateway.RefundAsync(transactionId);
                }
                catch (Exception ex)
                {
                    Log.Error($"Refund during cancellation failed: {ex.Message}");
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
                    Log.Error($"Stock release during cancellation failed: {ex.Message}");
                }
            }

            order.Status = OrderStatus.Pending;
            order.FailureReason = "Cancelled";

            await _orderRepository.SaveAsync(
                order,
                CancellationToken.None);

            throw;
        }
        catch (Exception ex)
        {
            if (stockReserved)
            {
                try
                {
                    await _inventoryService.ReleaseStockAsync(
                        order,
                        CancellationToken.None);
                }
                catch (Exception releaseException)
                {
                    Log.Error(
                        $"Stock release failed for Order #{order.Id}: {releaseException.Message}");
                }
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
        finally
        {
            stopwatch.Stop();

            Log.Info(
                $"OrderProcessor -> Order #{order.Id} finished in {stopwatch.ElapsedMilliseconds} ms on Thread #{Environment.CurrentManagedThreadId}.");
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
}
