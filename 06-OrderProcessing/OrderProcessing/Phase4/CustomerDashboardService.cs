
using System.Diagnostics;
using OrderProcessing.Repositories;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase4;

public sealed class CustomerDashboardService
{
    private readonly CustomerRepository _customerRepository;
    private readonly OrderRepository _orderRepository;
    private readonly PaymentRepository _paymentRepository;
    private readonly EmailRepository _emailRepository;
    private readonly HashSet<int> _failPaymentsForCustomerIds;

    public CustomerDashboardService(
        CustomerRepository customerRepository,
        OrderRepository orderRepository,
        PaymentRepository paymentRepository,
        EmailRepository emailRepository,
        IEnumerable<int>? failPaymentsForCustomerIds = null)
    {
        _customerRepository = customerRepository;
        _orderRepository = orderRepository;
        _paymentRepository = paymentRepository;
        _emailRepository = emailRepository;

        _failPaymentsForCustomerIds = new HashSet<int>(
            failPaymentsForCustomerIds ?? Array.Empty<int>());
    }

    public async Task<CustomerDashboard> LoadAsync(
        int customerId,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        Task<Customer?> customerTask = LoadCustomerAsync(customerId, ct);
        Task<List<Order>> ordersTask = LoadOrdersAsync(ct);
        Task<List<PaymentTransaction>> paymentsTask =
            LoadPaymentsAsync(customerId, ct);
        Task<List<EmailMessage>> emailsTask = LoadEmailsAsync(ct);

        Task[] tasks =
        {
            customerTask,
            ordersTask,
            paymentsTask,
            emailsTask
        };

        try
        {
            await Task.WhenAll(tasks);
        }
        catch
        {
            Console.WriteLine(
                $"Customer #{customerId} dashboard encountered errors:");

            foreach (Task task in tasks)
            {
                if (task.Exception is not null)
                {
                    foreach (Exception exception
                             in task.Exception.Flatten().InnerExceptions)
                    {
                        Console.WriteLine($"- {exception.Message}");
                    }
                }
            }

            throw new AggregateException(
                $"Failed to load Customer #{customerId} dashboard.",
                tasks
                    .Where(task => task.Exception is not null)
                    .SelectMany(task =>
                        task.Exception!.Flatten().InnerExceptions));
        }

        stopwatch.Stop();

        Customer customer = (await customerTask)!;
        List<Order> allOrders = await ordersTask;
        List<PaymentTransaction> allPayments = await paymentsTask;
        List<EmailMessage> allEmails = await emailsTask;

        HashSet<int> customerOrderIds = allOrders
            .Where(order => order.CustomerId == customerId)
            .Select(order => order.Id)
            .ToHashSet();

        var dashboard = new CustomerDashboard
        {
            Customer = customer,
            Orders = allOrders
                .Where(order => order.CustomerId == customerId)
                .ToList(),
            Payments = allPayments
                .Where(payment => customerOrderIds.Contains(payment.OrderId))
                .ToList(),
            Emails = allEmails
                .Where(email => customerOrderIds.Contains(email.OrderId))
                .ToList()
        };

        Console.WriteLine(
            $"Customer #{customerId} dashboard loaded in " +
            $"{stopwatch.Elapsed.TotalSeconds:F2} seconds.");

        return dashboard;
    }

    private async Task<Customer?> LoadCustomerAsync(
        int customerId,
        CancellationToken ct)
    {
        Customer? customer = await _customerRepository.GetByIdAsync(
            customerId, ct);

        if (customer is null)
        {
            throw new InvalidOperationException(
                $"Customer #{customerId} not found");
        }

        return customer;
    }

    private async Task<List<Order>> LoadOrdersAsync(
        CancellationToken ct)
    {
        return await _orderRepository.GetAllAsync(ct);
    }

    private async Task<List<PaymentTransaction>> LoadPaymentsAsync(
        int customerId,
        CancellationToken ct)
    {
        if (_failPaymentsForCustomerIds.Contains(customerId))
        {
            await Task.Delay(200, ct);
            throw new TimeoutException("Payments store timeout");
        }

        return await _paymentRepository.GetAllAsync(ct);
    }

    private async Task<List<EmailMessage>> LoadEmailsAsync(
        CancellationToken ct)
    {
        return await _emailRepository.GetAllAsync(ct);
    }
}
