using Hacaton.Models.CustomerOrder;
using Microsoft.EntityFrameworkCore;

namespace Hacaton.Data
{
    public class OrderAssistantDbContext : DbContext
    {
        public OrderAssistantDbContext(DbContextOptions<OrderAssistantDbContext> options) : base(options)
        {
        }
        public DbSet<Order> Orders { get; set; } = default!;
        public DbSet<OrderItem> OrderItems { get; set; } = default!;
    }
}
