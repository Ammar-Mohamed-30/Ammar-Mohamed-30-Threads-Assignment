
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Repositories;

public class CustomerRepository
{
    private readonly JsonFileStore _store;

    public CustomerRepository(JsonFileStore store)
    {
        _store = store;
    }

    public async Task<Customer?> GetByIdAsync(
        int customerId,
        CancellationToken ct)
    {
        Log.Info(
            $"CustomerRepository -> Opening connection for Customer #{customerId}...");

        await Task.Delay(200, ct);

        Log.Info(
            $"CustomerRepository -> Executing query for Customer #{customerId}...");

        await Task.Delay(Random.Shared.Next(300, 601), ct);

        List<Customer> customers =
            await _store.ReadAsync<Customer>(DataFiles.Customers, ct);

        Customer? customer = customers.FirstOrDefault(
            c => c.Id == customerId);

        if (customer is null)
        {
            Log.Error(
                $"CustomerRepository -> Customer #{customerId} not found.");
        }
        else
        {
            Log.Success(
                $"CustomerRepository -> Customer #{customerId} found ({customer.Name}).");
        }

        return customer;
    }
}
