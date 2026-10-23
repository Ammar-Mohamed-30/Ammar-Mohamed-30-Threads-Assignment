
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase4;

public sealed class CairoExpress : ShippingCarrier
{
    public CairoExpress(
        IReadOnlyList<Product> products,
        IEnumerable<int>? failForOrderIds = null)
        : base(
            name: "CairoExpress",
            minResponseMs: 300,
            maxResponseMs: 2000,
            priceFactor: 0.95m,
            products: products,
            failForOrderIds: failForOrderIds)
    {
    }

    protected override bool ShouldFail(Order order)
    {
        return order.Id % 2 == 0 || base.ShouldFail(order);
    }
}
