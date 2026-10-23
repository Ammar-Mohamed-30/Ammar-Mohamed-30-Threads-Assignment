
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase4;

public sealed class CustomerDashboard
{
	public Customer Customer { get; init; } = null!;

	public List<Order> Orders { get; init; } = new();

	public List<PaymentTransaction> Payments { get; init; } = new();

	public List<EmailMessage> Emails { get; init; } = new();
}
