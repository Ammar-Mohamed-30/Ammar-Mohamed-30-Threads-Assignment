using OrderProcessing.Repositories;
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
            Log.Error(
                $"CustomerService -> Customer #{customerId} not found.");

            throw new InvalidOperationException(
                $"Customer #{customerId} not found.");
        }

        if (!customer.IsActive)
        {
            Log.Error(
                $"CustomerService -> Customer #{customerId} is inactive.");

            throw new InvalidOperationException(
                $"Customer #{customerId} is inactive.");
        }

        Log.Success(
            $"CustomerService -> Customer #{customerId} validated successfully.");

        return customer;
    }
}