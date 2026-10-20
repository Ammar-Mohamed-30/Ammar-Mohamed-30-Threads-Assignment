
namespace OrderProcessing.Shared.Exceptions;

public class ShippingProviderUnavailableException : Exception
{
    public ShippingProviderUnavailableException(string providerName)
        : base($"Shipping provider '{providerName}' is unavailable.")
    {
    }
}
