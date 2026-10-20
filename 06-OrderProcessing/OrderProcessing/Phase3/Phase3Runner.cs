
using OrderProcessing.Repositories;
using OrderProcessing.Services;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;
using OrderProcessing.Phase2;

namespace OrderProcessing.Phase3;

public sealed class Phase3Runner
{
    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine("========== PHASE 3 ==========");

        var store = new JsonFileStore();
        var seeder = new DataSeeder(store);

        await seeder.ResetAsync(ct);

        var customerRepository = new CustomerRepository(store);
        var productRepository = new ProductRepository(store);

        var orderRepository = new OrderRepository(
            store,
            new[] { 13 });

        var couponRepository = new CouponRepository(store);
        var paymentRepository = new PaymentRepository(store);
        var emailRepository = new EmailRepository(store);

        var customerService = new CustomerService(
            customerRepository);

        var inventoryService = new InventoryService(
            productRepository);

        var discountService = new DiscountService(
            couponRepository);

        var shippingService = new ShippingService(
            Array.Empty<int>());

        var pricingService = new PricingService(
            productRepository,
            discountService,
            shippingService,
            customerRepository);

        var paymentGateway = new PaymentService(
            paymentRepository,
            timeoutOrderIds: new[] { 5 },
            declineOrderIds: new[] { 8 });

        var emailService = new EmailService(
            emailRepository,
            new Dictionary<int, int>
            {
                [3] = 2,
                [17] = int.MaxValue
            });

        var processor = new OrderProcessor(
            orderRepository,
            customerService,
            inventoryService,
            pricingService,
            paymentGateway,
            emailService);

        var incomingOrders = await store.ReadAsync<Order>(
            DataFiles.IncomingOrders,
            ct);

        Console.WriteLine(
            $"Total incoming orders: {incomingOrders.Count}");

        // Start processing all orders concurrently.
        List<Task<bool>> processingTasks = incomingOrders
            .Select(order => processor.ProcessOrderAsync(order, ct))
            .ToList();

        try
        {
            await Task.WhenAll(processingTasks);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine(
                "Cancellation requested. Waiting for order tasks to finish.");

            throw;
        }

        var finalOrders = await orderRepository.GetAllAsync(
            CancellationToken.None);

        Console.WriteLine();
        Console.WriteLine("========== PHASE 3 RESULTS ==========");
        Console.WriteLine(
            $"{"Order",-10} {"Status",-15} {"Total",-12} Reason");
        Console.WriteLine(new string('-', 85));

        foreach (Order order in finalOrders.OrderBy(o => o.Id))
        {
            Console.WriteLine(
                $"#{order.Id,-9} {order.Status,-15} " +
                $"{order.TotalAmount,-12:F2} {order.FailureReason ?? "-"}");
        }

        Console.WriteLine(new string('-', 85));
        Console.WriteLine($"Total orders: {finalOrders.Count}");
        Console.WriteLine(
            $"Completed: {finalOrders.Count(o => o.Status == OrderStatus.Completed)}");
        Console.WriteLine(
            $"Rejected: {finalOrders.Count(o => o.Status == OrderStatus.Rejected)}");
        Console.WriteLine(
            $"Payment failed: {finalOrders.Count(o => o.Status == OrderStatus.PaymentFailed)}");
        Console.WriteLine(
            $"Failed: {finalOrders.Count(o => o.Status == OrderStatus.Failed)}");

        Console.WriteLine("=====================================");
    }
}
