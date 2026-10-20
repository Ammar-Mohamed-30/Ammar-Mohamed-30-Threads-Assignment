   
using System.Text;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public sealed class InvoiceGenerator
{
    public async Task GenerateAsync(
        Order order,
        CancellationToken ct)
    {
        // PDF rendering is simulated CPU-bound work.
        string invoiceContent = await Task.Run(() =>
        {
            long result = 0;

            for (int i = 0; i < 20_000_000; i++)
            {
                result += (long)i * (i % 7);
            }

            var builder = new StringBuilder();

            builder.AppendLine($"Invoice #{order.Id}");
            builder.AppendLine($"Customer ID: {order.CustomerId}");
            builder.AppendLine($"Subtotal: {order.SubTotal:F2}");
            builder.AppendLine($"Discount: {order.Discount:F2}");
            builder.AppendLine($"Shipping: {order.ShippingCost:F2}");
            builder.AppendLine($"Total: {order.TotalAmount:F2}");
            builder.AppendLine($"Render checksum: {result}");

            return builder.ToString();
        }, ct);

        ct.ThrowIfCancellationRequested();

        string invoiceDirectory = Path.Combine(
            DataFiles.DataDirectory,
            "invoices");

        Directory.CreateDirectory(invoiceDirectory);

        string filePath = Path.Combine(
            invoiceDirectory,
            $"invoice_{order.Id}.txt");

        await File.WriteAllTextAsync(
            filePath,
            invoiceContent,
            ct);
    }
}
