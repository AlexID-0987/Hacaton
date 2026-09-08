using Hacaton.Controllers;

namespace Hacaton.Models.CustomerOrder
{
    public class CreateOrderRequest
    {
        public string CustomerName { get; set; } = "";

        public string Phone { get; set; } = "";

        public string Address { get; set; } = "";

        public List<CreateOrderItemRequest> Items { get; set; } = new();
    }
}
