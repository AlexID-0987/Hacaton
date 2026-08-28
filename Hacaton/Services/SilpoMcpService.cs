using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Hacaton.Services;

public class SilpoMcpService
{
    private readonly HttpClient _httpClient;

    private const string McpUrl = "https://mcp.silpo.ua/mcp";

    public SilpoMcpService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private async Task<string> CallToolAsync(
        string accessToken,
        int id,
        string toolName,
        object arguments)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var request = new
        {
            jsonrpc = "2.0",
            id,
            method = "tools/call",
            @params = new
            {
                name = toolName,
                arguments
            }
        };

        var json = JsonSerializer.Serialize(request);

        using var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(
            McpUrl,
            content);

        var responseBody =
            await response.Content.ReadAsStringAsync();

        return $"HTTP {(int)response.StatusCode}\n{responseBody}";
    }

    public async Task<string> TestAsync(string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var request = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new
            {
                protocolVersion = "2025-03-26",
                capabilities = new { },
                clientInfo = new
                {
                    name = "Hacaton",
                    version = "1.0.0"
                }
            }
        };

        var json = JsonSerializer.Serialize(request);

        using var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(
            McpUrl,
            content);

        var responseBody =
            await response.Content.ReadAsStringAsync();

        return $"HTTP {(int)response.StatusCode}\n{responseBody}";
    }

    public async Task<string> GetToolsAsync(string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var request = new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "tools/list",
            @params = new { }
        };

        var json = JsonSerializer.Serialize(request);

        using var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(
            McpUrl,
            content);

        var responseBody =
            await response.Content.ReadAsStringAsync();

        return $"HTTP {(int)response.StatusCode}\n{responseBody}";
    }

    public Task<string> FindAddressAsync(
        string accessToken,
        string address)
    {
        return CallToolAsync(
            accessToken,
            3,
            "silpo_find_address",
            new
            {
                address
            });
    }

    public Task<string> GetAvailableDeliveryTypesAsync(
        string accessToken,
        double latitude,
        double longitude)
    {
        return CallToolAsync(
            accessToken,
            5,
            "silpo_get_available_delivery_types",
            new
            {
                latitude,
                longitude
            });
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
                    limit = 20
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

        var responseBody =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return $"HTTP {(int)response.StatusCode}\n{responseBody}";
        }

        using var document =
            JsonDocument.Parse(responseBody);

        var text = document.RootElement
            .GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = "Порожня відповідь від Silpo MCP."
            });
        }

        using var slotsDocument =
            JsonDocument.Parse(text);

        var slots = slotsDocument.RootElement
            .GetProperty("slots");

        var kyivTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");

        var availableSlots = new List<object>();

        foreach (var slot in slots.EnumerateArray())
        {
            if (!slot.GetProperty("available").GetBoolean())
                continue;

            var startUtc =
                DateTimeOffset.Parse(
                    slot.GetProperty("start").GetString()!);

            var endUtc =
                DateTimeOffset.Parse(
                    slot.GetProperty("end").GetString()!);

            var startKyiv =
                TimeZoneInfo.ConvertTime(
                    startUtc,
                    kyivTimeZone);

            var endKyiv =
                TimeZoneInfo.ConvertTime(
                    endUtc,
                    kyivTimeZone);

            availableSlots.Add(new
            {
                date = startKyiv.ToString("dd.MM.yyyy"),
                start = startKyiv.ToString("HH:mm"),
                end = endKyiv.ToString("HH:mm"),
                time = $"{startKyiv:HH:mm}–{endKyiv:HH:mm}",
                deliveryType = slot
                    .GetProperty("deliveryType")
                    .GetString(),
                deliveryCost = slot
                    .GetProperty("deliveryCost")
                    .GetDecimal(),
                minOrderCost = slot
                    .GetProperty("minOrderCost")
                    .GetDecimal()
            });
        }

        return JsonSerializer.Serialize(
            new
            {
                success = true,
                total = availableSlots.Count,
                slots = availableSlots
            },
            new JsonSerializerOptions
            {
                WriteIndented = true
            });
    }

    public Task<string> FindProductsAsync(
        string accessToken,
        string branchId,
        string deliveryType,
        string timeslotStart,
        string timeslotEnd,
        string[] products)
    {
        return CallToolAsync(
            accessToken,
            7,
            "silpo_find_products_batch",
            new
            {
                branchId,
                deliveryType,
                timeslotStart,
                timeslotEnd,
                products
            });
    }
    public async Task<string> GetDeliveryTypesAsync(string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var request = new
        {
            jsonrpc = "2.0",
            id = 4,
            method = "tools/list",
            @params = new { }
        };

        var json = JsonSerializer.Serialize(request);

        using var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(
            McpUrl,
            content);

        var responseBody =
            await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(responseBody);

        if (!document.RootElement.TryGetProperty("result", out var result) ||
            !result.TryGetProperty("tools", out var tools))
        {
            return responseBody;
        }

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
}