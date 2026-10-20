
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Services;

public class ShippingService
{
    private readonly HashSet<int> _failShippingForOrderIds;

    public ShippingService(IEnumerable<int> failShippingForOrderIds)
    {
        _failShippingForOrderIds = new HashSet<int>(failShippingForOrderIds);
    }

    public async Task<decimal> CalculateShippingCostAsync(
        Order order,
        List<Product> products,
        CancellationToken ct)
    {
        Log.Info($"ShippingService -> Order #{order.Id}: DNS/handshake...");
        await Task.Delay(150, ct);

        Log.Info($"ShippingService -> Order #{order.Id}: Sending shipping request...");
        await Task.Delay(300, ct);

        if (_failShippingForOrderIds.Contains(order.Id))
        {
            Log.Error($"ShippingService -> Order #{order.Id}: Provider unavailable.");
            throw new ShippingProviderUnavailableException(
                "Shipping provider unavailable");
        }

        Log.Info($"ShippingService -> Order #{order.Id}: Waiting for response...");
        await Task.Delay(Random.Shared.Next(500, 1501), ct);

        ct.ThrowIfCancellationRequested();

        if (order.SubTotal >= 1000m)
        {
            Log.Success($"ShippingService -> Order #{order.Id}: Shipping = 0.00");
            return 0m;
        }

        decimal totalQuantity = order.Items.Sum(item => item.Quantity);

        decimal totalWeight = order.Items.Sum(item =>
        {
            Product? product = products.FirstOrDefault(
                product => product.Id == item.ProductId);

            if (product is null)
            {
                throw new InvalidOperationException(
                    $"Product #{item.ProductId} not found.");
            }

            return product.WeightKg * item.Quantity;
        });

        decimal shippingCost = Math.Round(
            5.00m + (1.50m * totalQuantity) + (2.00m * totalWeight),
            2,
            MidpointRounding.AwayFromZero);

        Log.Success(
            $"ShippingService -> Order #{order.Id}: Shipping = {shippingCost:F2}");

        return shippingCost;
    }
}
