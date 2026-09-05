namespace Hacaton.Models.CustomerOrder
{
    public class OrderItem
    {
        public int Id { get; set; }

        public int OrderId { get; set; }

        public Order? Order { get; set; }

        public string Name { get; set; } = "";

        public decimal Price { get; set; }

        public int Quantity { get; set; }

        public decimal Total { get; set; }
    }
}
