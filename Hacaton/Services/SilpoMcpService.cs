using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;


namespace Hacaton.Services;

public class SilpoMcpService
{
    private readonly HttpClient _httpClient;

    public SilpoMcpService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> TestAsync(string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var json = """
    {
        "jsonrpc": "2.0",
        "id": 1,
        "method": "initialize",
        "params": {
            "protocolVersion": "2025-03-26",
            "capabilities": {},
            "clientInfo": {
                "name": "Hacaton",
                "version": "1.0.0"
            }
        }
    }
    """;

        using var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        content.Headers.ContentType!.Parameters.Add(
            new System.Net.Http.Headers.NameValueHeaderValue(
                "charset",
                "\"utf-8\""));

        var response = await _httpClient.PostAsync(
            "https://mcp.silpo.ua/mcp",
            content);

        var responseBody =
            await response.Content.ReadAsStringAsync();

        return $"HTTP {(int)response.StatusCode}\n{responseBody}";
    }
    public async Task<string> GetToolsAsync(string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var json = """
    {
        "jsonrpc": "2.0",
        "id": 2,
        "method": "tools/list",
        "params": {}
    }
    """;

        using var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(
            "https://mcp.silpo.ua/mcp",
            content);

        var responseBody =
            await response.Content.ReadAsStringAsync();

        return $"HTTP {(int)response.StatusCode}\n{responseBody}";
    }
    public async Task<string> FindAddressAsync(
    string accessToken,
    string address)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var request = new
        {
            jsonrpc = "2.0",
            id = 3,
            method = "tools/call",
            @params = new
            {
                name = "silpo_find_address",
                arguments = new
                {
                    address
                }
            }
        };

        var json = JsonSerializer.Serialize(request);

        using var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(
            "https://mcp.silpo.ua/mcp",
            content);

        return await response.Content.ReadAsStringAsync();
    }
    public async Task<string> GetDeliveryTypesAsync(string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var json = """
    {
        "jsonrpc": "2.0",
        "id": 4,
        "method": "tools/list",
        "params": {}
    }
    """;

        using var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(
            "https://mcp.silpo.ua/mcp",
            content);

        var responseBody =
            await response.Content.ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(responseBody);

        var tools = document.RootElement
            .GetProperty("result")
            .GetProperty("tools");

        foreach (var tool in tools.EnumerateArray())
        {
            if (tool.GetProperty("name").GetString()
                == "silpo_get_available_delivery_types")
            {
                return JsonSerializer.Serialize(
                    tool,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
            }
        }

        return "Tool silpo_get_available_delivery_types не знайдено.";
    }
    public async Task<string> GetAvailableDeliveryTypesAsync(
    string accessToken,
    double latitude,
    double longitude)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var request = new
        {
            jsonrpc = "2.0",
            id = 5,
            method = "tools/call",
            @params = new
            {
                name = "silpo_get_available_delivery_types",
                arguments = new
                {
                    latitude,
                    longitude
                }
            }
        };

        var json = JsonSerializer.Serialize(request);

        using var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(
            "https://mcp.silpo.ua/mcp",
            content);

        return await response.Content.ReadAsStringAsync();
    }
    public async Task<string> GetTimeSlotsAsync(
    string accessToken,
    string branchId,
    string deliveryType)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var request = new
        {
            jsonrpc = "2.0",
            id = 6,
            method = "tools/call",
            @params = new
            {
                name = "silpo_get_time_slots",
                arguments = new
                {
                    branchId,
                    deliveryTypes = new[] { deliveryType },
                    limit = 10
                }
            }
        };

        var json = JsonSerializer.Serialize(request);

        using var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(
            "https://mcp.silpo.ua/mcp",
            content);

        return await response.Content.ReadAsStringAsync();
    }
    public async Task<string> FindProductsAsync(
    string accessToken,
    string branchId,
    string deliveryType,
    string timeslotStart,
    string timeslotEnd,
    string[] products)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var request = new
        {
            jsonrpc = "2.0",
            id = 7,
            method = "tools/call",
            @params = new
            {
                name = "silpo_find_products_batch",
                arguments = new
                {
                    branchId,
                    deliveryType,
                    timeslotStart,
                    timeslotEnd,
                    products
                }
            }
        };

        var json = JsonSerializer.Serialize(request);

        using var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(
            "https://mcp.silpo.ua/mcp",
            content);

        return await response.Content.ReadAsStringAsync();
    }

}