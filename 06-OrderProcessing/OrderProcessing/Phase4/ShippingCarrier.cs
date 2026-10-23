
using System.Diagnostics;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase4;

public abstract class ShippingCarrier : IShippingCarrier
{
    private readonly int _minResponseMs;
    private readonly int _maxResponseMs;
    private readonly decimal _priceFactor;
    private readonly HashSet<int> _failForOrderIds;
    private readonly IReadOnlyList<Product> _products;

    protected ShippingCarrier(
        string name,
        int minResponseMs,
        int maxResponseMs,
        decimal priceFactor,
        IReadOnlyList<Product> products,
        IEnumerable<int>? failForOrderIds = null)
    {
        Name = name;
        _minResponseMs = minResponseMs;
        _maxResponseMs = maxResponseMs;
        _priceFactor = priceFactor;
        _products = products;
        _failForOrderIds = new HashSet<int>(
            failForOrderIds ?? Array.Empty<int>());
    }

    public string Name { get; }

    protected virtual bool ShouldFail(Order order)
    {
        return _failForOrderIds.Contains(order.Id);
    }

    public async Task<decimal> GetQuoteAsync(
        Order order,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // DNS / handshake.
            await Task.Delay(150, ct);

            // Fail immediately after the handshake when configured to fail.
            if (ShouldFail(order))
            {
                throw new ShippingProviderUnavailableException(
                    $"{Name} is unavailable for Order #{order.Id}");
            }

            // Simulate the carrier response time.
            await Task.Delay(
                Random.Shared.Next(
                    _minResponseMs,
                    _maxResponseMs + 1),
                ct);

            decimal shippingCost = 0m;

            if (order.SubTotal < 1000m)
            {
                decimal totalQuantity = order.Items.Sum(
                    item => item.Quantity);

                decimal totalWeight = order.Items.Sum(item =>
                {
                    Product? product = _products.FirstOrDefault(
                        p => p.Id == item.ProductId);

                    if (product is null)
                    {
                        throw new InvalidOperationException(
                            $"Product #{item.ProductId} not found.");
                    }

                    return product.WeightKg * item.Quantity;
                });

                shippingCost = Math.Round(
                    5.00m +
                    (1.50m * totalQuantity) +
                    (2.00m * totalWeight),
                    2,
                    MidpointRounding.AwayFromZero);
            }

            decimal price = decimal.Round(
                shippingCost * _priceFactor,
                2,
                MidpointRounding.AwayFromZero);

            Console.WriteLine(
                $"[{Name}] Order #{order.Id} quoted {price:F2} " +
                $"after {stopwatch.ElapsedMilliseconds} ms.");

            return price;
        }
        catch (ShippingProviderUnavailableException ex)
        {
            Console.WriteLine(
                $"[{Name}] quote failed: {ex.Message}");

            throw;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine(
                $"[{Name}] cancelled for Order #{order.Id}.");

            throw;
        }
    }
}
