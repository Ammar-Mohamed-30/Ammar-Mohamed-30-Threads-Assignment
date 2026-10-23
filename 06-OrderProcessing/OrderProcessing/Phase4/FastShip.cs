
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase4;

public sealed class FastShip : ShippingCarrier
{
    public FastShip(
        IReadOnlyList<Product> products,
        IEnumerable<int>? failForOrderIds = null)
        : base(
            name: "FastShip",
            minResponseMs: 500,
            maxResponseMs: 1500,
            priceFactor: 1.00m,
            products: products,
            failForOrderIds: failForOrderIds)
    {
    }
}
