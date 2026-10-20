using System.Text.Json;
using OrderProcessing.Repositories;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public sealed class ReportGenerator
{
	private readonly OrderRepository _orderRepository;
	private readonly ProductRepository _productRepository;

	public ReportGenerator(
		OrderRepository orderRepository,
		ProductRepository productRepository)
	{
		_orderRepository = orderRepository;
		_productRepository = productRepository;
	}

	public async Task<Report> GenerateAsync(CancellationToken ct)
	{
		List<Order> orders = await _orderRepository.GetAllAsync(ct);

		// Load products through the repository as required by the assignment.
		List<Product> products = await _productRepository.GetAllAsync(ct);

		List<Order> completedOrders = orders
			.Where(order => order.Status == OrderStatus.Completed)
			.ToList();

		decimal totalRevenue = completedOrders.Sum(
			order => order.TotalAmount);

		int completedCount = completedOrders.Count;

		var quantitiesByProduct = completedOrders
			.SelectMany(order => order.Items)
			.GroupBy(item => item.ProductId)
			.Select(group => new
			{
				ProductId = group.Key,
				Quantity = group.Sum(item => item.Quantity)
			})
			.OrderByDescending(item => item.Quantity)
			.ThenBy(item => item.ProductId)
			.FirstOrDefault();

		string topSellingProduct = quantitiesByProduct is null
			? "N/A"
			: products.FirstOrDefault(
				product => product.Id == quantitiesByProduct.ProductId)?.Name
			  ?? $"Product #{quantitiesByProduct.ProductId}";

		var report = new Report
		{
			TotalOrders = orders.Count,
			Completed = orders.Count(
				order => order.Status == OrderStatus.Completed),
			Rejected = orders.Count(
				order => order.Status == OrderStatus.Rejected),
			Failed = orders.Count(
				order => order.Status == OrderStatus.Failed),
			PaymentFailed = orders.Count(
				order => order.Status == OrderStatus.PaymentFailed),
			TotalRevenue = totalRevenue,
			AverageOrderValue = completedCount == 0
				? 0
				: Math.Round(
					totalRevenue / completedCount,
					2,
					MidpointRounding.AwayFromZero),
			TopSellingProduct = topSellingProduct
		};

		string reportDirectory = Path.Combine(
			DataFiles.DataDirectory,
			"reports");

		Directory.CreateDirectory(reportDirectory);

		string fileName =
			$"report_{DateTime.Now:yyyyMMdd_HHmmss}.json";

		string filePath = Path.Combine(reportDirectory, fileName);

		await using FileStream stream = new(
			filePath,
			FileMode.Create,
			FileAccess.Write,
			FileShare.None,
			4096,
			FileOptions.Asynchronous);

		await JsonSerializer.SerializeAsync(
			stream,
			report,
			JsonOptions.Default,
			ct);

		await stream.FlushAsync(ct);

		return report;
	}
}

public sealed class Report
{
	public int TotalOrders { get; set; }
	public int Completed { get; set; }
	public int Rejected { get; set; }
	public int Failed { get; set; }
	public int PaymentFailed { get; set; }
	public decimal TotalRevenue { get; set; }
	public decimal AverageOrderValue { get; set; }
	public string TopSellingProduct { get; set; } = string.Empty;
}
