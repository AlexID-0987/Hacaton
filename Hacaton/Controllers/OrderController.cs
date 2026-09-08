using Hacaton.Data;
using Hacaton.Models;
using Hacaton.Models.CustomerOrder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hacaton.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly OrderAssistantDbContext _db;

    public OrdersController(OrderAssistantDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderRequest request)
    {
        if (request == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "Некоректні дані замовлення."
            });
        }

        if (string.IsNullOrWhiteSpace(request.CustomerName))
        {
            return BadRequest(new
            {
                success = false,
                message = "Не вказано ім'я."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Phone))
        {
            return BadRequest(new
            {
                success = false,
                message = "Не вказано телефон."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Address))
        {
            return BadRequest(new
            {
                success = false,
                message = "Не вказано адресу."
            });
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "Кошик порожній."
            });
        }

        var order = new Order
        {
            CustomerName = request.CustomerName.Trim(),
            Phone = request.Phone.Trim(),
            Address = request.Address.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        decimal total = 0;

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
                continue;

            var itemTotal =
                item.Price * item.Quantity;

            order.Items.Add(new OrderItem
            {
                Name = item.Name,
                Price = item.Price,
                Quantity = item.Quantity,
                Total = itemTotal
            });

            total += itemTotal;
        }

        if (order.Items.Count == 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "У замовленні немає товарів."
            });
        }

        order.Total = total;

        _db.Orders.Add(order);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "Замовлення успішно створено.",
            orderId = order.Id,
            total = order.Total,
            createdAt = order.CreatedAt
        });
    }


    [HttpGet]
    public async Task<IActionResult> GetOrders()
    {
        var orders = await _db.Orders
            .Include(x => x.Items)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(orders);
    }
}


public class CreateOrderRequest
{
    public string CustomerName { get; set; } = "";

    public string Phone { get; set; } = "";

    public string Address { get; set; } = "";

    public List<CreateOrderItemRequest> Items { get; set; } = new();
}


public class CreateOrderItemRequest
{
    public string Name { get; set; } = "";

    public decimal Price { get; set; }

    public int Quantity { get; set; }
}