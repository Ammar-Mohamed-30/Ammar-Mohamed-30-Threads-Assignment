
namespace OrderProcessing.Shared.Exceptions;

public class CustomerNotFoundException : Exception
{
    public CustomerNotFoundException(int customerId)
        : base($"Customer with ID {customerId} was not found.")
    {
    }
}
