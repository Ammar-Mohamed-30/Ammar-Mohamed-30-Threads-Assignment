using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Repositories;

public class OrderRepository
{
    private readonly JsonFileStore _store;
    private readonly HashSet<int> _failSaveForOrderIds;

    public OrderRepository(
        JsonFileStore store,
        IEnumerable<int>? failSaveForOrderIds = null)
    {
        _store = store;
        _failSaveForOrderIds = new HashSet<int>(
            failSaveForOrderIds ?? Array.Empty<int>());
    }

    public async Task<List<Order>> GetIncomingOrdersAsync(
        CancellationToken ct)
    {
        Log.Info("OrderRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info("OrderRepository -> Executing incoming orders query...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        List<Order> orders = await _store.ReadAsync<Order>(
            DataFiles.IncomingOrders, ct);

        Log.Success(
            $"OrderRepository -> Retrieved {orders.Count} incoming orders.");

        return orders;
    }

    public async Task<List<Order>> GetAllAsync(
        CancellationToken ct)
    {
        Log.Info("OrderRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info("OrderRepository -> Executing orders query...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        List<Order> orders = await _store.ReadAsync<Order>(
            DataFiles.Orders, ct);

        Log.Success(
            $"OrderRepository -> Retrieved {orders.Count} orders.");

        return orders;
    }

    public async Task SaveAsync(
        Order order,
        CancellationToken ct)
    {
        Log.Info(
            $"OrderRepository -> Opening connection for Order #{order.Id}...");
        await Task.Delay(200, ct);

        Log.Info(
            $"OrderRepository -> Beginning transaction for Order #{order.Id}...");
        await Task.Delay(100, ct);

        Log.Info(
            $"OrderRepository -> Inserting/Updating Order #{order.Id}...");
        await Task.Delay(400, ct);

        if (_failSaveForOrderIds.Contains(order.Id) &&
            order.Status == OrderStatus.Completed)
        {
            Log.Error(
                $"OrderRepository -> Simulated database failure for Order #{order.Id}.");

            throw new DatabaseWriteException(
                "Connection lost during commit");
        }

        await _store.UpdateAsync<Order, bool>(
            DataFiles.Orders,
            orders =>
            {
                int existingIndex = orders.FindIndex(
                    existingOrder => existingOrder.Id == order.Id);

                if (existingIndex >= 0)
                {
                    orders[existingIndex] = order;
                }
                else
                {
                    orders.Add(order);
                }

                return true;
            },
            ct);

        Log.Info(
            $"OrderRepository -> Committing Order #{order.Id}...");
        await Task.Delay(200, ct);

        Log.Success(
            $"OrderRepository -> Order #{order.Id} saved.");
    }
}