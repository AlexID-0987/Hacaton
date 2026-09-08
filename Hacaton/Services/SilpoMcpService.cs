
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Hacaton.Services;

public class SilpoMcpService
{
    private readonly HttpClient _httpClient;
    private readonly SilpoTokenStore _tokenStore;

    private const string McpUrl = "https://mcp.silpo.ua/mcp";

    public SilpoMcpService(
        HttpClient httpClient,
        SilpoTokenStore tokenStore)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
    }

    // ============================================================
    // INITIALIZE MCP
    // ============================================================

    public async Task<string> InitializeAsync(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException(
                "Access token не може бути порожнім.");

        var body = new
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

        return await SendMcpRequestAsync(
            accessToken,
            body);
    }

    // ============================================================
    // ENSURE SESSION
    // ============================================================

    public async Task EnsureSessionAsync(
        string accessToken)
    {
        if (!string.IsNullOrWhiteSpace(
                _tokenStore.McpSessionId))
        {
            return;
        }

        await InitializeSessionAsync(accessToken);
    }

    private async Task InitializeSessionAsync(
        string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException(
                "Access token не може бути порожнім.");

        var body = new
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

        using var request =
            CreateMcpRequest(
                accessToken,
                body);

        using var response =
            await _httpClient.SendAsync(request);

        var responseBody =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Silpo MCP initialize HTTP {(int)response.StatusCode}: {responseBody}");
        }

        var sessionId =
            GetSessionId(response);

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            _tokenStore.McpSessionId =
                sessionId;
        }
    }

    // ============================================================
    // SEND MCP REQUEST
    // ============================================================

    private async Task<string> SendMcpRequestAsync(
        string accessToken,
        object body)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException(
                "Access token не може бути порожнім.");

        await EnsureSessionAsync(accessToken);

        using var request =
            CreateMcpRequest(
                accessToken,
                body);

        using var response =
            await _httpClient.SendAsync(request);

        var responseBody =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Silpo MCP HTTP {(int)response.StatusCode}: {responseBody}");
        }

        var sessionId =
            GetSessionId(response);

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            _tokenStore.McpSessionId =
                sessionId;
        }

        return ExtractMcpJson(responseBody);
    }

    // ============================================================
    // CREATE MCP REQUEST
    // ============================================================

    private HttpRequestMessage CreateMcpRequest(
        string accessToken,
        object body)
    {
        var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                McpUrl);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/json"));

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "text/event-stream"));

        if (!string.IsNullOrWhiteSpace(
                _tokenStore.McpSessionId))
        {
            request.Headers.TryAddWithoutValidation(
                "Mcp-Session-Id",
                _tokenStore.McpSessionId);
        }

        var json =
            JsonSerializer.Serialize(body);

        request.Content =
            new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

        return request;
    }

    // ============================================================
    // GET MCP SESSION ID
    // ============================================================

    private string? GetSessionId(
        HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues(
                "Mcp-Session-Id",
                out var values))
        {
            return values.FirstOrDefault();
        }

        return null;
    }

    // ============================================================
    // CALL TOOL
    // ============================================================

    public async Task<string> CallToolAsync(
        string accessToken,
        string toolName,
        object arguments)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException(
                "Access token не може бути порожнім.");

        if (string.IsNullOrWhiteSpace(toolName))
            throw new ArgumentException(
                "Назва MCP tool не може бути порожньою.");

        var body = new
        {
            jsonrpc = "2.0",
            id =
                DateTimeOffset.UtcNow
                    .ToUnixTimeMilliseconds(),

            method = "tools/call",

            @params = new
            {
                name = toolName,
                arguments
            }
        };
        var json = JsonSerializer.Serialize(body);

        Console.WriteLine("========== MCP REQUEST ==========");
        Console.WriteLine(json);
        Console.WriteLine("=================================");

        return await SendMcpRequestAsync(
            accessToken,
            body);
        
    }
    
    
    // ============================================================
    // GET TOOLS
    // ============================================================

    public async Task<string> GetToolsAsync(
        string accessToken)
    {
        var body = new
        {
            jsonrpc = "2.0",
            id =
                DateTimeOffset.UtcNow
                    .ToUnixTimeMilliseconds(),

            method = "tools/list",

            @params = new { }
        };

        return await SendMcpRequestAsync(
            accessToken,
            body);
    }

    // ============================================================
    // FIND ADDRESS
    // ============================================================

    public async Task<string> FindAddressAsync(
        string accessToken,
        string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException(
                "Адреса не може бути порожньою.");

        return await CallToolAsync(
            accessToken,
            "silpo_find_address",
            new
            {
                address
            });
    }

    // ============================================================
    // GET AVAILABLE DELIVERY TYPES
    // WITHOUT COORDINATES
    // ============================================================

    public async Task<string> GetAvailableDeliveryTypesAsync(
        string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException(
                "Access token не може бути порожнім.");

        return await CallToolAsync(
            accessToken,
            "silpo_get_available_delivery_types",
            new { });
    }

    // ============================================================
    // GET AVAILABLE DELIVERY TYPES
    // BY COORDINATES
    // ============================================================

    public async Task<string> GetAvailableDeliveryTypesAsync(
        string accessToken,
        double latitude,
        double longitude)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException(
                "Access token не може бути порожнім.");

        Console.WriteLine();
        Console.WriteLine(
            "========== SILPO DELIVERY TYPES ==========");

        Console.WriteLine(
            $"Latitude:  {latitude}");

        Console.WriteLine(
            $"Longitude: {longitude}");

        Console.WriteLine(
            "==========================================");

        return await CallToolAsync(
            accessToken,
            "silpo_get_available_delivery_types",
            new
            {
                latitude,
                longitude
            });
    }

    // ============================================================
    // OVERLOAD: OBJECT ACCESS TOKEN
    // ============================================================

    public async Task<string> GetAvailableDeliveryTypesAsync(
        object accessToken)
    {
        if (accessToken is not string token ||
            string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException(
                "Access token повинен бути string.");
        }

        return await GetAvailableDeliveryTypesAsync(
            token);
    }

    // ============================================================
    // GET DELIVERY TYPES
    // ============================================================

    public async Task<string> GetDeliveryTypesAsync(
        string accessToken)
    {
        return await GetAvailableDeliveryTypesAsync(
            accessToken);
    }

    // ============================================================
    // SET BRANCH BY COORDINATES
    // ============================================================

    public async Task<string> SetBranchByCoordinatesAsync(
        string accessToken,
        double latitude,
        double longitude)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException(
                "Access token не може бути порожнім.");

        Console.WriteLine();
        Console.WriteLine(
            "========== SET BRANCH ==========");

        Console.WriteLine(
            $"Latitude:  {latitude}");

        Console.WriteLine(
            $"Longitude: {longitude}");

        Console.WriteLine(
            "================================");

        var result =
            await GetAvailableDeliveryTypesAsync(
                accessToken,
                latitude,
                longitude);

        Console.WriteLine();
        Console.WriteLine(
            "Available delivery types:");

        Console.WriteLine(result);

        var branchId =
            ExtractBranchId(result);

        Console.WriteLine();
        Console.WriteLine(
            $"Extracted BranchId: {branchId}");

        if (!string.IsNullOrWhiteSpace(branchId))
        {
            _tokenStore.BranchId =
                branchId;

            Console.WriteLine(
                $"SILPO BRANCH SET: {_tokenStore.BranchId}");
        }
        else
        {
            Console.WriteLine(
                "WARNING: BranchId не знайдений.");
        }

        _tokenStore.DeliveryAddress =
            $"{latitude.ToString(CultureInfo.InvariantCulture)}," +
            $"{longitude.ToString(CultureInfo.InvariantCulture)}";

        return JsonSerializer.Serialize(
            new
            {
                success = true,

                branchId =
                    _tokenStore.BranchId,

                latitude,
                longitude,

                deliveryType =
                    "DeliveryHome"
            },
            new JsonSerializerOptions
            {
                WriteIndented = true
            });
    }

    // ============================================================
    // GET TIME SLOTS
    // ============================================================

    public async Task<string> GetTimeSlotsAsync(
        string accessToken,
        string deliveryType = "DeliveryHome")
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException(
                "Access token не може бути порожнім.");

        if (string.IsNullOrWhiteSpace(
                _tokenStore.BranchId))
        {
            throw new Exception(
                "BranchId не визначений.");
        }

        var result =
            await CallToolAsync(
                accessToken,
                "silpo_get_time_slots",
                new
                {
                    branchId = _tokenStore.BranchId,
                    deliveryType
                });

        return NormalizeTimeSlots(result);
    }

    // ============================================================
    // FIND PRODUCTS
    // ============================================================

    public async Task<string> FindProductsAsync(
        string accessToken,
        string deliveryType,
        string timeslotStart,
        string timeslotEnd,
        string[] products)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException(
                "Access token не може бути порожнім.");

        if (string.IsNullOrWhiteSpace(
                _tokenStore.BranchId))
        {
            throw new Exception(
                "BranchId не визначений.");
        }

        if (products == null ||
            products.Length == 0)
        {
            throw new ArgumentException(
                "Не передані товари.");
        }

        if (string.IsNullOrWhiteSpace(
                timeslotStart) ||
            string.IsNullOrWhiteSpace(
                timeslotEnd))
        {
            throw new ArgumentException(
                "Не переданий час доставки.");
        }

        Console.WriteLine();
        Console.WriteLine(
            "========== FIND PRODUCTS ==========");

        Console.WriteLine(
            $"BranchId:       {_tokenStore.BranchId}");

        Console.WriteLine(
            $"DeliveryType:   {deliveryType}");

        Console.WriteLine(
            $"Timeslot Start: {timeslotStart}");

        Console.WriteLine(
            $"Timeslot End:   {timeslotEnd}");

        Console.WriteLine(
            $"Products:       {string.Join(", ", products)}");

        Console.WriteLine(
            "===================================");

        var result =
            await CallToolAsync(
                accessToken,
                "silpo_find_products_batch",
                new
                {
                    products,
                    deliveryType,
                    timeslotStart,
                    timeslotEnd,

                    branchId =
                        _tokenStore.BranchId
                });
        Console.WriteLine("========== RAW PRODUCTS RESPONSE ==========");
        Console.WriteLine(result);
        Console.WriteLine("===========================================");

        return JsonSerializer.Serialize(
            new
            {
                success = true,

                branchId =
                    _tokenStore.BranchId,

                deliveryType,
                timeslotStart,
                timeslotEnd,

                data =
                    ParseJsonElement(result)
            },
            new JsonSerializerOptions
            {
                WriteIndented = true
            });
    }

    // ============================================================
    // TEST
    // ============================================================

    public async Task<string> TestAsync(
        string accessToken)
    {
        return await GetToolsAsync(
            accessToken);
    }

    // ============================================================
    // EXTRACT BRANCH ID
    // ============================================================

    private string? ExtractBranchId(
        string json)
    {
        try
        {
            var clean =
                ExtractJson(json);

            using var document =
                JsonDocument.Parse(clean);

            return FindStringProperty(
                document.RootElement,
                "branchId");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"ExtractBranchId error: {ex.Message}");

            return null;
        }
    }

    // ============================================================
    // FIND STRING PROPERTY RECURSIVELY
    // ============================================================

    private string? FindStringProperty(
        JsonElement element,
        string propertyName)
    {
        if (element.ValueKind ==
            JsonValueKind.Object)
        {
            foreach (var property
                in element.EnumerateObject())
            {
                if (string.Equals(
                        property.Name,
                        propertyName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (property.Value.ValueKind ==
                        JsonValueKind.String)
                    {
                        return property.Value.GetString();
                    }

                    if (property.Value.ValueKind ==
                        JsonValueKind.Number)
                    {
                        return property.Value.ToString();
                    }
                }

                var nested =
                    FindStringProperty(
                        property.Value,
                        propertyName);

                if (!string.IsNullOrWhiteSpace(
                        nested))
                {
                    return nested;
                }
            }
        }
        else if (element.ValueKind ==
                 JsonValueKind.Array)
        {
            foreach (var item
                in element.EnumerateArray())
            {
                var nested =
                    FindStringProperty(
                        item,
                        propertyName);

                if (!string.IsNullOrWhiteSpace(
                        nested))
                {
                    return nested;
                }
            }
        }

        return null;
    }

    // ============================================================
    // PARSE JSON ELEMENT
    // ============================================================

    private JsonElement ParseJsonElement(
        string json)
    {
        var clean =
            ExtractJson(json);

        using var document =
            JsonDocument.Parse(clean);

        return document.RootElement.Clone();
    }

    // ============================================================
    // NORMALIZE TIME SLOTS
    // ============================================================

    private string NormalizeTimeSlots(
    string rawResponse)
    {
        try
        {
            var json = ExtractJson(rawResponse);

            using var document =
                JsonDocument.Parse(json);

            var slots = new List<JsonElement>();

            FindTimeSlots(
                document.RootElement,
                slots);

            var normalizedSlots =
                new List<object>();

            foreach (var slot in slots)
            {
                var available =
                    GetBool(slot, "available");

                var deliveryType =
                    GetString(slot, "deliveryType");

                // Беремо тільки доступну доставку додому
                if (!available ||
                    !string.Equals(
                        deliveryType,
                        "DeliveryHome",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var start =
                    GetString(slot, "start");

                var end =
                    GetString(slot, "end");

                var startIso =
                    GetString(slot, "startIso");

                var endIso =
                    GetString(slot, "endIso");

                // MCP вже повертає ISO UTC:
                // 2026-09-08T15:00:00+00:00
                if (string.IsNullOrWhiteSpace(startIso))
                    startIso = start;

                if (string.IsNullOrWhiteSpace(endIso))
                    endIso = end;

                normalizedSlots.Add(
                    new
                    {
                        start,
                        end,
                        startIso,
                        endIso,
                        available = true,
                        deliveryType,

                        deliveryCost =
                            GetDecimal(
                                slot,
                                "deliveryCost"),

                        minOrderCost =
                            GetDecimal(
                                slot,
                                "minOrderCost")
                    });
            }

            return JsonSerializer.Serialize(
                new
                {
                    success =
                        normalizedSlots.Count > 0,

                    slots =
                        normalizedSlots
                },
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"NormalizeTimeSlots error: {ex.Message}");

            return JsonSerializer.Serialize(
                new
                {
                    success = false,
                    slots = Array.Empty<object>(),
                    error = ex.Message
                });
        }
    }
    private bool GetBool(
    JsonElement element,
    string propertyName)
    {
        if (!element.TryGetProperty(
                propertyName,
                out var value))
        {
            return false;
        }

        if (value.ValueKind ==
            JsonValueKind.True)
        {
            return true;
        }

        if (value.ValueKind ==
            JsonValueKind.False)
        {
            return false;
        }

        if (value.ValueKind ==
            JsonValueKind.String &&
            bool.TryParse(
                value.GetString(),
                out var result))
        {
            return result;
        }

        return false;
    }

    private void FindTimeSlots(
    JsonElement element,
    List<JsonElement> slots)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("start", out _) &&
                element.TryGetProperty("end", out _) &&
                element.TryGetProperty("deliveryType", out _))
            {
                // КРИТИЧНО:
                // Clone() робить JsonElement незалежним
                // від JsonDocument, який може бути disposed.
                slots.Add(element.Clone());
                return;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(
                        property.Name,
                        "text",
                        StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String)
                {
                    var text = property.Value.GetString();

                    if (string.IsNullOrWhiteSpace(text))
                        continue;

                    try
                    {
                        using var textDocument =
                            JsonDocument.Parse(text);

                        FindTimeSlots(
                            textDocument.RootElement,
                            slots);
                    }
                    catch
                    {
                        // text може бути звичайним текстом,
                        // а не JSON — просто пропускаємо.
                    }
                }
                else
                {
                    FindTimeSlots(
                        property.Value,
                        slots);
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                FindTimeSlots(
                    item,
                    slots);
            }
        }
    }
    // ============================================================
    // BUILD KYIV ISO
    // ============================================================

    private string BuildKyivIso(
        string date,
        string time)
    {
        if (DateTime.TryParseExact(
                $"{date} {time}",
                "dd.MM.yyyy HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateTime))
        {
            return
                $"{dateTime:yyyy-MM-dd}T" +
                $"{dateTime:HH:mm:ss}+03:00";
        }

        return
            $"{date}T{time}:00+03:00";
    }

    // ============================================================
    // GET STRING
    // ============================================================

    private string? GetString(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(
                propertyName,
                out var value))
        {
            return null;
        }

        if (value.ValueKind ==
            JsonValueKind.String)
        {
            return value.GetString();
        }

        return value.ToString();
    }

    // ============================================================
    // GET DECIMAL
    // ============================================================

    private decimal? GetDecimal(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(
                propertyName,
                out var value))
        {
            return null;
        }

        if (value.ValueKind ==
                JsonValueKind.Number &&
            value.TryGetDecimal(
                out var number))
        {
            return number;
        }

        if (value.ValueKind ==
                JsonValueKind.String &&
            decimal.TryParse(
                value.GetString(),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            return parsed;
        }

        return null;
    }

    // ============================================================
    // EXTRACT MCP JSON
    // ============================================================

    private string ExtractMcpJson(
        string rawResponse)
    {
        if (string.IsNullOrWhiteSpace(
                rawResponse))
        {
            throw new Exception(
                "Silpo MCP повернув порожню відповідь.");
        }

        var trimmed =
            rawResponse.Trim();

        if (trimmed.StartsWith("{") ||
            trimmed.StartsWith("["))
        {
            return trimmed;
        }

        var lines =
            rawResponse.Split(
                '\n',
                StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var current =
                line.Trim();

            if (!current.StartsWith(
                    "data:",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var data =
                current[
                    "data:".Length..]
                    .Trim();

            if (data == "[DONE]")
                continue;

            if (data.StartsWith("{") ||
                data.StartsWith("["))
            {
                return data;
            }
        }

        return ExtractJson(
            rawResponse);
    }

    // ============================================================
    // EXTRACT JSON
    // ============================================================

    private string ExtractJson(
        string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new Exception(
                "Порожня відповідь MCP.");
        }

        var firstBrace =
            raw.IndexOf('{');

        var firstBracket =
            raw.IndexOf('[');

        var positions =
            new[]
            {
                firstBrace,
                firstBracket
            }
            .Where(x => x >= 0)
            .ToArray();

        if (positions.Length == 0)
        {
            throw new Exception(
                "У відповіді MCP не знайдено JSON.");
        }

        var start =
            positions.Min();

        return raw[start..].Trim();
    }
    public async Task<string> InitializeBranchByAddressAsync(
    string accessToken,
    string address)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException(
                "Access token не може бути порожнім.");

        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException(
                "Адреса не може бути порожньою.");

        Console.WriteLine();
        Console.WriteLine("========== DETERMINE SILPO BRANCH ==========");
        Console.WriteLine($"Address: {address}");
        Console.WriteLine("============================================");

        // ------------------------------------------------------------
        // 1. Знаходимо адресу
        // ------------------------------------------------------------

        var addressResult =
            await FindAddressAsync(
                accessToken,
                address);

        Console.WriteLine();
        Console.WriteLine("========== ADDRESS RESULT ==========");
        Console.WriteLine(addressResult);
        Console.WriteLine("====================================");

        // ------------------------------------------------------------
        // 2. Витягуємо latitude / longitude
        // ------------------------------------------------------------

        var latitude =
            ExtractDoubleProperty(
                addressResult,
                "latitude");

        var longitude =
            ExtractDoubleProperty(
                addressResult,
                "longitude");

        if (!latitude.HasValue ||
            !longitude.HasValue)
        {
            throw new Exception(
                "Silpo не повернув координати для вказаної адреси.");
        }

        Console.WriteLine(
            $"Latitude: {latitude.Value}");

        Console.WriteLine(
            $"Longitude: {longitude.Value}");

        // ------------------------------------------------------------
        // 3. Визначаємо доступні типи доставки
        // ------------------------------------------------------------

        var deliveryResult =
            await GetAvailableDeliveryTypesAsync(
                accessToken,
                latitude.Value,
                longitude.Value);

        Console.WriteLine();
        Console.WriteLine("========== DELIVERY TYPES ==========");
        Console.WriteLine(deliveryResult);
        Console.WriteLine("====================================");

        // ------------------------------------------------------------
        // 4. Витягуємо branchId
        // ------------------------------------------------------------

        var branchId =
            ExtractBranchId(
                deliveryResult);

        if (string.IsNullOrWhiteSpace(branchId))
        {
            throw new Exception(
                "Silpo не повернув branchId для вказаної адреси.");
        }

        // ------------------------------------------------------------
        // 5. Зберігаємо контекст
        // ------------------------------------------------------------

        _tokenStore.BranchId =
            branchId;

        _tokenStore.DeliveryAddress =
            address;

        Console.WriteLine();
        Console.WriteLine("========== SILPO BRANCH SELECTED ==========");
        Console.WriteLine($"Address:    {address}");
        Console.WriteLine($"Latitude:   {latitude}");
        Console.WriteLine($"Longitude:  {longitude}");
        Console.WriteLine($"BranchId:   {_tokenStore.BranchId}");
        Console.WriteLine("===========================================");

        return JsonSerializer.Serialize(
            new
            {
                success = true,

                address,

                latitude,
                longitude,

                branchId =
                    _tokenStore.BranchId,

                deliveryType =
                    "DeliveryHome"
            },
            new JsonSerializerOptions
            {
                WriteIndented = true
            });
    }
    private double? ExtractDoubleProperty(
    string json,
    string propertyName)
    {
        try
        {
            var clean =
                ExtractJson(json);

            using var document =
                JsonDocument.Parse(clean);

            return FindDoubleProperty(
                document.RootElement,
                propertyName);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"ExtractDoubleProperty error: {ex.Message}");

            return null;
        }
    }
    private double? FindDoubleProperty(
    JsonElement element,
    string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property
                     in element.EnumerateObject())
            {
                if (string.Equals(
                        property.Name,
                        propertyName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (property.Value.ValueKind ==
                        JsonValueKind.Number &&
                        property.Value.TryGetDouble(
                            out var number))
                    {
                        return number;
                    }

                    if (property.Value.ValueKind ==
                        JsonValueKind.String &&
                        double.TryParse(
                            property.Value.GetString(),
                            NumberStyles.Any,
                            CultureInfo.InvariantCulture,
                            out var parsed))
                    {
                        return parsed;
                    }
                }

                var nested =
                    FindDoubleProperty(
                        property.Value,
                        propertyName);

                if (nested.HasValue)
                    return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item
                     in element.EnumerateArray())
            {
                var nested =
                    FindDoubleProperty(
                        item,
                        propertyName);

                if (nested.HasValue)
                    return nested;
            }
        }

        return null;
    }
}

