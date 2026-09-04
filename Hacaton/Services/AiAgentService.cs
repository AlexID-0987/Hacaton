
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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

    private const string DefaultModel =
        "google/gemma-4-26b-a4b:free";

    private const string BranchId =
        "1edb6b38-214b-66d6-a8e0-7f2fdd178564";

    private const string DeliveryType =
        "DeliveryHome";

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

    public async Task<string> AskAsync(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return CreateResponse(
                false,
                "Напишіть, що ви хочете купити.",
                0,
                0,
                new List<SilpoProduct>());
        }

        var accessToken = _tokenStore.AccessToken;

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return CreateResponse(
                false,
                "Спочатку авторизуйтесь через Silpo.",
                0,
                0,
                new List<SilpoProduct>());
        }

        decimal budget = ExtractBudget(userMessage);

        if (budget <= 0)
            budget = 1000;

        var productNames = ExtractProductNames(userMessage);

        if (productNames.Count == 0)
        {
            return CreateResponse(
                true,
                "Напишіть, які продукти потрібно підібрати. Наприклад: Підбери продукти для сніданку до 350 грн.",
                budget,
                0,
                new List<SilpoProduct>());
        }

        string timeslotsRaw;

        try
        {
            timeslotsRaw =
                await _silpoMcpService.GetTimeSlotsAsync(
                    accessToken,
                    BranchId,
                    DeliveryType);
        }
        catch (Exception ex)
        {
            return CreateResponse(
                false,
                "Помилка отримання часових слотів Silpo.",
                budget,
                0,
                new List<SilpoProduct>(),
                ex.Message);
        }

        var slot = FindFirstAvailableSlot(timeslotsRaw);

        if (slot == null)
        {
            return CreateResponse(
                false,
                "Не знайдено доступного часу доставки Silpo.",
                budget,
                0,
                new List<SilpoProduct>());
        }

        string productsRaw;

        try
        {
            productsRaw =
                await _silpoMcpService.FindProductsAsync(
                    accessToken,
                    BranchId,
                    DeliveryType,
                    slot.Value.Start,
                    slot.Value.End,
                    productNames.ToArray());
        }
        catch (Exception ex)
        {
            return CreateResponse(
                false,
                "Помилка отримання товарів Silpo.",
                budget,
                0,
                new List<SilpoProduct>(),
                ex.Message);
        }

        var products = ParseProducts(productsRaw);

        if (products.Count == 0)
        {
            return CreateResponse(
                false,
                "Silpo не повернув товарів за вашим запитом.",
                budget,
                0,
                new List<SilpoProduct>());
        }

        var selectedItems =
            SelectProductsWithinBudget(
                products,
                productNames,
                budget);

        var total =
            selectedItems.Sum(x => x.Price);

        string fallbackMessage;

        if (selectedItems.Count == 0)
        {
            fallbackMessage =
                $"Не вдалося підібрати товари в межах {budget:0.##} грн.";
        }
        else
        {
            fallbackMessage =
                $"Підібрано {selectedItems.Count} товарів на суму {total:0.##} грн. Бюджет: {budget:0.##} грн.";
        }

        var aiMessage =
            await GenerateAiMessageAsync(
                userMessage,
                selectedItems,
                budget,
                fallbackMessage);

        var message =
            string.IsNullOrWhiteSpace(aiMessage)
                ? fallbackMessage
                : aiMessage;

        return CreateResponse(
            true,
            message,
            budget,
            total,
            selectedItems);
    }

    // =========================================================
    // ВИЗНАЧЕННЯ ТОВАРІВ ІЗ ЗАПИТУ КОРИСТУВАЧА
    // =========================================================

    private List<string> ExtractProductNames(string message)
    {
        var result = new List<string>();

        var text = message.ToLowerInvariant();

        // =====================================================
        // СНІДАНОК
        // =====================================================

        if (text.Contains("снідан"))
        {
            AddIfMissing(result, "Яйця");
            AddIfMissing(result, "Молоко");
            AddIfMissing(result, "Хліб");
            AddIfMissing(result, "Сир");
            AddIfMissing(result, "Масло");
            AddIfMissing(result, "Вівсянка");
            AddIfMissing(result, "Банан");
            AddIfMissing(result, "Йогурт");
        }


        // =====================================================
        // ОБІД
        // =====================================================

        if (text.Contains("обід"))
        {
            AddIfMissing(result, "Курка");
            AddIfMissing(result, "Картопля");
            AddIfMissing(result, "Помідори");
            AddIfMissing(result, "Огірки");
            AddIfMissing(result, "Хліб");
            AddIfMissing(result, "Сир");
            AddIfMissing(result, "Йогурт");
        }


        // =====================================================
        // ВЕЧЕРЯ
        // =====================================================

        if (text.Contains("вечер"))
        {
            AddIfMissing(result, "Курка");
            AddIfMissing(result, "Картопля");
            AddIfMissing(result, "Помідори");
            AddIfMissing(result, "Огірки");
            AddIfMissing(result, "Сир");
            AddIfMissing(result, "Йогурт");
            AddIfMissing(result, "Овочі");
        }


        // =====================================================
        // ЗДОРОВЕ ХАРЧУВАННЯ
        // =====================================================

        if (text.Contains("здоров"))
        {
            AddIfMissing(result, "Вівсянка");
            AddIfMissing(result, "Яблука");
            AddIfMissing(result, "Банан");
            AddIfMissing(result, "Йогурт");
            AddIfMissing(result, "Сир");
            AddIfMissing(result, "Огірки");
            AddIfMissing(result, "Помідори");
        }


        // =====================================================
        // ЗДОРОВІ ПРОДУКТИ
        // =====================================================

        if (text.Contains("корисн"))
        {
            AddIfMissing(result, "Вівсянка");
            AddIfMissing(result, "Яблука");
            AddIfMissing(result, "Банан");
            AddIfMissing(result, "Йогурт");
            AddIfMissing(result, "Сир");
            AddIfMissing(result, "Огірки");
            AddIfMissing(result, "Помідори");
        }


        // =====================================================
        // ПІКНІК
        // =====================================================

        if (text.Contains("пікнік") ||
            text.Contains("пікніку"))
        {
            AddIfMissing(result, "Хліб");
            AddIfMissing(result, "Сир");
            AddIfMissing(result, "Курка");
            AddIfMissing(result, "Огірки");
            AddIfMissing(result, "Помідори");
            AddIfMissing(result, "Яблука");
            AddIfMissing(result, "Йогурт");
        }


        // =====================================================
        // ОКРЕМІ ПРОДУКТИ
        // =====================================================

        if (text.Contains("яйц"))
            AddIfMissing(result, "Яйця");

        if (text.Contains("молок"))
            AddIfMissing(result, "Молоко");

        if (text.Contains("хліб"))
            AddIfMissing(result, "Хліб");

        if (text.Contains("сир"))
            AddIfMissing(result, "Сир");

        if (text.Contains("масл"))
            AddIfMissing(result, "Масло");

        if (text.Contains("йогурт"))
            AddIfMissing(result, "Йогурт");

        if (text.Contains("вівся"))
            AddIfMissing(result, "Вівсянка");

        if (text.Contains("кав"))
            AddIfMissing(result, "Кава");

        if (text.Contains("чай"))
            AddIfMissing(result, "Чай");

        if (text.Contains("банан"))
            AddIfMissing(result, "Банан");

        if (text.Contains("яблу"))
            AddIfMissing(result, "Яблука");

        if (text.Contains("помід"))
            AddIfMissing(result, "Помідори");

        if (text.Contains("огір"))
            AddIfMissing(result, "Огірки");

        if (text.Contains("картоп"))
            AddIfMissing(result, "Картопля");

        if (text.Contains("курк"))
            AddIfMissing(result, "Курка");


        // =====================================================
        // "КУПИТИ ..."
        // =====================================================

        if (text.Contains("купити"))
        {
            var match =
                Regex.Match(
                    message,
                    @"купити\s+(.+?)(?:\s+до\s+\d+(?:[.,]\d+)?|\s+за\s+\d+(?:[.,]\d+)?|\s+бюджет\s+\d+(?:[.,]\d+)?|$)",
                    RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var requested =
                    match.Groups[1].Value.Split(
                        new[] { ',', ';' },
                        StringSplitOptions.RemoveEmptyEntries);

                foreach (var item in requested)
                {
                    var name = item.Trim();

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        AddIfMissing(result, name);
                    }
                }
            }
        }


        return result;
    }

    private static void AddIfMissing(
        List<string> list,
        string value)
    {
        if (!list.Any(x =>
                x.Equals(
                    value,
                    StringComparison.OrdinalIgnoreCase)))
        {
            list.Add(value);
        }
    }

    // =========================================================
    // БЮДЖЕТ
    // =========================================================

    private static decimal ExtractBudget(string message)
    {
        var match =
            Regex.Match(
                message,
                @"(?:до|бюджет|за|менше)\s*(\d+(?:[.,]\d+)?)",
                RegexOptions.IgnoreCase);

        if (!match.Success)
            return 0;

        var value =
            match.Groups[1].Value.Replace(',', '.');

        if (decimal.TryParse(
                value,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var budget))
        {
            return budget;
        }

        return 0;
    }

    // =========================================================
    // TIME SLOT
    // =========================================================

    private static (string Start, string End)? FindFirstAvailableSlot(
        string response)
    {
        try
        {
            using var document =
                JsonDocument.Parse(response);

            var root = document.RootElement;

            if (!root.TryGetProperty(
                    "success",
                    out var success))
            {
                return null;
            }

            if (!success.GetBoolean())
                return null;

            if (!root.TryGetProperty(
                    "slots",
                    out var slots))
            {
                return null;
            }

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
                    dateElement.GetString();

                var start =
                    startElement.GetString();

                var end =
                    endElement.GetString();

                if (string.IsNullOrWhiteSpace(date) ||
                    string.IsNullOrWhiteSpace(start) ||
                    string.IsNullOrWhiteSpace(end))
                {
                    continue;
                }

                return (
                    $"{date} {start}",
                    $"{date} {end}"
                );
            }
        }
        catch
        {
        }

        return null;
    }

    // =========================================================
    // ПАРСИНГ ТОВАРІВ SILPO
    // =========================================================

    private static List<SilpoProduct> ParseProducts(
        string rawResponse)
    {
        var result =
            new List<SilpoProduct>();

        try
        {
            var json =
                ExtractJson(rawResponse);

            if (string.IsNullOrWhiteSpace(json))
                return result;

            using var document =
                JsonDocument.Parse(json);

            ParseProductsFromElement(
                document.RootElement,
                result);
        }
        catch
        {
        }

        return result
            .GroupBy(
                x => x.Name,
                StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
    }

    private static void ParseProductsFromElement(
        JsonElement element,
        List<SilpoProduct> result)
    {
        if (element.ValueKind ==
            JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                ParseProductsFromElement(
                    item,
                    result);
            }

            return;
        }

        if (element.ValueKind !=
            JsonValueKind.Object)
        {
            return;
        }

        string? name = null;

        if (element.TryGetProperty(
                "name",
                out var nameElement))
        {
            name =
                GetStringValue(nameElement);
        }

        if (string.IsNullOrWhiteSpace(name) &&
            element.TryGetProperty(
                "productName",
                out var productNameElement))
        {
            name =
                GetStringValue(productNameElement);
        }

        decimal? price = null;

        if (element.TryGetProperty(
                "price",
                out var priceElement))
        {
            price =
                ReadDecimal(priceElement);
        }

        if (price == null &&
            element.TryGetProperty(
                "currentPrice",
                out var currentPriceElement))
        {
            price =
                ReadDecimal(currentPriceElement);
        }

        bool available = true;

        if (element.TryGetProperty(
                "available",
                out var availableElement))
        {
            if (availableElement.ValueKind ==
                JsonValueKind.False)
            {
                available = false;
            }
        }

        string? stock = null;

        if (element.TryGetProperty(
                "stock",
                out var stockElement))
        {
            stock =
                GetStringValue(stockElement);
        }

        // ==============================
        // ФОТО
        // ==============================

        string? image = null;

        if (element.TryGetProperty(
                "image",
                out var imageElement))
        {
            image =
                GetStringValue(imageElement);
        }

        // ==============================
        // ДОДАТКОВІ ДАНІ
        // ==============================

        string? oldPrice = null;

        if (element.TryGetProperty(
                "oldPrice",
                out var oldPriceElement))
        {
            oldPrice =
                GetStringValue(oldPriceElement);
        }

        string? displayRatio = null;

        if (element.TryGetProperty(
                "displayRatio",
                out var displayRatioElement))
        {
            displayRatio =
                GetStringValue(displayRatioElement);
        }

        string? slug = null;

        if (element.TryGetProperty(
                "slug",
                out var slugElement))
        {
            slug =
                GetStringValue(slugElement);
        }

        // ==============================
        // ДОДАЄМО ТОВАР
        // ==============================

        if (!string.IsNullOrWhiteSpace(name) &&
            price.HasValue &&
            price.Value > 0)
        {
            result.Add(
                new SilpoProduct
                {
                    Name = name,
                    Price = price.Value,
                    Available = available,
                    Stock = stock,
                    Image = image,
                    OldPrice = oldPrice,
                    DisplayRatio = displayRatio,
                    Slug = slug
                });
        }

        // ==============================
        // РЕКУРСИВНИЙ ПОШУК ТОВАРІВ
        // ==============================

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(
                    "products",
                    StringComparison.OrdinalIgnoreCase) ||
                property.Name.Equals(
                    "items",
                    StringComparison.OrdinalIgnoreCase) ||
                property.Name.Equals(
                    "results",
                    StringComparison.OrdinalIgnoreCase) ||
                property.Name.Equals(
                    "queries",
                    StringComparison.OrdinalIgnoreCase))
            {
                ParseProductsFromElement(
                    property.Value,
                    result);
            }
        }
    }

    private static string? GetStringValue(
        JsonElement element)
    {
        if (element.ValueKind ==
            JsonValueKind.String)
        {
            return element.GetString();
        }

        if (element.ValueKind ==
            JsonValueKind.Number)
        {
            return element.ToString();
        }

        return null;
    }

    private static decimal? ReadDecimal(
        JsonElement element)
    {
        try
        {
            if (element.ValueKind ==
                JsonValueKind.Number)
            {
                return element.GetDecimal();
            }

            if (element.ValueKind ==
                JsonValueKind.String)
            {
                var text =
                    element.GetString();

                if (string.IsNullOrWhiteSpace(text))
                    return null;

                text =
                    text
                        .Replace(
                            "грн",
                            "",
                            StringComparison.OrdinalIgnoreCase)
                        .Trim()
                        .Replace(',', '.');

                if (decimal.TryParse(
                        text,
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out var value))
                {
                    return value;
                }
            }
        }
        catch
        {
        }

        return null;
    }

    // =========================================================
    // ВИТЯГУЄМО JSON
    // =========================================================

    private static string ExtractJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var text =
            raw.Trim();

        if (text.StartsWith(
                "HTTP ",
                StringComparison.OrdinalIgnoreCase))
        {
            var newline =
                text.IndexOf('\n');

            if (newline >= 0)
            {
                text =
                    text[(newline + 1)..].Trim();
            }
        }

        if (text.StartsWith("```"))
        {
            var newline =
                text.IndexOf('\n');

            if (newline >= 0)
            {
                text =
                    text[(newline + 1)..];
            }

            var ending =
                text.LastIndexOf("```");

            if (ending >= 0)
            {
                text =
                    text[..ending];
            }
        }

        text =
            text.Trim();

        var firstBrace =
            text.IndexOf('{');

        var lastBrace =
            text.LastIndexOf('}');

        if (firstBrace >= 0 &&
            lastBrace > firstBrace)
        {
            return text[
                firstBrace..(lastBrace + 1)];
        }

        var firstArray =
            text.IndexOf('[');

        var lastArray =
            text.LastIndexOf(']');

        if (firstArray >= 0 &&
            lastArray > firstArray)
        {
            return text[
                firstArray..(lastArray + 1)];
        }

        return text;
    }

    // =========================================================
    // ВИБІР ТОВАРІВ
    // =========================================================

    private static List<SilpoProduct>
        SelectProductsWithinBudget(
            List<SilpoProduct> products,
            List<string> requestedProducts,
            decimal budget)
    {
        var selected =
            new List<SilpoProduct>();

        var available =
            products
                .Where(x => x.Available)
                .Where(x => x.Price > 0)
                .ToList();

        foreach (var requested in requestedProducts)
        {
            var match =
                FindBestProduct(
                    available,
                    requested);

            if (match == null)
                continue;

            if (selected.Any(x =>
                    x.Name.Equals(
                        match.Name,
                        StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var currentTotal =
                selected.Sum(x => x.Price);

            if (currentTotal + match.Price <= budget)
            {
                selected.Add(match);
            }
        }

        return selected;
    }

    // =========================================================
    // ПОШУК НАЙКРАЩОГО ТОВАРУ
    // =========================================================

    private static SilpoProduct? FindBestProduct(
        List<SilpoProduct> products,
        string requested)
    {
        var candidates =
            products
                .Where(x =>
                    x.Name.Contains(
                        requested,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

        if (candidates.Count == 0)
        {
            var requestedWords =
                requested
                    .Split(
                        ' ',
                        StringSplitOptions.RemoveEmptyEntries);

            candidates =
                products
                    .Where(x =>
                        requestedWords.Any(word =>
                            x.Name.Contains(
                                word,
                                StringComparison.OrdinalIgnoreCase)))
                    .ToList();
        }

        if (candidates.Count == 0)
            return null;

        // Виключаємо дитячі товари,
        // якщо користувач явно їх не просив.
        var normalProducts =
            candidates
                .Where(x => !IsChildProduct(x.Name))
                .ToList();

        if (normalProducts.Count > 0)
            candidates = normalProducts;

        // Для звичайних яєць надаємо перевагу курячим.
        if (requested.Equals(
                "Яйця",
                StringComparison.OrdinalIgnoreCase))
        {
            var chickenEggs =
                candidates
                    .Where(x =>
                        x.Name.Contains(
                            "куряч",
                            StringComparison.OrdinalIgnoreCase) ||
                        x.Name.Contains(
                            "курячі",
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();

            if (chickenEggs.Count > 0)
                candidates = chickenEggs;
        }

        // Сортуємо за якістю збігу,
        // а не просто за найдешевшою ціною.
        return candidates
            .Select(x => new
            {
                Product = x,
                Score = CalculateProductScore(
                    x,
                    requested)
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Product.Price)
            .Select(x => x.Product)
            .FirstOrDefault();
    }

    private static bool IsChildProduct(string name)
    {
        var text =
            name.ToLowerInvariant();

        string[] forbidden =
        {
            "дитяч",
            "для дітей",
            "для дітей",
            "дитяче харчування",
            "від 0 місяців",
            "від 4 місяців",
            "від 6 місяців",
            "від 9 місяців",
            "від 12 місяців",
            "немовля",
            "немовлят",
            "baby",
            "junior",
            "ростишка"
        };

        return forbidden.Any(
            word => text.Contains(word));
    }

    private static int CalculateProductScore(
        SilpoProduct product,
        string requested)
    {
        var name =
            product.Name.ToLowerInvariant();

        var request =
            requested.ToLowerInvariant();

        int score = 0;

        // Точний збіг — найвищий пріоритет.
        if (name.Equals(request))
            score += 1000;

        // Назва починається із запиту.
        if (name.StartsWith(request))
            score += 200;

        // Містить повний запит.
        if (name.Contains(request))
            score += 100;

        // Збіг окремих слів.
        var words =
            request.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            if (name.Contains(word))
                score += 20;
        }

        // Нормальні продукти мають перевагу.
        if (!IsChildProduct(product.Name))
            score += 100;

        // Для молока краще звичайне коров'яче молоко.
        if (request.Contains("молоко"))
        {
            if (name.Contains("коров"))
                score += 30;

            if (name.Contains("стерилізован"))
                score += 20;

            if (name.Contains("пастеризован"))
                score += 20;
        }

        // Для йогурту уникаємо дитячих брендів.
        if (request.Contains("йогурт"))
        {
            if (name.Contains("ростишка"))
                score -= 500;
        }

        // Для сиру уникаємо дитячих сирків.
        if (request.Contains("сир"))
        {
            if (name.Contains("дит"))
                score -= 500;

            if (name.Contains("кисломолоч"))
                score += 30;
        }

        return score;
    }

    // =========================================================
    // OPENROUTER
    // =========================================================

    private async Task<string?> GenerateAiMessageAsync(
        string userMessage,
        List<SilpoProduct> selectedItems,
        decimal budget,
        string fallbackMessage)
    {
        var apiKey =
            _configuration["OpenRouter:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
            return fallbackMessage;

        try
        {
            var itemsText =
                string.Join(
                    "\n",
                    selectedItems.Select(
                        x =>
                            "- " +
                            x.Name +
                            ": " +
                            x.Price.ToString(
                                "0.00",
                                CultureInfo.InvariantCulture) +
                            " грн"));

            var total =
                selectedItems.Sum(x => x.Price);

            var prompt =
                "Користувач написав: " +
                userMessage +
                "\n\n" +
                "Реально знайдені товари Silpo:\n" +
                itemsText +
                "\n\n" +
                "Бюджет: " +
                budget.ToString(
                    "0.00",
                    CultureInfo.InvariantCulture) +
                " грн\n" +
                "Загальна сума: " +
                total.ToString(
                    "0.00",
                    CultureInfo.InvariantCulture) +
                " грн\n\n" +
                "Напиши коротке повідомлення українською мовою. " +
                "Не вигадуй нових товарів. " +
                "Не змінюй ціни. " +
                "Не використовуй JSON. " +
                "Не використовуй Markdown.";

            var requestBody = new
            {
                model = DefaultModel,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                },
                max_tokens = 120,
                temperature = 0.2
            };

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    OpenRouterUrl);

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    apiKey);

            request.Headers.TryAddWithoutValidation(
                "HTTP-Referer",
                "http://localhost:5068");

            request.Headers.TryAddWithoutValidation(
                "X-Title",
                "Hacaton Silpo Assistant");

            request.Content =
                new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

            using var response =
                await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                return fallbackMessage;

            var responseText =
                await response.Content.ReadAsStringAsync();

            using var document =
                JsonDocument.Parse(responseText);

            if (!document.RootElement.TryGetProperty(
                    "choices",
                    out var choices))
            {
                return fallbackMessage;
            }

            if (choices.GetArrayLength() == 0)
                return fallbackMessage;

            var content =
                choices[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

            if (string.IsNullOrWhiteSpace(content))
                return fallbackMessage;

            if (content.Contains(
                    "User Safety",
                    StringComparison.OrdinalIgnoreCase))
            {
                return fallbackMessage;
            }

            if (content.Contains("```") ||
                content.TrimStart().StartsWith("{"))
            {
                return fallbackMessage;
            }

            return content.Trim();
        }
        catch
        {
            return fallbackMessage;
        }
    }

    // =========================================================
    // ФІНАЛЬНА ВІДПОВІДЬ API
    // =========================================================

    private static string CreateResponse(
        bool success,
        string message,
        decimal budget,
        decimal total,
        List<SilpoProduct> items,
        string? error = null)
    {
        var formattedItems =
            items.Select(x => new
            {
                name = x.Name,
                price = Math.Round(x.Price, 2),
                oldPrice = x.OldPrice,
                available = x.Available,
                stock = x.Stock,
                displayRatio = x.DisplayRatio,
                image = x.Image,
                slug = x.Slug
            }).ToArray();

        if (string.IsNullOrWhiteSpace(error))
        {
            var response =
                new
                {
                    success,
                    message,
                    budget = Math.Round(budget, 2),
                    total = Math.Round(total, 2),
                    items = formattedItems
                };

            return JsonSerializer.Serialize(
                response,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder =
                        System.Text.Encodings.Web.JavaScriptEncoder
                            .UnsafeRelaxedJsonEscaping
                });
        }
        else
        {
            var response =
                new
                {
                    success,
                    message,
                    budget = Math.Round(budget, 2),
                    total = Math.Round(total, 2),
                    items = formattedItems,
                    error
                };

            return JsonSerializer.Serialize(
                response,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder =
                        System.Text.Encodings.Web.JavaScriptEncoder
                            .UnsafeRelaxedJsonEscaping
                });
        }
    }

    // =========================================================
    // МОДЕЛЬ ТОВАРУ
    // =========================================================

    private class SilpoProduct
    {
        public string Name { get; set; } = "";

        public decimal Price { get; set; }

        public bool Available { get; set; }

        public string? Stock { get; set; }

        public string? Image { get; set; }

        public string? OldPrice { get; set; }

        public string? DisplayRatio { get; set; }

        public string? Slug { get; set; }
    }
}
