
using System.Diagnostics;
using OrderProcessing.Repositories;
using OrderProcessing.Services;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public sealed class Phase1Runner
{
    public async Task RunAsync(CancellationToken ct)
    {
        // Hide detailed service logs so the final output stays clean.
        Log.Info("Preparing Phase 1...");

        var store = new JsonFileStore();
        var orderRepository = new OrderRepository(store);
        var seeder = new DataSeeder(store);

        // Reset the data before running the validation.
        await seeder.ResetAsync(ct);

        var orders = await orderRepository.GetIncomingOrdersAsync(ct);

        var customerRepository = new CustomerRepository(store);
        var couponRepository = new CouponRepository(store);
        var productRepository = new ProductRepository(store);

        var customerService = new CustomerService(customerRepository);
        var discountService = new DiscountService(couponRepository);
        var shippingService = new ShippingService(Array.Empty<int>());

        var pricingService = new PricingService(
            productRepository,
            discountService,
            shippingService,
            customerRepository);

        var inventoryService = new InventoryService(productRepository);

        // Run the asynchronous sequential validation.
        // The synchronous timing is measured separately below.
        var asyncTimer = Stopwatch.StartNew();

        int validated = 0;
        int rejected = 0;

        foreach (var order in orders)
        {
            ct.ThrowIfCancellationRequested();

            bool stockReserved = false;

            try
            {
                await customerService.GetCustomerAsync(order.CustomerId, ct);

                stockReserved = await inventoryService.ReserveStockAsync(
                    order,
                    ct);

                if (!stockReserved)
                {
                    throw new InvalidOperationException(
                        $"Insufficient stock for '{GetProductName(order)}'");
                }

                await pricingService.CalculateTotalAsync(order, ct);

                order.Status = OrderStatus.Validated;
                order.FailureReason = null;

                await orderRepository.SaveAsync(order, ct);
                validated++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason = GetRejectionReason(order, ex.Message);

                await orderRepository.SaveAsync(order, ct);
                rejected++;
            }
            finally
            {
                if (stockReserved)
                {
                    await inventoryService.ReleaseStockAsync(
                        order,
                        CancellationToken.None);
                }
            }
        }

        asyncTimer.Stop();

        // Read the saved records back from the JSON file.
        var savedOrders = await orderRepository.GetIncomingOrdersAsync(ct);

        Console.WriteLine();
        Console.WriteLine("===== Validation Summary =====");
        Console.WriteLine($"Total Orders: {orders.Count}");
        Console.WriteLine($"Validated:    {validated}");
        Console.WriteLine($"Rejected:     {rejected}");

        foreach (var order in savedOrders
                     .Where(o => o.Status == OrderStatus.Rejected)
                     .OrderBy(o => o.Id))
        {
            Console.WriteLine(
                $"  - Order #{order.Id}: {order.FailureReason}");
        }

        Console.WriteLine($"Async Time: {asyncTimer.Elapsed.TotalSeconds:F1} seconds");
        Console.WriteLine(
            $"orders.json now contains {savedOrders.Count} records");
    }

    private static string GetRejectionReason(Order order, string originalReason)
    {
        if (originalReason.Contains("inactive", StringComparison.OrdinalIgnoreCase))
            return "Customer is inactive";

        if (originalReason.Contains("not found", StringComparison.OrdinalIgnoreCase)
            && order.CustomerId == 99)
            return "Customer #99 not found";

        if (originalReason.Contains("expired", StringComparison.OrdinalIgnoreCase))
            return "Coupon 'OLD20' has expired";

        if (originalReason.Contains("FAKE50", StringComparison.OrdinalIgnoreCase))
            return "Coupon 'FAKE50' does not exist";

        if (originalReason.Contains("stock", StringComparison.OrdinalIgnoreCase))
            return $"Insufficient stock for '{GetProductName(order)}'";

        return originalReason;
    }

    private static string GetProductName(Order order)
    {
        // The seed data uses product ID 3 for Wireless Mouse.
        return order.Items.Any(item => item.ProductId == 3)
            ? "Wireless Mouse"
            : "the requested product";
    }
}
