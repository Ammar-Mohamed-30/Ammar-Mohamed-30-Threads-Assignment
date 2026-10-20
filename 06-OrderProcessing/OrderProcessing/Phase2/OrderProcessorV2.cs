
using OrderProcessing.Repositories;
using OrderProcessing.Services;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Logging;

namespace OrderProcessing.Phase2;

public sealed class OrderProcessorV2
{
    private readonly InventoryService _inventoryService;
    private readonly OrderPricer _orderPricer;
    private readonly InvoiceGenerator _invoiceGenerator;
    private readonly OrderRepository _orderRepository;

    public OrderProcessorV2(
        InventoryService inventoryService,
        OrderPricer orderPricer,
        InvoiceGenerator invoiceGenerator,
        OrderRepository orderRepository)
    {
        _inventoryService = inventoryService;
        _orderPricer = orderPricer;
        _invoiceGenerator = invoiceGenerator;
        _orderRepository = orderRepository;
    }

    public async Task<bool> ProcessValidatedOrderAsync(
        Order order,
        CancellationToken ct)
    {
        var reservedItems = new List<OrderItem>();

        foreach (OrderItem item in order.Items)
        {
            var singleItemOrder = new Order
            {
                Id = order.Id,
                Items = new List<OrderItem> { item }
            };

            bool reserved = await _inventoryService.ReserveStockAsync(
                singleItemOrder,
                ct);

            if (!reserved)
            {
                foreach (OrderItem reservedItem in reservedItems)
                {
                    var releaseOrder = new Order
                    {
                        Id = order.Id,
                        Items = new List<OrderItem> { reservedItem }
                    };

                    await _inventoryService.ReleaseStockAsync(
                        releaseOrder,
                        ct);
                }

                string productName = GetProductName(item.ProductId);

                order.Status = OrderStatus.Rejected;
                order.FailureReason =
                    $"Insufficient stock for '{productName}'";

                await _orderRepository.SaveAsync(order, ct);
                return false;
            }

            reservedItems.Add(item);
        }

        await _orderPricer.PriceOrderAsync(order, ct);

        await _invoiceGenerator.GenerateAsync(order, ct);

        order.Status = OrderStatus.Completed;
        order.FailureReason = null;

        await _orderRepository.SaveAsync(order, ct);

        return true;
    }

    private static string GetProductName(int productId)
    {
        return productId switch
        {
            1 => "Laptop",
            2 => "Smartphone",
            3 => "Wireless Mouse",
            4 => "Mechanical Keyboard",
            5 => "USB-C Hub",
            6 => "Headphones",
            7 => "Monitor",
            8 => "Webcam",
            _ => $"Product #{productId}"
        };
    }
}
