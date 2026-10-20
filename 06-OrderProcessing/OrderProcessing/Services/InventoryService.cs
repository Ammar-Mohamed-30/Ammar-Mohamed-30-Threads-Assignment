
using OrderProcessing.Repositories;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Services;

public class InventoryService
{
    private readonly ProductRepository _productRepository;

    public InventoryService(ProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<bool> ReserveStockAsync(
        Order order,
        CancellationToken ct)
    {
        Log.Info($"InventoryService -> Reserving stock for Order #{order.Id}...");

        await Task.Delay(100, ct);

        bool reserved = await _productRepository.ReserveStockAsync(order, ct);

        if (!reserved)
        {
            Log.Error($"InventoryService -> Insufficient stock for Order #{order.Id}.");
            return false;
        }

        Log.Success($"InventoryService -> Stock reserved for Order #{order.Id}.");
        return true;
    }

    public async Task ReleaseStockAsync(
        Order order,
        CancellationToken ct)
    {
        Log.Info($"InventoryService -> Releasing stock for Order #{order.Id}...");

        await Task.Delay(100, ct);

        await _productRepository.ReleaseStockAsync(order, ct);

        Log.Success($"InventoryService -> Stock released for Order #{order.Id}.");
    }
}
