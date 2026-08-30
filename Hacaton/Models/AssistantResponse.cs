namespace Hacaton.Models
{
    public class AssistantResponse
    {
        public string Message { get; set; } = string.Empty;
        public decimal Budget { get; set; }
        public decimal TotalPrice { get; set; }
        public List<RecommendationItem> Items { get; set; } = new();
    }
}
