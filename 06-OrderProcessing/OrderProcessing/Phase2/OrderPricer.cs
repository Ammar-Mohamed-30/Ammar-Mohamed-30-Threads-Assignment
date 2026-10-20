
using System.Diagnostics;
using OrderProcessing.Repositories;
using OrderProcessing.Services;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase2;

public sealed class OrderPricer
{
    private readonly PricingService _pricingService;
    private readonly DiscountService _discountService;
    private readonly ShippingService _shippingService;
    private readonly ProductRepository _productRepository;
    private readonly CustomerRepository _customerRepository;

    public OrderPricer(
        PricingService pricingService,
        DiscountService discountService,
        ShippingService shippingService,
        ProductRepository productRepository,
        CustomerRepository customerRepository)
    {
        _pricingService = pricingService;
        _discountService = discountService;
        _shippingService = shippingService;
        _productRepository = productRepository;
        _customerRepository = customerRepository;
    }

    public async Task PriceOrderAsync(
        Order order,
        CancellationToken ct)
    {
        await _pricingService.CalculateSubTotalAsync(order, ct);

        Customer? customer = await _customerRepository.GetByIdAsync(
            order.CustomerId,
            ct);

        if (customer is null)
        {
            throw new InvalidOperationException(
                $"Customer #{order.CustomerId} not found.");
        }

        Task<decimal> discountTask =
            _discountService.GetDiscountAsync(order, customer, ct);

        Task<List<Product>> productsTask =
            _productRepository.GetAllAsync(ct);

        List<Product> products = await productsTask;

        Task<decimal> shippingTask =
            _shippingService.CalculateShippingCostAsync(
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
    }

    public async Task PriceOrderSequentialAsync(
        Order order,
        CancellationToken ct)
    {
        await _pricingService.CalculateSubTotalAsync(order, ct);

        Customer? customer = await _customerRepository.GetByIdAsync(
            order.CustomerId,
            ct);

        if (customer is null)
        {
            throw new InvalidOperationException(
                $"Customer #{order.CustomerId} not found.");
        }

        order.Discount = await _discountService.GetDiscountAsync(
            order,
            customer,
            ct);

        List<Product> products = await _productRepository.GetAllAsync(ct);

        order.ShippingCost = await _shippingService.CalculateShippingCostAsync(
            order,
            products,
            ct);

        order.TotalAmount = Math.Round(
            order.SubTotal - order.Discount + order.ShippingCost,
            2,
            MidpointRounding.AwayFromZero);
    }

    public async Task ComparePricingTimesAsync(
        Order order,
        CancellationToken ct)
    {
        var concurrentOrder = CloneOrder(order);
        var sequentialOrder = CloneOrder(order);

        Stopwatch timer = Stopwatch.StartNew();
        await PriceOrderAsync(concurrentOrder, ct);
        timer.Stop();

        Console.WriteLine(
            $"Concurrent pricing: {timer.Elapsed.TotalSeconds:F2} seconds");

        timer.Restart();
        await PriceOrderSequentialAsync(sequentialOrder, ct);
        timer.Stop();

        Console.WriteLine(
            $"Sequential pricing: {timer.Elapsed.TotalSeconds:F2} seconds");
    }

    private static Order CloneOrder(Order order)
    {
        return new Order
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            CouponCode = order.CouponCode,
            Items = order.Items.Select(item => new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            }).ToList(),
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            FailureReason = order.FailureReason,
            SubTotal = order.SubTotal,
            Discount = order.Discount,
            ShippingCost = order.ShippingCost,
            TotalAmount = order.TotalAmount
        };
    }
}
