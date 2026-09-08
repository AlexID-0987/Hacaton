
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Hacaton.Services;

public class AiAgentService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly SilpoMcpService _silpoMcpService;
    private readonly SilpoTokenStore _tokenStore;

    private const string OpenRouterUrl =
        "https://openrouter.ai/api/v1/chat/completions";

    public AiAgentService(
        HttpClient httpClient,
        IConfiguration configuration,
        SilpoMcpService silpoMcpService,
        SilpoTokenStore tokenStore)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _silpoMcpService = silpoMcpService;
        _tokenStore = tokenStore;
    }

    public async Task<string> AskAsync(string userMessage, string? address)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = "Повідомлення не може бути порожнім."
            });
        }

        var accessToken = _tokenStore.AccessToken;

        if (string.IsNullOrWhiteSpace(address))
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = "Вкажіть адресу доставки Silpo."
            });
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = "Вкажіть адресу доставки Silpo."
            });
        }

        try
        {
            // =====================================================
            // 1. ВИЗНАЧАЄМО ФІЛІЮ SILPO
            // =====================================================

            Console.WriteLine();
            Console.WriteLine(
                "========== INITIALIZE SILPO LOCATION ==========");

            Console.WriteLine(
                $"Address: {address}");

            Console.WriteLine(
                "================================================");

            await _silpoMcpService
                .InitializeBranchByAddressAsync(
                    accessToken,
                    address);

            Console.WriteLine(
                $"Selected BranchId: {_tokenStore.BranchId}");

            // =====================================================
            // 2. ДАЛІ ВЖЕ ЙДЕ ТВОЯ ІСНУЮЧА ЛОГІКА
            // =====================================================

            Console.WriteLine();
            Console.WriteLine(
                "========== AI REQUEST ==========");

            Console.WriteLine(
                userMessage);

            Console.WriteLine(
                $"BranchId: {_tokenStore.BranchId}");

            Console.WriteLine(
                "===============================");
            // ---------------------------------------------------------

            var localRequest =
                TryParseSimpleProductRequest(userMessage);

            ProductRequest? productRequest;

            if (localRequest != null)
            {
                productRequest = localRequest;

                Console.WriteLine("LOCAL PRODUCT PARSER:");
                Console.WriteLine(
                    $"Products: {string.Join(", ", productRequest.Products)}");
                Console.WriteLine(
                    $"Budget: {productRequest.Budget}");
            }
            else
            {
                productRequest =
                    await ExtractProductRequestAsync(userMessage);
            }

            // ---------------------------------------------------------
            // 2. Якщо це не запит товарів — звичайна відповідь AI.
            // ---------------------------------------------------------

            if (productRequest == null ||
                !productRequest.IsProductRequest ||
                productRequest.Products.Count == 0)
            {
                var generalAnswer =
                    await AskGeneralAiAsync(userMessage);

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    answer = generalAnswer,
                    branchId = _tokenStore.BranchId
                });
            }

            Console.WriteLine();
            Console.WriteLine("========== PRODUCT REQUEST ==========");
            Console.WriteLine(
                $"Products: {string.Join(", ", productRequest.Products)}");
            Console.WriteLine($"Budget: {productRequest.Budget}");
            Console.WriteLine("=====================================");
            Console.WriteLine();

            // ---------------------------------------------------------
            // 3. Отримуємо доступні слоти доставки.
            // ---------------------------------------------------------

            var timeSlotsRaw =
                await _silpoMcpService.GetTimeSlotsAsync(
                    accessToken,
                    "DeliveryHome");

            var timeSlot =
                ExtractFirstAvailableTimeSlot(timeSlotsRaw);

            if (timeSlot == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message =
                        "Для вашої адреси зараз немає доступного часу доставки.",
                    branchId = _tokenStore.BranchId
                });
            }

            Console.WriteLine();
            Console.WriteLine("========== TIME SLOT ==========");
            Console.WriteLine($"Date:  {timeSlot.Date}");
            Console.WriteLine($"Start: {timeSlot.StartIso}");
            Console.WriteLine($"End:   {timeSlot.EndIso}");
            Console.WriteLine($"Time:  {timeSlot.DisplayTime}");
            Console.WriteLine("==============================");
            Console.WriteLine();

            // ---------------------------------------------------------
            // 4. Запитуємо товари саме для BranchId + timeslot.
            // ---------------------------------------------------------

            var productsRaw =
                await _silpoMcpService.FindProductsAsync(
                    accessToken,
                    "DeliveryHome",
                    timeSlot.StartIso,
                    timeSlot.EndIso,
                    productRequest.Products.ToArray());

            Console.WriteLine();
            Console.WriteLine("========== SILPO PRODUCTS ==========");
            Console.WriteLine(productsRaw);
            Console.WriteLine("====================================");
            Console.WriteLine();

            // ---------------------------------------------------------
            // 5. Парсимо реальні товари Silpo.
            // ---------------------------------------------------------

            var products =
                ParseProducts(productsRaw);

            if (products.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message =
                        "Silpo не повернув товарів за вашим запитом.",
                    branchId = _tokenStore.BranchId,
                    requestedProducts = productRequest.Products
                });
            }

            var availableProducts =
                products
                    .Where(x => x.Available && x.Stock > 0)
                    .ToList();

            if (availableProducts.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message =
                        "Товари знайдені, але зараз їх немає в наявності.",
                    branchId = _tokenStore.BranchId
                });
            }

            // ---------------------------------------------------------
            // 6. Якщо заданий бюджет, відбираємо товари до бюджету.
            //
            //    Для одного товару це дуже просто:
            //    беремо найдешевший, який вкладається в бюджет.
            //
            //    Для кількох товарів AI вже отримає список реальних
            //    товарів і підбере комбінацію.
            // ---------------------------------------------------------

            var productsForAi = availableProducts
                .OrderBy(x => x.Price)
                .Take(100)
                .ToList();

            if (productRequest.Budget.HasValue &&
                productRequest.Products.Count == 1)
            {
                var budgetProducts =
                    productsForAi
                        .Where(x =>
                            x.Price <= productRequest.Budget.Value)
                        .ToList();

                if (budgetProducts.Count == 0)
                {
                    var cheapest =
                        productsForAi
                            .OrderBy(x => x.Price)
                            .First();

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        answer =
                            $"Найдешевший доступний варіант — {cheapest.Name}, " +
                            $"але він коштує {cheapest.Price:0.00} грн, " +
                            $"що перевищує ваш бюджет " +
                            $"{productRequest.Budget.Value:0.00} грн.",
                        branchId = _tokenStore.BranchId,
                        deliveryType = "DeliveryHome",
                        timeslot = new
                        {
                            date = timeSlot.Date,
                            start = timeSlot.StartIso,
                            end = timeSlot.EndIso,
                            time = timeSlot.DisplayTime
                        },
                        products = new[]
                        {
                            new
                            {
                                name = cheapest.Name,
                                price = cheapest.Price,
                                stock = cheapest.Stock,
                                available = cheapest.Available,
                                displayRatio = cheapest.DisplayRatio,
                                image = cheapest.Image,
                                externalProductId =
                                    cheapest.ExternalProductId
                            }
                        }
                    });
                }

                productsForAi =
                    budgetProducts
                        .OrderBy(x => x.Price)
                        .Take(50)
                        .ToList();
            }

            // ---------------------------------------------------------
            // 7. Формуємо компактний список для OpenRouter.
            // ---------------------------------------------------------

            var productsJson =
                JsonSerializer.Serialize(
                    productsForAi.Select(x => new
                    {
                        name = x.Name,
                        price = x.Price,
                        stock = x.Stock,
                        available = x.Available,
                        package = x.DisplayRatio,
                        image = x.Image
                    }),
                    new JsonSerializerOptions
                    {
                        WriteIndented = false
                    });

            var budgetText =
                productRequest.Budget.HasValue
                    ? productRequest.Budget.Value
                        .ToString(
                            "0.00",
                            CultureInfo.InvariantCulture)
                    : "не вказаний";

            // ---------------------------------------------------------
            // 8. AI отримує ТІЛЬКИ реальні товари Silpo.
            // ---------------------------------------------------------

            var systemPrompt = """
Ти — AI-помічник для покупок у Silpo.

Тобі переданий список РЕАЛЬНИХ товарів, отриманих із Silpo MCP.

КРИТИЧНІ ПРАВИЛА:

1. Використовуй тільки товари з переданого списку.
2. Не вигадуй товари.
3. Не вигадуй ціни.
4. Не вигадуй залишки.
5. Не вигадуй фасування.
6. price — реальна ціна товару у гривнях.
7. stock — реальний залишок.
8. available=true означає, що товар доступний.
9. Якщо користувач вказав бюджет — не перевищуй його, якщо це можливо.
10. Якщо бюджет неможливо виконати — чесно скажи про це.
11. Якщо користувач просить один товар — запропонуй найкращий доступний варіант.
12. Якщо користувач просить кілька товарів — підбери товари зі списку.
13. Не показуй JSON.
14. Не показуй internal ID.
15. Не показуй externalProductId.
16. Відповідай українською.
17. Будь коротким і зрозумілим.
18. Обов'язково вказуй ціну.
19. Якщо доречно — вказуй фасування та залишок.

Формат відповіді:

🛒 Знайшов для вас:

• Назва товару — 74,49 грн (870 г)
  В наявності: 18 шт.

💰 Разом: 74,49 грн
""";

            var userPrompt = $"""
Запит користувача:

{userMessage}

Філія Silpo:

{_tokenStore.BranchId}

Тип доставки:

DeliveryHome

Час доставки:

{timeSlot.DisplayTime}

Максимальний бюджет:

{budgetText} грн

Реальні товари Silpo:

{productsJson}

Сформуй фінальну відповідь українською.

Використовуй тільки товари з цього списку.
Не вигадуй ціни, залишки або товари.
""";

            var aiAnswer =
                await CallOpenRouterAsync(
                    systemPrompt,
                    userPrompt);

            // ---------------------------------------------------------
            // 9. Фінальна відповідь API.
            // ---------------------------------------------------------

            return JsonSerializer.Serialize(
                new
                {
                    success = true,
                    answer = aiAnswer,
                    branchId = _tokenStore.BranchId,
                    deliveryType = "DeliveryHome",
                    timeslot = new
                    {
                        date = timeSlot.Date,
                        start = timeSlot.StartIso,
                        end = timeSlot.EndIso,
                        time = timeSlot.DisplayTime
                    },
                    products =
                        productsForAi.Select(x => new
                        {
                            name = x.Name,
                            price = x.Price,
                            stock = x.Stock,
                            available = x.Available,
                            displayRatio = x.DisplayRatio,
                            image = x.Image,
                            externalProductId =
                                x.ExternalProductId
                        })
                },
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("========== AI ERROR ==========");
            Console.WriteLine(ex);
            Console.WriteLine("==============================");
            Console.WriteLine();

            return JsonSerializer.Serialize(new
            {
                success = false,
                message = "Помилка AI Assistant.",
                error = ex.Message
            });
        }
    }

    // ================================================================
    // ЛОКАЛЬНЕ РОЗПІЗНАВАННЯ ПРОСТИХ ЗАПИТІВ
    // ================================================================

    private ProductRequest? TryParseSimpleProductRequest(
        string userMessage)
    {
        var text =
            userMessage
                .Trim()
                .ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(text))
            return null;

        var productWords =
            new[]
            {
                "молоко",
                "хліб",
                "хлеб",
                "яйця",
                "яйца",
                "сир",
                "масло",
                "кефір",
                "кефир",
                "йогурт",
                "сметана",
                "вода",
                "сік",
                "сок",
                "кава",
                "чай",
                "цукор",
                "борошно",
                "рис",
                "гречка",
                "макарони",
                "картопля",
                "помідори",
                "помидоры",
                "огірки",
                "огурцы",
                "банани",
                "бананы",
                "яблука",
                "яблоки",
                "курка",
                "курятина",
                "ковбаса",
                "шоколад",
                "печиво"
            };

        var foundProducts =
            productWords
                .Where(text.Contains)
                .Distinct()
                .ToList();

        if (foundProducts.Count == 0)
            return null;

        // Нормалізація назв.
        var normalized =
            new List<string>();

        foreach (var product in foundProducts)
        {
            switch (product)
            {
                case "хлеб":
                    normalized.Add("Хліб");
                    break;

                case "яйца":
                    normalized.Add("Яйця");
                    break;

                case "кефир":
                    normalized.Add("Кефір");
                    break;

                case "сок":
                    normalized.Add("Сік");
                    break;

                case "помидоры":
                    normalized.Add("Помідори");
                    break;

                case "огурцы":
                    normalized.Add("Огірки");
                    break;

                case "бананы":
                    normalized.Add("Банани");
                    break;

                case "яблоки":
                    normalized.Add("Яблука");
                    break;

                default:
                    normalized.Add(
                        char.ToUpper(product[0]) +
                        product[1..]);
                    break;
            }
        }

        decimal? budget = null;

        // Пошук:
        // до 100 грн
        // до 100 гривень
        // бюджет 100
        // максимум 100
        var budgetMatch =
            Regex.Match(
                text,
                @"(?:до|бюджет|максимум|не більше|не більш як)\s*(\d+(?:[.,]\d+)?)");

        if (budgetMatch.Success)
        {
            var budgetText =
                budgetMatch.Groups[1]
                    .Value
                    .Replace(',', '.');

            if (decimal.TryParse(
                    budgetText,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var parsedBudget))
            {
                budget = parsedBudget;
            }
        }

        return new ProductRequest
        {
            IsProductRequest = true,
            Products = normalized,
            Budget = budget
        };
    }

    // ================================================================
    // AI ВИЗНАЧЕННЯ ТОВАРНОГО ЗАПИТУ
    // ================================================================

    private async Task<ProductRequest?>
        ExtractProductRequestAsync(
            string userMessage)
    {
        var prompt = """
Визнач, чи хоче користувач знайти продукти в магазині Silpo.

Поверни ТІЛЬКИ JSON.

Формат:

{
  "isProductRequest": true,
  "products": ["Молоко", "Хліб"],
  "budget": 350
}

Або:

{
  "isProductRequest": false,
  "products": [],
  "budget": null
}

Правила:

- Якщо користувач хоче купити, знайти, підібрати або замовити
  будь-який продукт — isProductRequest=true.
- products — товари, які потрібні користувачу.
- budget — максимальний бюджет у гривнях.
- Якщо бюджет не вказаний — null.
- Якщо користувач просить "сніданок", можна сформувати базовий
  список продуктів для сніданку.
- Якщо користувач просто ставить загальне питання — false.
- Не додавай пояснення.
""";

        var result =
            await CallOpenRouterAsync(
                prompt,
                userMessage);

        result = CleanJson(result);

        try
        {
            return JsonSerializer.Deserialize<ProductRequest>(
                result,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Product request parse error: {ex.Message}");

            return null;
        }
    }

    // ================================================================
    // ЗАГАЛЬНА ВІДПОВІДЬ
    // ================================================================

    private async Task<string> AskGeneralAiAsync(
        string userMessage)
    {
        var systemPrompt = """
Ти — дружній AI-помічник для покупок у Silpo.

Відповідай українською.

Будь коротким, корисним і зрозумілим.

Якщо користувач хоче знайти конкретний товар,
не вигадуй його ціну або наявність.
""";

        return await CallOpenRouterAsync(
            systemPrompt,
            userMessage);
    }

    // ================================================================
    // OPENROUTER
    // ================================================================

    private async Task<string> CallOpenRouterAsync(
        string systemPrompt,
        string userPrompt)
    {
        var apiKey =
            _configuration["OpenRouter:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new Exception(
                "OpenRouter:ApiKey не налаштований.");
        }

        var model =
            _configuration["OpenRouter:Model"]
            ?? "openai/gpt-4o-mini";

        var request = new
        {
            model,

            messages = new[]
            {
                new
                {
                    role = "system",
                    content = systemPrompt
                },

                new
                {
                    role = "user",
                    content = userPrompt
                }
            },

            temperature = 0.2,

            max_tokens = 800
        };

        var json =
            JsonSerializer.Serialize(request);

        using var httpRequest =
            new HttpRequestMessage(
                HttpMethod.Post,
                OpenRouterUrl);

        httpRequest.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                apiKey);

        httpRequest.Headers.TryAddWithoutValidation(
            "HTTP-Referer",
            "http://localhost:5068");

        httpRequest.Headers.TryAddWithoutValidation(
            "X-Title",
            "Silpo AI Assistant");

        httpRequest.Content =
            new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

        using var response =
            await _httpClient.SendAsync(
                httpRequest);

        var responseBody =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                $"OpenRouter HTTP {(int)response.StatusCode}: {responseBody}");
        }

        using var document =
            JsonDocument.Parse(responseBody);

        var root =
            document.RootElement;

        if (!root.TryGetProperty(
                "choices",
                out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            throw new Exception(
                "OpenRouter не повернув choices.");
        }

        var message =
            choices[0].GetProperty("message");

        if (!message.TryGetProperty(
                "content",
                out var content))
        {
            throw new Exception(
                "OpenRouter не повернув content.");
        }

        return content.GetString()?.Trim() ?? "";
    }

    // ================================================================
    // PARSE SILPO PRODUCTS
    // ================================================================

    private List<SilpoProduct> ParseProducts(
        string rawResponse)
    {
        var result =
            new List<SilpoProduct>();

        if (string.IsNullOrWhiteSpace(rawResponse))
            return result;

        try
        {
            var json =
                ExtractJson(rawResponse);

            using var document =
                JsonDocument.Parse(json);

            var root =
                document.RootElement;

            JsonElement data;

            if (root.TryGetProperty(
                    "data",
                    out var dataElement))
            {
                data = dataElement;
            }
            else
            {
                data = root;
            }

            FindProductArrays(
                data,
                result);

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"ParseProducts error: {ex.Message}");

            return result;
        }
    }

    private void FindProductArrays(
        JsonElement element,
        List<SilpoProduct> result)
    {
        if (element.ValueKind ==
            JsonValueKind.Object)
        {
            foreach (var property
                     in element.EnumerateObject())
            {
                if (property.Value.ValueKind ==
                    JsonValueKind.Array)
                {
                    foreach (var item
                             in property.Value.EnumerateArray())
                    {
                        if (item.ValueKind !=
                            JsonValueKind.Object)
                        {
                            continue;
                        }

                        if (IsProduct(item))
                        {
                            var product =
                                ParseProduct(item);

                            if (product != null)
                            {
                                result.Add(product);
                            }
                        }
                        else
                        {
                            FindProductArrays(
                                item,
                                result);
                        }
                    }
                }
                else if (
                    property.Value.ValueKind ==
                    JsonValueKind.Object)
                {
                    FindProductArrays(
                        property.Value,
                        result);
                }
            }
        }
        else if (
            element.ValueKind ==
            JsonValueKind.Array)
        {
            foreach (var item
                     in element.EnumerateArray())
            {
                FindProductArrays(
                    item,
                    result);
            }
        }
    }

    private bool IsProduct(
        JsonElement element)
    {
        return
            element.TryGetProperty(
                "name",
                out _)
            &&
            element.TryGetProperty(
                "price",
                out _)
            &&
            element.TryGetProperty(
                "stock",
                out _);
    }

    private SilpoProduct? ParseProduct(
        JsonElement element)
    {
        try
        {
            var name =
                element.TryGetProperty(
                    "name",
                    out var nameElement)
                    ? nameElement.GetString()
                    : null;

            if (string.IsNullOrWhiteSpace(name))
                return null;

            var price =
                element.TryGetProperty(
                    "price",
                    out var priceElement)
                    ? priceElement.GetDecimal()
                    : 0;

            var stock =
                element.TryGetProperty(
                    "stock",
                    out var stockElement)
                    ? stockElement.GetInt32()
                    : 0;

            var available =
                element.TryGetProperty(
                    "available",
                    out var availableElement)
                    &&
                    availableElement.GetBoolean();

            var image =
                element.TryGetProperty(
                    "image",
                    out var imageElement)
                    ? imageElement.GetString()
                    : null;

            var displayRatio =
                element.TryGetProperty(
                    "displayRatio",
                    out var ratioElement)
                    ? ratioElement.GetString()
                    : null;

            long? externalProductId = null;

            if (element.TryGetProperty(
                    "externalProductId",
                    out var externalIdElement))
            {
                if (externalIdElement.ValueKind ==
                    JsonValueKind.Number &&
                    externalIdElement.TryGetInt64(
                        out var id))
                {
                    externalProductId = id;
                }
            }

            return new SilpoProduct
            {
                Name = name,
                Price = price,
                Stock = stock,
                Available = available,
                Image = image,
                DisplayRatio = displayRatio,
                ExternalProductId =
                    externalProductId
            };
        }
        catch
        {
            return null;
        }
    }

    // ================================================================
    // TIME SLOT
    // ================================================================

    private TimeSlotInfo?
        ExtractFirstAvailableTimeSlot(
            string rawResponse)
    {
        try
        {
            var json =
                ExtractJson(rawResponse);

            using var document =
                JsonDocument.Parse(json);

            var root =
                document.RootElement;

            if (!root.TryGetProperty(
                    "slots",
                    out var slots) ||
                slots.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var slot
                     in slots.EnumerateArray())
            {
                var date =
                    slot.TryGetProperty(
                        "date",
                        out var dateElement)
                        ? dateElement.GetString()
                        : null;

                var startIso =
                    slot.TryGetProperty(
                        "startIso",
                        out var startIsoElement)
                        ? startIsoElement.GetString()
                        : null;

                var endIso =
                    slot.TryGetProperty(
                        "endIso",
                        out var endIsoElement)
                        ? endIsoElement.GetString()
                        : null;

                var start =
                    slot.TryGetProperty(
                        "start",
                        out var startElement)
                        ? startElement.GetString()
                        : null;

                var end =
                    slot.TryGetProperty(
                        "end",
                        out var endElement)
                        ? endElement.GetString()
                        : null;

                if (string.IsNullOrWhiteSpace(
                        startIso) ||
                    string.IsNullOrWhiteSpace(
                        endIso))
                {
                    if (!string.IsNullOrWhiteSpace(date) &&
                        !string.IsNullOrWhiteSpace(start) &&
                        !string.IsNullOrWhiteSpace(end))
                    {
                        startIso =
                            BuildKyivIso(
                                date,
                                start);

                        endIso =
                            BuildKyivIso(
                                date,
                                end);
                    }
                }

                if (string.IsNullOrWhiteSpace(startIso) ||
                    string.IsNullOrWhiteSpace(endIso))
                {
                    continue;
                }

                return new TimeSlotInfo
                {
                    Date = date ?? "",
                    StartIso = startIso,
                    EndIso = endIso,
                    DisplayTime =
                        $"{date} {start}–{end}"
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Time slot parse error: {ex.Message}");
        }

        return null;
    }

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
                $"{dateTime:yyyy-MM-dd}T{dateTime:HH:mm:ss}+03:00";
        }

        return $"{date}T{time}:00+03:00";
    }

    // ================================================================
    // JSON HELPERS
    // ================================================================

    private string ExtractJson(
        string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new Exception(
                "Порожня відповідь.");
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
                "У відповіді не знайдено JSON.");
        }

        var start =
            positions.Min();

        return raw[start..].Trim();
    }

    private string CleanJson(
        string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        text = text.Trim();

        if (text.StartsWith("```"))
        {
            text =
                Regex.Replace(
                    text,
                    @"^```(?:json)?\s*",
                    "",
                    RegexOptions.IgnoreCase);

            text =
                Regex.Replace(
                    text,
                    @"\s*```$",
                    "");
        }

        return text.Trim();
    }

    // ================================================================
    // MODELS
    // ================================================================

    private class ProductRequest
    {
        [JsonPropertyName("isProductRequest")]
        public bool IsProductRequest { get; set; }

        [JsonPropertyName("products")]
        public List<string> Products { get; set; } =
            new();

        [JsonPropertyName("budget")]
        public decimal? Budget { get; set; }
    }

    private class SilpoProduct
    {
        public string Name { get; set; } = "";

        public decimal Price { get; set; }

        public int Stock { get; set; }

        public bool Available { get; set; }

        public string? Image { get; set; }

        public string? DisplayRatio { get; set; }

        public long? ExternalProductId { get; set; }
    }

    private class TimeSlotInfo
    {
        public string Date { get; set; } = "";

        public string StartIso { get; set; } = "";

        public string EndIso { get; set; } = "";

        public string DisplayTime { get; set; } = "";
    }
}

