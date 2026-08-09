namespace Hacaton.Models;

public class UserRequest
{
    public string Message { get; set; } = string.Empty;
}

public class ProductSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
}

public class RecommendationItem
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
}

public class AssistantResponse
{
    public string Message { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public decimal TotalPrice { get; set; }
    public List<RecommendationItem> Items { get; set; } = new();
}
