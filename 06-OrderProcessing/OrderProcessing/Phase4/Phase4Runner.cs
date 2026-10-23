
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using OrderProcessing.Phase2;
using OrderProcessing.Phase3;
using OrderProcessing.Repositories;
using OrderProcessing.Services;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase4;

public sealed class Phase4Runner
{
    private sealed record CheckoutResult(
        int OrderId,
        OrderStatus Status,
        string? Reason,
        double ElapsedSeconds);

    public async Task RunAsync(CancellationToken ct)
    {
        var store = new JsonFileStore();
        var seeder = new DataSeeder(store);

        await seeder.ResetAsync(ct);

        var customerRepository = new CustomerRepository(store);
        var productRepository = new ProductRepository(store);
        var orderRepository = new OrderRepository(store, new[] { 13 });
        var couponRepository = new CouponRepository(store);
        var paymentRepository = new PaymentRepository(store);
        var emailRepository = new EmailRepository(store);

        var customerService = new CustomerService(customerRepository);
        var inventoryService = new InventoryService(productRepository);
        var discountService = new DiscountService(couponRepository);

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

        var orderProcessor = new OrderProcessor(
            orderRepository,
            customerService,
            inventoryService,
            pricingService,
            paymentGateway,
            emailService);

        var orders = await store.ReadAsync<Order>(
            DataFiles.IncomingOrders, ct);

        // Task 10.1 - Concurrent Order Processing
        Console.WriteLine();
        Console.WriteLine("========== TASK 10.1 - ORDER PROCESSING ==========");

        var channel = Channel.CreateUnbounded<CheckoutResult>();
        var stopwatch = Stopwatch.StartNew();

        Task displayTask = DisplayResultsAsync(
            channel.Reader,
            orders.Count);

        Task[] processingTasks = orders
            .Select(async order =>
            {
                var orderStopwatch = Stopwatch.StartNew();

                try
                {
                    await orderProcessor.ProcessOrderAsync(order, ct);

                    await channel.Writer.WriteAsync(
                        new CheckoutResult(
                            order.Id,
                            order.Status,
                            order.FailureReason,
                            orderStopwatch.Elapsed.TotalSeconds),
                        ct);
                }
                catch (OperationCanceledException)
                    when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    await channel.Writer.WriteAsync(
                        new CheckoutResult(
                            order.Id,
                            OrderStatus.Failed,
                            ex.Message,
                            orderStopwatch.Elapsed.TotalSeconds),
                        ct);
                }
            })
            .ToArray();

        try
        {
            await Task.WhenAll(processingTasks);
        }
        finally
        {
            channel.Writer.TryComplete();
        }

        await displayTask;
        stopwatch.Stop();

        List<Order> processedOrders =
            await orderRepository.GetAllAsync(ct);

        Console.WriteLine();
        Console.WriteLine("Order Processing Summary");
        Console.WriteLine("------------------------");
        Console.WriteLine($"Total orders: {processedOrders.Count}");
        Console.WriteLine(
            $"Completed: {processedOrders.Count(o => o.Status == OrderStatus.Completed)}");
        Console.WriteLine(
            $"Rejected: {processedOrders.Count(o => o.Status == OrderStatus.Rejected)}");
        Console.WriteLine(
            $"Payment failed: {processedOrders.Count(o => o.Status == OrderStatus.PaymentFailed)}");
        Console.WriteLine(
            $"Failed: {processedOrders.Count(o => o.Status == OrderStatus.Failed)}");
        Console.WriteLine(
            $"Total elapsed: {stopwatch.Elapsed.TotalSeconds:F1} s");

        // Task 10.2 - Customer Dashboard
        Console.WriteLine();
        Console.WriteLine("========== TASK 10.2 - CUSTOMER DASHBOARD ==========");

        var dashboardService = new CustomerDashboardService(
            customerRepository,
            orderRepository,
            paymentRepository,
            emailRepository);

