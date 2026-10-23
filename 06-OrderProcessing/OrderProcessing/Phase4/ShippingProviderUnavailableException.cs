
namespace OrderProcessing.Phase4;

public sealed class ShippingProviderUnavailableException : Exception
{
    public ShippingProviderUnavailableException(string message)
        : base(message)
    {
    }
}
