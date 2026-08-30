
using Hacaton.Services;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hacaton.Controllers;

[ApiController]
[Route("api/silpo")]
public class SilpoAuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly SilpoTokenStore _tokenStore;

    // Branch ID, який ти вже використовував
    private const string DefaultBranchId =
        "1edb6b38-214b-66d6-a8e0-7f2fdd178564";

    private const string DefaultDeliveryType = "DeliveryHome";

    public SilpoAuthController(
        IConfiguration configuration,
        SilpoTokenStore tokenStore)
    {
        _configuration = configuration;
        _tokenStore = tokenStore;
    }

    // ============================================================
    // LOGIN
    // ============================================================

    [HttpGet("login")]
    public IActionResult Login()
    {
        var clientId = _configuration["Silpo:ClientId"];

        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Problem("Silpo:ClientId не налаштований.");
        }

        var state = Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");

        var codeVerifier = Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");

        var codeChallenge = CreateCodeChallenge(codeVerifier);

        Response.Cookies.Append(
            "silpo_oauth_state",
            state,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = false,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromMinutes(10)
            });

        Response.Cookies.Append(
            "silpo_code_verifier",
            codeVerifier,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = false,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromMinutes(10)
            });

        var redirectUri =
            "http://localhost:5068/api/silpo/callback";

        var authorizeUrl =
            "https://mcp.silpo.ua/authorize" +
            $"?response_type=code" +
            $"&client_id={Uri.EscapeDataString(clientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&state={Uri.EscapeDataString(state)}" +
            $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
            $"&code_challenge_method=S256";

        return Redirect(authorizeUrl);
    }

    private static string CreateCodeChallenge(string codeVerifier)
    {
        var bytes = SHA256.HashData(
            Encoding.ASCII.GetBytes(codeVerifier));

        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    // ============================================================
    // CALLBACK
    // ============================================================

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            return BadRequest(new
            {
                error
            });
        }

        if (string.IsNullOrEmpty(code))
        {
            return BadRequest("Authorization code відсутній.");
        }

        var savedState =
            Request.Cookies["silpo_oauth_state"];

        if (string.IsNullOrEmpty(savedState) ||
            savedState != state)
        {
            return BadRequest("Невірний OAuth state.");
        }

        var codeVerifier =
            Request.Cookies["silpo_code_verifier"];

        if (string.IsNullOrEmpty(codeVerifier))
        {
            return BadRequest("PKCE code_verifier відсутній.");
        }

        var clientId =
            _configuration["Silpo:ClientId"];

        var clientSecret =
            _configuration["Silpo:ClientSecret"];

        using var httpClient = new HttpClient();

        var tokenRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "https://mcp.silpo.ua/token");

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(
                $"{clientId}:{clientSecret}"));

        tokenRequest.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Basic",
                credentials);

        tokenRequest.Content =
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["redirect_uri"] = "http://localhost:5068/api/silpo/callback",
                    
                    ["code_verifier"] = codeVerifier
                });

        var response =
            await httpClient.SendAsync(tokenRequest);

        var content =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode(
                (int)response.StatusCode,
                content);
        }

        using var json =
            JsonDocument.Parse(content);

        var accessToken =
            json.RootElement
                .GetProperty("access_token")
                .GetString();

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return BadRequest(
                "Access token не отримано.");
        }

        _tokenStore.AccessToken =
            accessToken;

        return Redirect("/") ;
        //return Ok(new { message = "Авторизація успішна. Access token отримано." });
    }

    // ============================================================
    // TOOLS
    // ============================================================

    [HttpGet("tools")]
    public async Task<IActionResult> GetTools(
        [FromServices] SilpoMcpService silpoMcpService)
    {
        var token = _tokenStore.AccessToken;

        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new
            {
                message =
                    "Спочатку авторизуйтесь через /api/silpo/login"
            });
        }

        try
        {
            var result =
                await silpoMcpService.GetToolsAsync(token);

            return Content(
                result,
                "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message =
                    "Помилка отримання MCP tools.",
                error = ex.Message
            });
        }
    }

    // ============================================================
    // DELIVERY
    // ============================================================

    [HttpGet("delivery")]
    public async Task<IActionResult> GetDelivery(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromServices] SilpoMcpService silpoMcpService)
    {
        if (string.IsNullOrWhiteSpace(
            _tokenStore.AccessToken))
        {
            return Unauthorized(
                "Спочатку авторизуйтесь через /api/silpo/login");
        }

        if (latitude == 0 || longitude == 0)
        {
            return BadRequest(
                "Не вказано координати адреси.");
        }

        try
        {
            var result =
                await silpoMcpService
                    .GetAvailableDeliveryTypesAsync(
                        _tokenStore.AccessToken,
                        latitude,
                        longitude);

            return Content(
                result,
                "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message =
                    "Помилка отримання способів доставки.",
                error = ex.Message
            });
        }
    }

    // ============================================================
    // TIME SLOTS
    // ============================================================

    [HttpGet("time-slots")]
    public async Task<IActionResult> GetTimeSlots(
        [FromQuery] string branchId,
        [FromQuery] string deliveryType,
        [FromServices] SilpoMcpService silpoMcpService)
    {
        if (string.IsNullOrWhiteSpace(
            _tokenStore.AccessToken))
        {
            return Unauthorized(
                "Спочатку авторизуйтесь через /api/silpo/login");
        }

        if (string.IsNullOrWhiteSpace(branchId))
        {
            return BadRequest(
                "Не вказано branchId.");
        }

        if (string.IsNullOrWhiteSpace(deliveryType))
        {
            return BadRequest(
                "Не вказано deliveryType.");
        }

        var result =
            await silpoMcpService.GetTimeSlotsAsync(
                _tokenStore.AccessToken,
                branchId,
                deliveryType);

        return Content(
            result,
            "application/json");
    }

    // ============================================================
    // PRODUCTS - AUTOMATIC
    // ============================================================




    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(
[FromServices] SilpoMcpService silpoMcpService,
[FromQuery] string[]? products)
    {
        var token = _tokenStore.AccessToken;

// ============================================
// 0. Перевірка авторизації
// ============================================

if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Спочатку авторизуйтесь через /api/silpo/login"
            });
        }

        // ============================================
        // 1. Якщо товари не передані
        // ============================================

        if (products == null || products.Length == 0)
        {
            products =
            [
                "Молоко",
        "Хліб",
        "Яйця"
            ];
        }

        // ============================================
        // 2. Максимум 30 товарів
        // ============================================

        if (products.Length > 30)
        {
            return BadRequest(new
            {
                success = false,
                message = "Максимум можна шукати 30 товарів."
            });
        }

        try
        {
            // ============================================
            // 3. Отримуємо актуальні time slots
            // ============================================

            var slotsResult =
                await silpoMcpService.GetTimeSlotsAsync(
                    token,
                    DefaultBranchId,
                    DefaultDeliveryType);

            if (string.IsNullOrWhiteSpace(slotsResult))
            {
                return StatusCode(502, new
                {
                    success = false,
                    message = "Silpo MCP не повернув time slots."
                });
            }

            // ============================================
            // 4. Розбираємо time slots
            // ============================================

            using var slotsDocument =
                JsonDocument.Parse(slotsResult);

            var root = slotsDocument.RootElement;

            if (!root.TryGetProperty(
                    "success",
                    out var successElement) ||
                successElement.ValueKind != JsonValueKind.True)
            {
                return StatusCode(502, new
                {
                    success = false,
                    message = "Не вдалося отримати time slots.",
                    details = slotsResult
                });
            }

            if (!root.TryGetProperty(
                    "slots",
                    out var slots) ||
                slots.ValueKind != JsonValueKind.Array)
            {
                return StatusCode(502, new
                {
                    success = false,
                    message = "У відповіді Silpo MCP немає slots."
                });
            }

            // ============================================
            // 5. Вибираємо перший доступний слот
            // ============================================

            string? selectedDate = null;
            string? selectedStart = null;
            string? selectedEnd = null;
            string? selectedTime = null;

            foreach (var slot in slots.EnumerateArray())
            {
                if (!slot.TryGetProperty(
                        "date",
                        out var dateElement))
                {
                    continue;
                }

                if (!slot.TryGetProperty(
                        "start",
                        out var startElement))
                {
                    continue;
                }

                if (!slot.TryGetProperty(
                        "end",
                        out var endElement))
                {
                    continue;
                }

                var date =
                    dateElement.ValueKind == JsonValueKind.String
                        ? dateElement.GetString()
                        : null;

                var start =
                    startElement.ValueKind == JsonValueKind.String
                        ? startElement.GetString()
                        : null;

                var end =
                    endElement.ValueKind == JsonValueKind.String
                        ? endElement.GetString()
                        : null;

                if (string.IsNullOrWhiteSpace(date) ||
                    string.IsNullOrWhiteSpace(start) ||
                    string.IsNullOrWhiteSpace(end))
                {
                    continue;
                }

                selectedDate = date;
                selectedStart = $"{date} {start}";
                selectedEnd = $"{date} {end}";
                selectedTime = $"{start}–{end}";

                break;
            }

            // ============================================
            // 6. Якщо немає доступного часу
            // ============================================

            if (selectedStart == null ||
                selectedEnd == null)
            {
                return Ok(new
                {
                    success = false,
                    message =
                        "Silpo MCP повернув slots, але немає доступного часу.",
                    branchId = DefaultBranchId,
                    deliveryType = DefaultDeliveryType
                });
            }

            // ============================================
            // 7. Перетворюємо дату початку
            // ============================================

            if (!DateTime.TryParseExact(
                    selectedStart,
                    "dd.MM.yyyy HH:mm",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var startLocal))
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        $"Не вдалося розібрати start: {selectedStart}"
                });
            }

            // ============================================
            // 8. Перетворюємо дату кінця
            // ============================================

            if (!DateTime.TryParseExact(
                    selectedEnd,
                    "dd.MM.yyyy HH:mm",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var endLocal))
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        $"Не вдалося розібрати end: {selectedEnd}"
                });
            }

            // ============================================
            // 9. Київський часовий пояс
            // ============================================

            var kyivOffset =
                new TimeSpan(3, 0, 0);

            var startWithOffset =
                new DateTimeOffset(
                    DateTime.SpecifyKind(
                        startLocal,
                        DateTimeKind.Unspecified),
                    kyivOffset);

            var endWithOffset =
                new DateTimeOffset(
                    DateTime.SpecifyKind(
                        endLocal,
                        DateTimeKind.Unspecified),
                    kyivOffset);

            var timeslotStart =
                startWithOffset.ToString(
                    "yyyy-MM-dd'T'HH:mm:sszzz");

            var timeslotEnd =
                endWithOffset.ToString(
                    "yyyy-MM-dd'T'HH:mm:sszzz");

            // ============================================
            // 10. Шукаємо товари в Silpo
            // ============================================

            var productsResult =
                await silpoMcpService.FindProductsAsync(
                    token,
                    DefaultBranchId,
                    DefaultDeliveryType,
                    timeslotStart,
                    timeslotEnd,
                    products);

            if (string.IsNullOrWhiteSpace(productsResult))
            {
                return StatusCode(502, new
                {
                    success = false,
                    message =
                        "Silpo MCP не повернув результат пошуку товарів."
                });
            }

            // ============================================
            // 11. Розбираємо JSON від MCP
            // ============================================

            JsonElement actualResult;

            using (var productsDocument =
                   JsonDocument.Parse(productsResult))
            {
                var productsRoot =
                    productsDocument.RootElement;

                // ВАЖЛИВО:
                // Clone() дозволяє використовувати JsonElement
                // після Dispose JsonDocument.

                if (productsRoot.TryGetProperty(
                        "result",
                        out var resultElement))
                {
                    actualResult =
                        resultElement.Clone();
                }
                else
                {
                    actualResult =
                        productsRoot.Clone();
                }
            }

            // ============================================
            // 12. Обробка MCP result/content/text
            // ============================================

            if (actualResult.TryGetProperty(
                    "content",
                    out var contentElement) &&
                contentElement.ValueKind == JsonValueKind.Array &&
                contentElement.GetArrayLength() > 0)
            {
                var firstContent =
                    contentElement[0];

                if (firstContent.TryGetProperty(
                        "text",
                        out var textElement) &&
                    textElement.ValueKind == JsonValueKind.String)
                {
                    var text =
                        textElement.GetString();

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        try
                        {
                            using var innerDocument =
                                JsonDocument.Parse(text);

                            actualResult =
                                innerDocument.RootElement.Clone();
                        }
                        catch (JsonException)
                        {
                            // text не є JSON.
                            // Залишаємо actualResult без змін.
                        }
                    }
                }
            }

            // ============================================
            // 13. Створюємо компактний список товарів
            // ============================================

            var simplifiedProducts =
                new List<object>();

            if (actualResult.TryGetProperty(
                    "queries",
                    out var queriesElement) &&
                queriesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var queryElement
                         in queriesElement.EnumerateArray())
                {
                    // ------------------------------------
                    // Назва пошукового запиту
                    // ------------------------------------

                    string query = "";

                    if (queryElement.TryGetProperty(
                            "query",
                            out var queryProperty) &&
                        queryProperty.ValueKind ==
                            JsonValueKind.String)
                    {
                        query =
                            queryProperty.GetString() ?? "";
                    }

                    // ------------------------------------
                    // Масив товарів
                    // ------------------------------------

                    if (!queryElement.TryGetProperty(
                            "products",
                            out var productsArray))
                    {
                        continue;
                    }

                    if (productsArray.ValueKind !=
                        JsonValueKind.Array)
                    {
                        continue;
                    }

                    // Максимум 5 товарів
                    // на один пошуковий запит
                    var count = 0;

                    foreach (var product
                             in productsArray.EnumerateArray())
                    {
                        if (count >= 5)
                        {
                            break;
                        }

                        // =================================
                        // NAME
                        // =================================

                        string? name = null;

                        if (product.TryGetProperty(
                                "name",
                                out var nameElement) &&
                            nameElement.ValueKind ==
                                JsonValueKind.String)
                        {
                            name =
                                nameElement.GetString();
                        }

                        // =================================
                        // PRICE
                        // =================================

                        decimal? price = null;

                        if (product.TryGetProperty(
                                "price",
                                out var priceElement))
                        {
                            if (priceElement.ValueKind ==
                                JsonValueKind.Number)
                            {
                                if (priceElement.TryGetDecimal(
                                        out var numberPrice))
                                {
                                    price = numberPrice;
                                }
                            }
                            else if (
                                priceElement.ValueKind ==
                                JsonValueKind.String)
                            {
                                if (decimal.TryParse(
                                        priceElement.GetString(),
                                        NumberStyles.Any,
                                        CultureInfo.InvariantCulture,
                                        out var stringPrice))
                                {
                                    price = stringPrice;
                                }
                            }
                        }

                        // =================================
                        // OLD PRICE
                        // =================================

                        decimal? oldPrice = null;

                        if (product.TryGetProperty(
                                "oldPrice",
                                out var oldPriceElement))
                        {
                            if (oldPriceElement.ValueKind ==
                                JsonValueKind.Number)
                            {
                                if (oldPriceElement.TryGetDecimal(
                                        out var numberOldPrice))
                                {
                                    oldPrice = numberOldPrice;
                                }
                            }
                            else if (
                                oldPriceElement.ValueKind ==
                                JsonValueKind.String)
                            {
                                if (decimal.TryParse(
                                        oldPriceElement.GetString(),
                                        NumberStyles.Any,
                                        CultureInfo.InvariantCulture,
                                        out var stringOldPrice))
                                {
                                    oldPrice = stringOldPrice;
                                }
                            }
                        }

                        // =================================
                        // STOCK
                        // =================================

                        int? stock = null;

                        if (product.TryGetProperty(
                                "stock",
                                out var stockElement))
                        {
                            if (stockElement.ValueKind ==
                                JsonValueKind.Number)
                            {
                                if (stockElement.TryGetInt32(
                                        out var numberStock))
                                {
                                    stock = numberStock;
                                }
                                else if (
                                    stockElement.TryGetInt64(
                                        out var longStock))
                                {
                                    stock =
                                        (int)longStock;
                                }
                            }
                            else if (
                                stockElement.ValueKind ==
                                JsonValueKind.String)
                            {
                                if (int.TryParse(
                                        stockElement.GetString(),
                                        NumberStyles.Integer,
                                        CultureInfo.InvariantCulture,
                                        out var stringStock))
                                {
                                    stock = stringStock;
                                }
                            }
                        }

                        // =================================
                        // AVAILABLE
                        // =================================

                        bool? available = null;

                        if (product.TryGetProperty(
                                "available",
                                out var availableElement))
                        {
                            if (availableElement.ValueKind ==
                                JsonValueKind.True)
                            {
                                available = true;
                            }
                            else if (
                                availableElement.ValueKind ==
                                JsonValueKind.False)
                            {
                                available = false;
                            }
                            else if (
                                availableElement.ValueKind ==
                                JsonValueKind.String)
                            {
                                if (bool.TryParse(
                                        availableElement.GetString(),
                                        out var stringAvailable))
                                {
                                    available =
                                        stringAvailable;
                                }
                            }
                        }

                        // =================================
                        // IMAGE
                        // =================================

                        string? image = null;

                        if (product.TryGetProperty(
                                "image",
                                out var imageElement) &&
                            imageElement.ValueKind ==
                                JsonValueKind.String)
                        {
                            image =
                                imageElement.GetString();
                        }

                        // =================================
                        // DISPLAY RATIO
                        // =================================

                        string? displayRatio = null;

                        if (product.TryGetProperty(
                                "displayRatio",
                                out var ratioElement) &&
                            ratioElement.ValueKind ==
                                JsonValueKind.String)
                        {
                            displayRatio =
                                ratioElement.GetString();
                        }

                        // =================================
                        // SLUG
                        // =================================

                        string? slug = null;

                        if (product.TryGetProperty(
                                "slug",
                                out var slugElement) &&
                            slugElement.ValueKind ==
                                JsonValueKind.String)
                        {
                            slug =
                                slugElement.GetString();
                        }

                        // =================================
                        // Додаємо товар
                        // =================================

                        simplifiedProducts.Add(new
                        {
                            query,
                            name,
                            price,
                            oldPrice,
                            stock,
                            available,
                            displayRatio,
                            image,
                            slug
                        });

                        count++;
                    }
                }
            }

            // ============================================
            // 14. Фінальна відповідь
            // ============================================

            return new JsonResult(
                new
                {
                    success = true,

                    branchId = DefaultBranchId,

                    deliveryType = DefaultDeliveryType,

                    timeslot = new
                    {
                        date = selectedDate,
                        time = selectedTime,
                        start = timeslotStart,
                        end = timeslotEnd
                    },

                    requestedProducts = products,

                    totalProducts =
                        simplifiedProducts.Count,

                    products =
                        simplifiedProducts
                },
                new JsonSerializerOptions
                {
                    Encoder =
                        System.Text.Encodings.Web
                            .JavaScriptEncoder
                            .UnsafeRelaxedJsonEscaping,

                    WriteIndented = true
                });
        }
        catch (JsonException ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message =
                    "Помилка обробки JSON від Silpo MCP.",
                error = ex.Message
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message =
                    "Помилка отримання товарів Silpo.",
                error = ex.Message
            });
        }

    }
    
    [Route("api/silpo")]
    [HttpGet("status")]
    public IActionResult Status()
    {
        var authenticated =
            !string.IsNullOrWhiteSpace(_tokenStore.AccessToken);

        return Ok(new
        {
            authenticated
        });
    }
}