        try
        {
            CustomerDashboard dashboard =
                await dashboardService.LoadAsync(3, ct);

            Console.WriteLine($"Customer: {dashboard.Customer.Name}");
            Console.WriteLine($"Orders: {dashboard.Orders.Count}");

            foreach (Order order in dashboard.Orders)
            {
                Console.WriteLine(
                    $"  Order #{order.Id}: {order.Status}");
            }

            Console.WriteLine($"Payments: {dashboard.Payments.Count}");
            Console.WriteLine($"Emails: {dashboard.Emails.Count}");
        }
        catch (AggregateException ex)
        {
            Console.WriteLine($"Dashboard error: {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("Testing customer #99 (expected errors)...");

        var failingDashboardService = new CustomerDashboardService(
            customerRepository,
            orderRepository,
            paymentRepository,
            emailRepository,
            failPaymentsForCustomerIds: new[] { 99 });

        try
        {
            await failingDashboardService.LoadAsync(99, ct);
        }
        catch (AggregateException)
        {
            Console.WriteLine("Both expected errors were handled.");
        }

        Console.WriteLine("====================================================");

        // Task 10.3 - Fastest Shipping Quote
        Console.WriteLine();
        Console.WriteLine("========== TASK 10.3 - SHIPPING QUOTES ==========");

        List<Product> shippingProducts =
            await productRepository.GetAllAsync(ct);

        var shippingCarriers = new List<IShippingCarrier>
        {
            new FastShip(shippingProducts),
            new CairoExpress(shippingProducts),
            new NileCourier(shippingProducts)
        };

        var shippingQuoteService =
            new ShippingQuoteService(shippingCarriers);

        foreach (int orderId in Enumerable.Range(1, 5))
        {
            ct.ThrowIfCancellationRequested();

            Order? order = orders.FirstOrDefault(
                o => o.Id == orderId);

            if (order is null)
            {
                Console.WriteLine($"Order #{orderId} not found.");
                continue;
            }

            try
            {
                var quote = await shippingQuoteService
                    .GetFastestQuoteAsync(order, ct);

                Console.WriteLine(
                    $"Order #{order.Id}: Carrier={quote.Carrier}, " +
                    $"Price={quote.Price:F2}, " +
                    $"Time={quote.ElapsedMs} ms");
            }
            catch (ShippingProviderUnavailableException ex)
            {
                Console.WriteLine(
                    $"Order #{order.Id}: {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            "Testing Order #6 (all carriers should fail)...");

        Order? order6 = orders.FirstOrDefault(o => o.Id == 6);

        if (order6 is not null)
        {
            var failingCarriers = new List<IShippingCarrier>
            {
                new FastShip(shippingProducts, new[] { 6 }),
                new CairoExpress(shippingProducts, new[] { 6 }),
                new NileCourier(shippingProducts, new[] { 6 })
            };

            var failingShippingService =
                new ShippingQuoteService(failingCarriers);

            try
            {
                var quote = await failingShippingService
                    .GetFastestQuoteAsync(order6, ct);

                Console.WriteLine(
                    $"Order #6: Carrier={quote.Carrier}, " +
                    $"Price={quote.Price:F2}");
            }
            catch (ShippingProviderUnavailableException ex)
            {
                Console.WriteLine(
                    $"Expected failure: {ex.Message}");
            }
        }

        Console.WriteLine("=================================================");

        // Task 10.4 A - Fraud Scoring
        Console.WriteLine();
        Console.WriteLine("========== TASK 10.4 A - FRAUD SCORING ==========");

        const int totalOrders = 10_000;

        var sequentialStopwatch = Stopwatch.StartNew();
        var sequentialFlaggedIds = new List<int>();

        for (int orderId = 1; orderId <= totalOrders; orderId++)
        {
            ct.ThrowIfCancellationRequested();

            if (Score(orderId) >= 90)
            {
                sequentialFlaggedIds.Add(orderId);
            }
        }

        sequentialStopwatch.Stop();

        var parallelStopwatch = Stopwatch.StartNew();
        var parallelFlaggedIds = new ConcurrentBag<int>();
        var threadIds = new ConcurrentDictionary<int, byte>();

        await Parallel.ForEachAsync(
            Enumerable.Range(1, totalOrders),
            new ParallelOptions
            {
                CancellationToken = ct,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            (orderId, token) =>
            {
                token.ThrowIfCancellationRequested();

                threadIds.TryAdd(
                    Environment.CurrentManagedThreadId, 0);

                if (Score(orderId) >= 90)
                {
                    parallelFlaggedIds.Add(orderId);
                }

                return ValueTask.CompletedTask;
            });

        parallelStopwatch.Stop();

        List<int> sortedParallelIds = parallelFlaggedIds
            .OrderBy(id => id)
            .ToList();

        bool sameResults = sequentialFlaggedIds
            .SequenceEqual(sortedParallelIds);

        double speedUp =
            sequentialStopwatch.Elapsed.TotalMilliseconds /
            Math.Max(
                parallelStopwatch.Elapsed.TotalMilliseconds,
                0.001);

        Console.WriteLine(
            $"Sequential flagged count: {sequentialFlaggedIds.Count}");
        Console.WriteLine(
            $"Parallel flagged count: {sortedParallelIds.Count}");
        Console.WriteLine(
            $"Sequential time: {sequentialStopwatch.ElapsedMilliseconds} ms");
        Console.WriteLine(
            $"Parallel time: {parallelStopwatch.ElapsedMilliseconds} ms");
        Console.WriteLine($"Speed-up: {speedUp:F2}x");
        Console.WriteLine($"Distinct threads used: {threadIds.Count}");
        Console.WriteLine(
            $"Environment.ProcessorCount: {Environment.ProcessorCount}");
        Console.WriteLine($"Same IDs in both runs: {sameResults}");
        Console.WriteLine(
            $"First five flagged IDs: {string.Join(", ", sortedParallelIds.Take(5))}");
        Console.WriteLine("=================================================");

        // Task 10.4 B - Email Re-send
        Console.WriteLine();
        Console.WriteLine("========== TASK 10.4 B - EMAIL RE-SEND ==========");

        const int totalTestEmails = 30;
        const int firstTestOrderId = 1001;

        // No injected failures for this test.
        IEmailService resendEmailService =
            new EmailService(emailRepository);

        int currentlySending = 0;
        int highestConcurrentSends = 0;

        var emailStopwatch = Stopwatch.StartNew();

        await Parallel.ForEachAsync(
            Enumerable.Range(firstTestOrderId, totalTestEmails),
            new ParallelOptions
            {
                CancellationToken = ct,
                MaxDegreeOfParallelism = 4
            },
            async (orderId, token) =>
            {
                int current = Interlocked.Increment(
                    ref currentlySending);

                UpdateMaximum(
                    ref highestConcurrentSends,
                    current);

                try
                {
                    var testOrder = new Order
                    {
                        Id = orderId,
                        CustomerId = 1,
                        TotalAmount = 0m,
                        Status = OrderStatus.Completed,
                        CreatedAt = DateTime.Now
                    };

                    var testCustomer = new Customer
                    {
                        Id = 1,
                        Name = "Email Re-send Test",
                        Email = $"test-order-{orderId}@example.com",
                        IsActive = true
                    };

                    await resendEmailService.SendConfirmationAsync(
                        testOrder,
                        testCustomer,
                        token);
                }
                finally
                {
                    Interlocked.Decrement(ref currentlySending);
                }
            });

        emailStopwatch.Stop();

        // Verify that every test order has exactly one saved email.
        List<EmailMessage> savedEmails =
            await emailRepository.GetAllAsync(ct);

        List<EmailMessage> testEmails = savedEmails
            .Where(email =>
                email.OrderId >= firstTestOrderId &&
                email.OrderId < firstTestOrderId + totalTestEmails)
            .ToList();

        bool eachEmailSavedOnce = Enumerable
            .Range(firstTestOrderId, totalTestEmails)
            .All(orderId =>
                testEmails.Count(email => email.OrderId == orderId) == 1);

        Console.WriteLine($"Test emails requested: {totalTestEmails}");
        Console.WriteLine($"Test emails saved: {testEmails.Count}");
        Console.WriteLine(
            $"Highest concurrent sends: {highestConcurrentSends}");
        Console.WriteLine(
            $"All emails saved exactly once: {eachEmailSavedOnce}");
        Console.WriteLine(
            $"Email re-send total time: {emailStopwatch.Elapsed.TotalSeconds:F2} s");
        Console.WriteLine("=================================================");
    }

    private static int Score(int orderId)
    {
        long h = orderId;

        for (int i = 0; i < 100_000; i++)
        {
            h = (h * 31 + i) % 1_000_000_007;
        }

        return (int)(h % 100);
    }

    private static void UpdateMaximum(
        ref int maximum,
        int candidate)
    {
        int currentMaximum;

        do
        {
            currentMaximum = Volatile.Read(ref maximum);

            if (candidate <= currentMaximum)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(
            ref maximum,
            candidate,
            currentMaximum) != currentMaximum);
    }

    private static async Task DisplayResultsAsync(
        ChannelReader<CheckoutResult> reader,
        int totalOrders)
    {
        int count = 0;

        await foreach (CheckoutResult result in reader.ReadAllAsync())
        {
            count++;

            Console.WriteLine(
                $"[{count}/{totalOrders}] Order #{result.OrderId}: " +
                $"{result.Status} ({result.ElapsedSeconds:F2}s)" +
                (string.IsNullOrWhiteSpace(result.Reason)
                    ? string.Empty
                    : $" - {result.Reason}"));
        }
    }
}
