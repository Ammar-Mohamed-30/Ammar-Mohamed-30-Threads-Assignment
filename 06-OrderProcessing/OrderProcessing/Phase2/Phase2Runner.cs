
using System.Diagnostics;
using OrderProcessing.Repositories;
using OrderProcessing.Services;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;
using OrderProcessing.Phase2;

namespace OrderProcessing.Phase2;

public sealed class Phase2Runner
{
    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine("========== PHASE 2 ==========");

        var store = new JsonFileStore();
        var seeder = new DataSeeder(store);

        await seeder.ResetAsync(ct);

        var customerRepository = new CustomerRepository(store);
        var productRepository = new ProductRepository(store);
        var orderRepository = new OrderRepository(store);
        var couponRepository = new CouponRepository(store);

        var customerService = new CustomerService(customerRepository);
        var inventoryService = new InventoryService(productRepository);
        var discountService = new DiscountService(couponRepository);

        var validationShippingService =
            new ShippingService(Array.Empty<int>());

        var validationPricingService = new PricingService(
            productRepository,
            discountService,
            validationShippingService,
            customerRepository);

        var processingShippingService =
            new ShippingService(new[] { 7 });

        var processingPricingService = new PricingService(
            productRepository,
            discountService,
            processingShippingService,
            customerRepository);

        var orderPricer = new OrderPricer(
            processingPricingService,
            discountService,
            processingShippingService,
            productRepository,
            customerRepository);

        var invoiceGenerator = new InvoiceGenerator();

        var reportGenerator = new ReportGenerator(
            orderRepository,
            productRepository);

        var processor = new OrderProcessorV2(
            inventoryService,
            orderPricer,
            invoiceGenerator,
            orderRepository);

        var incomingOrders = await store.ReadAsync<Order>(
            DataFiles.IncomingOrders,
            ct);

        Console.WriteLine($"Total incoming orders: {incomingOrders.Count}");

        if (incomingOrders.Count > 0)
        {
            var sampleOrder = incomingOrders[0];

            var sequentialTimer = Stopwatch.StartNew();
            await orderPricer.PriceOrderSequentialAsync(sampleOrder, ct);
            sequentialTimer.Stop();

            var parallelTimer = Stopwatch.StartNew();
            await orderPricer.PriceOrderAsync(sampleOrder, ct);
            parallelTimer.Stop();

            Console.WriteLine(
                $"Sequential pricing: {sequentialTimer.ElapsedMilliseconds} ms");

            Console.WriteLine(
                $"Parallel pricing: {parallelTimer.ElapsedMilliseconds} ms");
        }

        var validationTasks = incomingOrders.Select(order =>
            ValidateOrderAsync(
                order,
                customerService,
                inventoryService,
                validationPricingService,
                orderRepository,
                ct)).ToList();

        await Task.WhenAll(validationTasks);

        var savedOrders = await orderRepository.GetAllAsync(ct);

        var validatedOrders = savedOrders
            .Where(order => order.Status == OrderStatus.Validated)
            .ToList();

        Console.WriteLine(
            $"Orders ready for processing: {validatedOrders.Count}");

        var processingTasks = validatedOrders
            .Select(order => processor.ProcessValidatedOrderAsync(order, ct))
            .ToList();

        for (int i = 0; i < processingTasks.Count; i++)
        {
            Order order = validatedOrders[i];

            try
            {
                await processingTasks[i];
            }
            catch (OperationCanceledException)
            {
                order.Status = OrderStatus.Pending;
                order.FailureReason = "Cancelled";

                await orderRepository.SaveAsync(
                    order,
                    CancellationToken.None);

                throw;
            }
            catch (Exception ex)
            {
                try
                {
                    await inventoryService.ReleaseStockAsync(
                        order,
                        CancellationToken.None);
                }
                catch (Exception releaseException)
                {
                    Console.WriteLine(
                        $"Warning: stock release failed for order " +
                        $"#{order.Id}: {releaseException.Message}");
                }

                order.Status = OrderStatus.Failed;
                order.FailureReason = ex.Message;

                await orderRepository.SaveAsync(
                    order,
                    CancellationToken.None);

                Console.WriteLine(
                    $"Order #{order.Id} failed: {ex.Message}");
            }
        }

        var finalOrders = await orderRepository.GetAllAsync(
            CancellationToken.None);

        Report report = await reportGenerator.GenerateAsync(
            CancellationToken.None);

        Console.WriteLine();
        Console.WriteLine("========== PHASE 2 SUMMARY ==========");
        Console.WriteLine($"Total orders: {report.TotalOrders}");
        Console.WriteLine($"Completed: {report.Completed}");
        Console.WriteLine($"Rejected: {report.Rejected}");
        Console.WriteLine($"Failed: {report.Failed}");
        Console.WriteLine($"Payment failed: {report.PaymentFailed}");
        Console.WriteLine($"Total revenue: {report.TotalRevenue:F2}");
        Console.WriteLine($"Average order value: {report.AverageOrderValue:F2}");
        Console.WriteLine($"Top selling product: {report.TopSellingProduct}");
        Console.WriteLine("=====================================");
    }

    private static async Task ValidateOrderAsync(
        Order order,
        CustomerService customerService,
        InventoryService inventoryService,
        PricingService pricingService,
        OrderRepository orderRepository,
        CancellationToken ct)
    {
        bool stockReserved = false;

        try
        {
            await customerService.GetCustomerAsync(
                order.CustomerId,
                ct);

            bool reserved = await inventoryService.ReserveStockAsync(
                order,
                ct);

            if (!reserved)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason = "Insufficient stock";

                await orderRepository.SaveAsync(order, ct);
                return;
            }

            stockReserved = true;

            await pricingService.CalculateTotalAsync(order, ct);

            order.Status = OrderStatus.Validated;
            order.FailureReason = null;

            await orderRepository.SaveAsync(order, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            order.Status = OrderStatus.Rejected;
            order.FailureReason = ex.Message;

            await orderRepository.SaveAsync(
                order,
                CancellationToken.None);
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
}
