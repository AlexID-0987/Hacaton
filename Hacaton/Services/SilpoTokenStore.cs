namespace Hacaton.Services;

public class SilpoTokenStore
{
    public string? AccessToken { get; set; }

    // ID MCP-сесії
    public string? McpSessionId { get; set; }
    

    public string? BranchId { get; set; }

    public string? DeliveryAddress { get; set; }
}