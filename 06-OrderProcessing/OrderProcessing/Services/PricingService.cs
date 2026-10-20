using OrderProcessing.Repositories;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Services;

public class PricingService
{
    private readonly ProductRepository _productRepository;
    private readonly DiscountService _discountService;
    private readonly ShippingService _shippingService;
    private readonly CustomerRepository _customerRepository;

    public PricingService(
        ProductRepository productRepository,
        DiscountService discountService,
        ShippingService shippingService,
        CustomerRepository customerRepository)
    {
        _productRepository = productRepository;
        _discountService = discountService;
        _shippingService = shippingService;
        _customerRepository = customerRepository;
    }

    public async Task<decimal> CalculateSubTotalAsync(
        Order order,
        CancellationToken ct)
    {
        Log.Info(
            $"PricingService -> Order #{order.Id}: Loading products...");

        await Task.Delay(100, ct);

        Log.Info(
            $"PricingService -> Order #{order.Id}: Calculating subtotal...");

        await Task.Delay(100, ct);

        decimal subTotal = 0m;

        foreach (OrderItem item in order.Items)
        {
            ct.ThrowIfCancellationRequested();

            Product? product = await _productRepository.GetByIdAsync(
                item.ProductId,
                ct);

            if (product is null)
            {
                throw new InvalidOperationException(
                    $"Product #{item.ProductId} not found.");
            }

            if (item.Quantity <= 0)
            {
                throw new InvalidOperationException(
                    $"Invalid quantity for Product #{item.ProductId}.");
            }

            item.UnitPrice = product.Price;
            subTotal += product.Price * item.Quantity;
        }

        order.SubTotal = Math.Round(
            subTotal,
            2,
            MidpointRounding.AwayFromZero);

        Log.Success(
            $"PricingService -> Order #{order.Id}: SubTotal = {order.SubTotal:F2}");

        return order.SubTotal;
    }

    public async Task<decimal> CalculateTotalAsync(
        Order order,
        CancellationToken ct)
    {
        Log.Info(
            $"PricingService -> Loading Customer #{order.CustomerId}...");

        await Task.Delay(100, ct);

        Log.Info(
            $"PricingService -> Preparing total calculation for Order #{order.Id}...");

        await Task.Delay(100, ct);

        Customer? customer = await _customerRepository.GetByIdAsync(
            order.CustomerId,
            ct);

        if (customer is null)
        {
            throw new CustomerNotFoundException(order.CustomerId);
        }

        await CalculateSubTotalAsync(order, ct);

        Task<decimal> discountTask = _discountService.GetDiscountAsync(
            order,
            customer,
            ct);

        List<Product> products = await _productRepository.GetAllAsync(ct);

        Task<decimal> shippingTask = _shippingService.CalculateShippingCostAsync(
            order,
            products,
            ct);

        order.Discount = await discountTask;
        order.ShippingCost = await shippingTask;

        order.TotalAmount = Math.Round(
            order.SubTotal - order.Discount + order.ShippingCost,
            2,
            MidpointRounding.AwayFromZero);

        Log.Success(
            $"PricingService -> Order #{order.Id}: Total = {order.TotalAmount:F2}");

        return order.TotalAmount;
    }
}