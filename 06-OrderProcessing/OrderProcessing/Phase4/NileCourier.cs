
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase4;

public sealed class NileCourier : ShippingCarrier
{
    public NileCourier(
        IReadOnlyList<Product> products,
        IEnumerable<int>? failForOrderIds = null)
        : base(
            name: "NileCourier",
            minResponseMs: 800,
            maxResponseMs: 1200,
            priceFactor: 1.10m,
            products: products,
            failForOrderIds: failForOrderIds)
    {
    }
}
