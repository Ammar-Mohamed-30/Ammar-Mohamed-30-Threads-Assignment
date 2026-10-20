
using OrderProcessing.Repositories;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Services;

public class CustomerService
{
    private readonly CustomerRepository _customerRepository;

    public CustomerService(CustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<Customer> GetCustomerAsync(
        int customerId,
        CancellationToken ct)
    {
        Log.Info(
            $"CustomerService -> Starting validation for Customer #{customerId}...");

        await Task.Delay(100, ct);

        Log.Info(
            $"CustomerService -> Loading Customer #{customerId}...");

        await Task.Delay(100, ct);

        Customer? customer = await _customerRepository.GetByIdAsync(
            customerId,
            ct);

        if (customer is null)
        {
            string message = $"Customer #{customerId} not found";

            Log.Error($"CustomerService -> {message}.");

            throw new CustomerNotFoundException(customerId);
        }

        if (!customer.IsActive)
        {
            string message = $"Customer #{customerId} is inactive";

            Log.Error($"CustomerService -> {message}.");

            throw new InvalidOperationException(message);
        }

        Log.Success(
            $"CustomerService -> Customer #{customerId} validated successfully.");

        return customer;
    }
}
