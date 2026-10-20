
using OrderProcessing.Repositories;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public sealed class RaceConditionDemo
{
    private readonly JsonFileStore _store;
    private readonly ProductRepository _productRepository;

    public RaceConditionDemo(
        JsonFileStore store,
        ProductRepository productRepository)
    {
        _store = store;
        _productRepository = productRepository;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("===== Race Condition Demo =====");

        List<Product> products =
            await _store.ReadAsync<Product>(DataFiles.Products, ct);

        Product? keyboard = products.FirstOrDefault(p => p.Id == 4);

        if (keyboard is null)
        {
            Console.WriteLine("Keyboard product #4 was not found.");
            return;
        }

        keyboard.StockQuantity = 1;
        await _store.WriteAsync(DataFiles.Products, products, ct);

        Console.WriteLine("Unsafe reservations: starting both tasks...");

        Task<bool> first = ReserveStockUnsafeAsync(4, 1, ct);
        Task<bool> second = ReserveStockUnsafeAsync(4, 1, ct);

        bool[] unsafeResults = await Task.WhenAll(first, second);

        Console.WriteLine(
            $"Unsafe results: Order #10 = {unsafeResults[0]}, " +
            $"Order #11 = {unsafeResults[1]}");

        products = await _store.ReadAsync<Product>(DataFiles.Products, ct);
        keyboard = products.First(p => p.Id == 4);

        Console.WriteLine(
            $"Stock after unsafe reservations: {keyboard.StockQuantity}");

        // Reset stock before the safe test.
        keyboard.StockQuantity = 1;
        await _store.WriteAsync(DataFiles.Products, products, ct);

        Console.WriteLine();
        Console.WriteLine("Safe reservations: starting both tasks...");

        var order10 = CreateKeyboardOrder(10);
        var order11 = CreateKeyboardOrder(11);

        Task<bool> safeFirst =
            _productRepository.ReserveStockAsync(order10, ct);

        Task<bool> safeSecond =
            _productRepository.ReserveStockAsync(order11, ct);

        bool[] safeResults = await Task.WhenAll(safeFirst, safeSecond);

        Console.WriteLine(
            $"Safe results: Order #10 = {safeResults[0]}, " +
            $"Order #11 = {safeResults[1]}");

        products = await _store.ReadAsync<Product>(DataFiles.Products, ct);
        keyboard = products.First(p => p.Id == 4);

        Console.WriteLine(
            $"Stock after safe reservations: {keyboard.StockQuantity}");
    }

    private async Task<bool> ReserveStockUnsafeAsync(
        int productId,
        int quantity,
        CancellationToken ct)
    {
        List<Product> products =
            await _store.ReadAsync<Product>(DataFiles.Products, ct);

        Product? product = products.FirstOrDefault(p => p.Id == productId);

        if (product is null || product.StockQuantity < quantity)
        {
            return false;
        }

        // Intentionally separate the read and write to expose the race.
        await Task.Delay(200, ct);
        await Task.Delay(100, ct);
        await Task.Delay(400, ct);

        product.StockQuantity -= quantity;

        await _store.WriteAsync(DataFiles.Products, products, ct);
        await Task.Delay(200, ct);

        return true;
    }

    private static Order CreateKeyboardOrder(int orderId)
    {
        return new Order
        {
            Id = orderId,
            Items = new List<OrderItem>
            {
                new()
                {
                    ProductId = 4,
                    Quantity = 1
                }
            }
        };
    }
}
