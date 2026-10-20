
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Repositories;

public class ProductRepository
{
    private readonly JsonFileStore _store;

    public ProductRepository(JsonFileStore store)
    {
        _store = store;
    }

    public async Task<Product?> GetByIdAsync(
        int productId,
        CancellationToken ct)
    {
        Log.Info(
            $"ProductRepository -> Opening connection for Product #{productId}...");

        await Task.Delay(200, ct);

        Log.Info(
            $"ProductRepository -> Executing query for Product #{productId}...");

        await Task.Delay(Random.Shared.Next(300, 601), ct);

        List<Product> products =
            await _store.ReadAsync<Product>(DataFiles.Products, ct);

        Product? product = products.FirstOrDefault(
            p => p.Id == productId);

        if (product is null)
        {
            Log.Error(
                $"ProductRepository -> Product #{productId} not found.");
        }
        else
        {
            Log.Success(
                $"ProductRepository -> Product #{productId} found ({product.Name}).");
        }

        return product;
    }

    public async Task<List<Product>> GetAllAsync(
        CancellationToken ct)
    {
        Log.Info("ProductRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info("ProductRepository -> Executing products query...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        List<Product> products =
            await _store.ReadAsync<Product>(DataFiles.Products, ct);

        Log.Success(
            $"ProductRepository -> Retrieved {products.Count} products.");

        return products;
    }

    public async Task<bool> ReserveStockAsync(
        Order order,
        CancellationToken ct)
    {
        Log.Info(
            $"ProductRepository -> Opening connection to reserve stock for Order #{order.Id}...");

        await Task.Delay(200, ct);

        Log.Info(
            $"ProductRepository -> Beginning stock reservation transaction for Order #{order.Id}...");

        await Task.Delay(100, ct);

        bool reserved = await _store.UpdateAsync<Product, bool>(
            DataFiles.Products,
            products =>
            {
                foreach (OrderItem item in order.Items)
                {
                    Product? product = products.FirstOrDefault(
                        p => p.Id == item.ProductId);

                    if (product is null ||
                        product.StockQuantity < item.Quantity ||
                        item.Quantity <= 0)
                    {
                        return false;
                    }
                }

                foreach (OrderItem item in order.Items)
                {
                    Product product = products.First(
                        p => p.Id == item.ProductId);

                    product.StockQuantity -= item.Quantity;
                }

                return true;
            },
            ct);

        Log.Info(
            $"ProductRepository -> Committing stock reservation for Order #{order.Id}...");

        await Task.Delay(200, ct);

        if (reserved)
        {
            Log.Success(
                $"ProductRepository -> Stock reserved for Order #{order.Id}.");
        }
        else
        {
            Log.Error(
                $"ProductRepository -> Stock reservation failed for Order #{order.Id}.");
        }

        return reserved;
    }

    public async Task ReleaseStockAsync(
        Order order,
        CancellationToken ct)
    {
        Log.Info(
            $"ProductRepository -> Opening connection to release stock for Order #{order.Id}...");

        await Task.Delay(200, ct);

        Log.Info(
            $"ProductRepository -> Beginning stock release transaction for Order #{order.Id}...");

        await Task.Delay(100, ct);

        await _store.UpdateAsync<Product, bool>(
            DataFiles.Products,
            products =>
            {
                foreach (OrderItem item in order.Items)
                {
                    Product? product = products.FirstOrDefault(
                        p => p.Id == item.ProductId);

                    if (product is not null)
                    {
                        product.StockQuantity += item.Quantity;
                    }
                }

                return true;
            },
            ct);

        Log.Info(
            $"ProductRepository -> Committing stock release for Order #{order.Id}...");

        await Task.Delay(200, ct);

        Log.Success(
            $"ProductRepository -> Stock released for Order #{order.Id}.");
    }
}
