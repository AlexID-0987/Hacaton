namespace Hacaton.Models
{
    public class RecommendationItem
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
    }
}
