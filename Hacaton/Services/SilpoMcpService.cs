
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Hacaton.Services;

public class SilpoMcpService
{
    private readonly HttpClient _httpClient;
    private readonly SilpoTokenStore _tokenStore;

    private const string McpUrl = "https://mcp.silpo.ua/mcp";

    private int _requestId = 1;

    public SilpoMcpService(
        HttpClient httpClient,
        SilpoTokenStore tokenStore)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
    }

    // ============================================================
    // INITIALIZE MCP SESSION
    // ============================================================

    private async Task<bool> InitializeAsync(string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);

        var request = new
        {
            jsonrpc = "2.0",
            id = _requestId++,
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

        using var response =
            await _httpClient.PostAsync(
                McpUrl,
                content);

        var responseBody =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        // ========================================================
        // IMPORTANT:
        // MCP session ID comes from response headers
        // ========================================================

        if (response.Headers.TryGetValues(
                "Mcp-Session-Id",
                out var sessionValues))
        {
            var sessionId = sessionValues.FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                _tokenStore.McpSessionId = sessionId;
            }
        }

        return true;
    }


    // ============================================================
    // ENSURE SESSION
    // ============================================================

    private async Task<bool> EnsureSessionAsync(
        string accessToken)
    {
        // Якщо вже є session ID —
        // повторний initialize не потрібен
        if (!string.IsNullOrWhiteSpace(
                _tokenStore.McpSessionId))
        {
            return true;
        }

        return await InitializeAsync(accessToken);
    }


    // ============================================================
    // COMMON MCP REQUEST
    // ============================================================

    private async Task<HttpResponseMessage> SendMcpRequestAsync(
        string accessToken,
        object request)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);

        using var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            McpUrl);

        httpRequest.Content = content;

        // MCP session
        if (!string.IsNullOrWhiteSpace(
                _tokenStore.McpSessionId))
        {
            httpRequest.Headers.TryAddWithoutValidation(
                "Mcp-Session-Id",
                _tokenStore.McpSessionId);
        }

        return await _httpClient.SendAsync(
            httpRequest);
    }


    // ============================================================
    // GENERIC TOOL CALL
    // ============================================================

    public async Task<string> CallToolAsync(
        string accessToken,
        int id,
        string toolName,
        object arguments)
    {
        if (!await EnsureSessionAsync(accessToken))
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = "Не вдалося ініціалізувати MCP-сесію Silpo."
            });
        }

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

        using var response =
            await SendMcpRequestAsync(
                accessToken,
                request);

        var responseBody =
            await response.Content.ReadAsStringAsync();

        // ========================================================
        // Якщо MCP каже, що session недійсна —
        // очищаємо session і пробуємо один раз повторно.
        // ========================================================

        if ((int)response.StatusCode == 400 ||
            (int)response.StatusCode == 404)
        {
            _tokenStore.McpSessionId = null;

            if (await InitializeAsync(accessToken))
            {
                using var retryResponse =
                    await SendMcpRequestAsync(
                        accessToken,
                        request);

                var retryBody =
                    await retryResponse.Content.ReadAsStringAsync();

                return
                    $"HTTP {(int)retryResponse.StatusCode}\n{retryBody}";
            }
        }

        return
            $"HTTP {(int)response.StatusCode}\n{responseBody}";
    }


    // ============================================================
    // TEST INITIALIZE
    // ============================================================

    public async Task<string> TestAsync(
        string accessToken)
    {
        _tokenStore.McpSessionId = null;

        var success =
            await InitializeAsync(accessToken);

        if (!success)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = "MCP initialize завершився помилкою."
            });
        }

        return JsonSerializer.Serialize(new
        {
            success = true,
            message = "MCP initialize успішний.",
            sessionId = _tokenStore.McpSessionId
        });
    }


    // ============================================================
    // TOOLS LIST
    // ============================================================

    public async Task<string> GetToolsAsync(
        string accessToken)
    {
        if (!await EnsureSessionAsync(accessToken))
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = "Не вдалося ініціалізувати MCP-сесію."
            });
        }

        var request = new
        {
            jsonrpc = "2.0",
            id = _requestId++,
            method = "tools/list",
            @params = new { }
        };

        using var response =
            await SendMcpRequestAsync(
                accessToken,
                request);

        var responseBody =
            await response.Content.ReadAsStringAsync();

        return
            $"HTTP {(int)response.StatusCode}\n{responseBody}";
    }


    // ============================================================
    // FIND ADDRESS
    // ============================================================

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


    // ============================================================
    // DELIVERY TYPES
    // ============================================================

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


    // ============================================================
    // TIME SLOTS
    // ============================================================

    public async Task<string> GetTimeSlotsAsync(
        string accessToken,
        string branchId,
        string deliveryType)
    {
        var rawResponse =
            await CallToolAsync(
                accessToken,
                6,
                "silpo_get_time_slots",
                new
                {
                    branchId,
                    deliveryTypes = new[]
                    {
                        deliveryType
                    },
                    limit = 20
                });

        try
        {
            var parts =
                rawResponse.Split(
                    '\n',
                    2,
                    StringSplitOptions.None);

            if (parts.Length < 2)
                return rawResponse;

            var responseBody = parts[1];

            using var document =
                JsonDocument.Parse(responseBody);

            var root =
                document.RootElement;

            if (!root.TryGetProperty(
                    "result",
                    out var result))
            {
                return rawResponse;
            }

            if (!result.TryGetProperty(
                    "content",
                    out var content))
            {
                return rawResponse;
            }

            if (content.GetArrayLength() == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message =
                        "Silpo MCP повернув порожній результат."
                });
            }

            var text =
                content[0]
                    .GetProperty("text")
                    .GetString();

            if (string.IsNullOrWhiteSpace(text))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message =
                        "Silpo MCP не повернув текст."
                });
            }

            using var slotsDocument =
                JsonDocument.Parse(text);

            if (!slotsDocument.RootElement
                    .TryGetProperty(
                        "slots",
                        out var slots))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message =
                        "У відповіді Silpo немає slots."
                });
            }

            var kyivTimeZone =
                TimeZoneInfo.FindSystemTimeZoneById(
                    "FLE Standard Time");

            var availableSlots =
                new List<object>();

            foreach (var slot in slots.EnumerateArray())
            {
                if (!slot.TryGetProperty(
                        "available",
                        out var available) ||
                    !available.GetBoolean())
                {
                    continue;
                }

                var startUtc =
                    DateTimeOffset.Parse(
                        slot.GetProperty("start")
                            .GetString()!);

                var endUtc =
                    DateTimeOffset.Parse(
                        slot.GetProperty("end")
                            .GetString()!);

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
                    date =
                        startKyiv.ToString(
                            "dd.MM.yyyy"),

                    start =
                        startKyiv.ToString(
                            "HH:mm"),

                    end =
                        endKyiv.ToString(
                            "HH:mm"),

                    time =
                        $"{startKyiv:HH:mm}–{endKyiv:HH:mm}",

                    deliveryType =
                        slot.GetProperty(
                            "deliveryType")
                            .GetString(),

                    deliveryCost =
                        slot.GetProperty(
                            "deliveryCost")
                            .GetDecimal(),

                    minOrderCost =
                        slot.GetProperty(
                            "minOrderCost")
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
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                message =
                    "Помилка обробки time slots.",
                error = ex.Message
            });
        }
    }


    // ============================================================
    // FIND PRODUCTS
    // ============================================================

    public async Task<string> FindProductsAsync(
        string accessToken,
        string branchId,
        string deliveryType,
        string timeslotStart,
        string timeslotEnd,
        string[] products)
    {
        var rawResponse =
            await CallToolAsync(
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

        try
        {
            var parts =
                rawResponse.Split(
                    '\n',
                    2,
                    StringSplitOptions.None);

            if (parts.Length < 2)
                return rawResponse;

            var responseBody = parts[1];

            using var document =
                JsonDocument.Parse(responseBody);

            var root =
                document.RootElement;

            if (!root.TryGetProperty(
                    "result",
                    out var result))
            {
                return responseBody;
            }

            if (!result.TryGetProperty(
                    "content",
                    out var contentArray))
            {
                return responseBody;
            }

            if (contentArray.GetArrayLength() == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message =
                        "Silpo MCP повернув порожній результат."
                });
            }

            var text =
                contentArray[0]
                    .GetProperty("text")
                    .GetString();

            if (string.IsNullOrWhiteSpace(text))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message =
                        "Silpo MCP не повернув текст."
                });
            }

            using var innerDocument =
                JsonDocument.Parse(text);

            // Дуже важливо:
            // Clone перед Dispose JsonDocument
            var cleanResult =
                innerDocument.RootElement.Clone();

            return JsonSerializer.Serialize(
                cleanResult,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                message =
                    "Не вдалося розібрати JSON від Silpo MCP.",
                error = ex.Message,
                raw = rawResponse
            });
        }
    }


    // ============================================================
    // GET DELIVERY TOOL DESCRIPTION
    // ============================================================

    public async Task<string> GetDeliveryTypesAsync(
        string accessToken)
    {
        var toolsResponse =
            await GetToolsAsync(accessToken);

        try
        {
            var parts =
                toolsResponse.Split(
                    '\n',
                    2,
                    StringSplitOptions.None);

            if (parts.Length < 2)
                return toolsResponse;

            var responseBody = parts[1];

            using var document =
                JsonDocument.Parse(responseBody);

            if (!document.RootElement.TryGetProperty(
                    "result",
                    out var result))
            {
                return responseBody;
            }

            if (!result.TryGetProperty(
                    "tools",
                    out var tools))
            {
                return responseBody;
            }

            foreach (var tool in tools.EnumerateArray())
            {
                if (tool.GetProperty("name")
                        .GetString()
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

            return
                "Tool silpo_get_available_delivery_types не знайдено.";
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                message =
                    "Помилка розбору tools/list.",
                error = ex.Message
            });
        }
    }
}
