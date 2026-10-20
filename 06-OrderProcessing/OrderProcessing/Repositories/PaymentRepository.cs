using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Repositories;

public class PaymentRepository
{
    private readonly JsonFileStore _store;

    public PaymentRepository(JsonFileStore store)
    {
        _store = store;
    }

    public async Task SaveAsync(
        PaymentTransaction transaction,
        CancellationToken ct)
    {
        Log.Info(
            $"PaymentRepository -> Opening connection for Order #{transaction.OrderId}...");

        await Task.Delay(200, ct);

        Log.Info(
            $"PaymentRepository -> Beginning payment transaction for Order #{transaction.OrderId}...");

        await Task.Delay(100, ct);

        Log.Info(
            $"PaymentRepository -> Saving payment for Order #{transaction.OrderId}...");

        await Task.Delay(400, ct);

        await _store.UpdateAsync<PaymentTransaction, bool>(
            DataFiles.Payments,
            payments =>
            {
                int existingIndex = payments.FindIndex(
                    payment => payment.OrderId == transaction.OrderId);

                if (existingIndex >= 0)
                {
                    payments[existingIndex] = transaction;
                }
                else
                {
                    payments.Add(transaction);
                }

                return true;
            },
            ct);

        Log.Info(
            $"PaymentRepository -> Committing payment for Order #{transaction.OrderId}...");

        await Task.Delay(200, ct);

        Log.Success(
            $"PaymentRepository -> Payment saved for Order #{transaction.OrderId}.");
    }

    public async Task RefundAsync(
        string transactionId,
        CancellationToken ct)
    {
        Log.Info(
            $"PaymentRepository -> Opening connection to refund {transactionId}...");

        await Task.Delay(200, ct);

        Log.Info(
            $"PaymentRepository -> Beginning refund transaction for {transactionId}...");

        await Task.Delay(100, ct);

        Log.Info(
            $"PaymentRepository -> Updating payment status for {transactionId}...");

        await Task.Delay(400, ct);

        bool updated = await _store.UpdateAsync<PaymentTransaction, bool>(
            DataFiles.Payments,
            payments =>
            {
                PaymentTransaction? payment = payments.FirstOrDefault(
                    item => item.TransactionId == transactionId);

                if (payment is null)
                {
                    return false;
                }

                payment.Status = PaymentStatus.Refunded;
                return true;
            },
            ct);

        if (!updated)
        {
            throw new InvalidOperationException(
                $"Payment transaction '{transactionId}' was not found.");
        }

        Log.Info(
            $"PaymentRepository -> Committing refund for {transactionId}...");

        await Task.Delay(200, ct);

        Log.Success(
            $"PaymentRepository -> Refund recorded for {transactionId}.");
    }

    public async Task<List<PaymentTransaction>> GetAllAsync(
        CancellationToken ct)
    {
        Log.Info("PaymentRepository -> Opening connection...");

        await Task.Delay(200, ct);

        Log.Info("PaymentRepository -> Executing payments query...");

        await Task.Delay(Random.Shared.Next(300, 601), ct);

        List<PaymentTransaction> payments =
            await _store.ReadAsync<PaymentTransaction>(
                DataFiles.Payments,
                ct);

        Log.Success(
            $"PaymentRepository -> Retrieved {payments.Count} payment records.");

        return payments;
    }
}