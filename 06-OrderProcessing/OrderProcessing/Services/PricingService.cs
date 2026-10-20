
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
        List<Product> products =
            await _productRepository.GetAllAsync(ct);

        return CalculateSubTotal(order, products, ct);
    }

    private static decimal CalculateSubTotal(
        Order order,
        List<Product> products,
        CancellationToken ct)
    {
        decimal subTotal = 0m;

        foreach (OrderItem item in order.Items)
        {
            ct.ThrowIfCancellationRequested();

            Product? product = products.FirstOrDefault(
                p => p.Id == item.ProductId);

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
        Customer? customer = await _customerRepository.GetByIdAsync(
            order.CustomerId,
            ct);

        if (customer is null)
        {
            throw new CustomerNotFoundException(order.CustomerId);
        }

        List<Product> products =
            await _productRepository.GetAllAsync(ct);

        CalculateSubTotal(order, products, ct);

        Task<decimal> discountTask = _discountService.GetDiscountAsync(
            order,
            customer,
            ct);

        Task<decimal> shippingTask = _shippingService.CalculateShippingCostAsync(
            order,
            products,
            ct);

        await Task.WhenAll(discountTask, shippingTask);

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
