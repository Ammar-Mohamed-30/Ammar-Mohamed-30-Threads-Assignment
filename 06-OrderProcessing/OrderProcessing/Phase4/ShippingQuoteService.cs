
using System.Diagnostics;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase4;

public sealed class ShippingQuoteService
{
    private readonly IReadOnlyList<IShippingCarrier> _carriers;
    private readonly TimeSpan _budget;

    public ShippingQuoteService(
        IEnumerable<IShippingCarrier> carriers,
        TimeSpan? budget = null)
    {
        _carriers = carriers.ToList();

        if (_carriers.Count == 0)
        {
            throw new ArgumentException(
                "At least one shipping carrier is required.",
                nameof(carriers));
        }

        _budget = budget ?? TimeSpan.FromMilliseconds(1200);
    }

    public async Task<(string Carrier, decimal Price, long ElapsedMs)>
        GetFastestQuoteAsync(Order order, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        using var carrierCts =
            CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Start all carriers concurrently.
        List<Task<QuoteOutcome>> pending = _carriers
            .Select(carrier => GetQuoteSafelyAsync(
                carrier, order, carrierCts.Token))
            .ToList();

        Task timeoutTask = Task.Delay(_budget, ct);

        try
        {
            while (pending.Count > 0)
            {
                Task completed = await Task.WhenAny(
                    pending.Cast<Task>().Append(timeoutTask));

                // Caller cancellation takes priority over fallback.
                ct.ThrowIfCancellationRequested();

                if (completed == timeoutTask ||
                    stopwatch.Elapsed >= _budget)
                {
                    carrierCts.Cancel();
                    await ObserveAllAsync(pending);

                    return ("Flat rate", 25.00m,
                        stopwatch.ElapsedMilliseconds);
                }

                Task<QuoteOutcome> quoteTask =
                    (Task<QuoteOutcome>)completed;

                pending.Remove(quoteTask);

                QuoteOutcome outcome = await quoteTask;

                if (outcome.Error is not null)
                {
                    Console.WriteLine(
                        $"[{outcome.Carrier}] quote failed: " +
                        outcome.Error.Message);

                    continue;
                }

                if (outcome.Cancelled)
                {
                    continue;
                }

                // First successful quote wins.
                carrierCts.Cancel();

                await ObserveAllAsync(pending);

                return (
                    outcome.Carrier,
                    outcome.Price!.Value,
                    stopwatch.ElapsedMilliseconds);
            }

            throw new ShippingProviderUnavailableException(
                "All carriers failed");
        }
        catch (OperationCanceledException)
            when (ct.IsCancellationRequested)
        {
            carrierCts.Cancel();
            await ObserveAllAsync(pending);
            throw;
        }
    }

    private static async Task<QuoteOutcome> GetQuoteSafelyAsync(
        IShippingCarrier carrier,
        Order order,
        CancellationToken ct)
    {
        try
        {
            decimal price = await carrier.GetQuoteAsync(order, ct);

            return new QuoteOutcome(
                carrier.Name, price, null, false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new QuoteOutcome(
                carrier.Name, null, null, true);
        }
        catch (Exception ex)
        {
            return new QuoteOutcome(
                carrier.Name, null, ex, false);
        }
    }

    private static async Task ObserveAllAsync(
        IEnumerable<Task<QuoteOutcome>> tasks)
    {
        await Task.WhenAll(tasks);
    }

    private sealed record QuoteOutcome(
        string Carrier,
        decimal? Price,
        Exception? Error,
        bool Cancelled);
}
