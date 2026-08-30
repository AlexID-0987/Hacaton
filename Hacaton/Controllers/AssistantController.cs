using Hacaton.Models;
using Hacaton.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace Hacaton.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssistantController : ControllerBase
{
    private readonly SilpoMcpService _silpoMcpService;
    private readonly SilpoTokenStore _tokenStore;

    public AssistantController(
        SilpoMcpService silpoMcpService,
        SilpoTokenStore tokenStore)
    {
        _silpoMcpService = silpoMcpService;
        _tokenStore = tokenStore;
    }

    // =========================================================
    // AI ASSISTANT
    // =========================================================

    [HttpPost]
    public async Task<IActionResult> Ask(
        [FromBody] UserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new
            {
                message = "Повідомлення не може бути порожнім."
            });
        }

        if (string.IsNullOrWhiteSpace(_tokenStore.AccessToken))
        {
            return Unauthorized(new
            {
                message =
                    "Спочатку авторизуйтесь через /api/silpo/login"
            });
        }

        try
        {
            // Витягуємо товари із запиту користувача
            var products = ExtractProducts(request.Message);

            if (products.Length == 0)
            {
                return Ok(new
                {
                    message =
                        "Напишіть, які товари ви хочете придбати. " +
                        "Наприклад: молоко, хліб та яйця до 350 грн.",
                    items = Array.Empty<object>(),
                    budget = ExtractBudget(request.Message),
                    totalPrice = 0
                });
            }

            // Поки використовуємо твою актуальну філію
            var branchId =
                "1edb6b38-214b-66d6-a8e0-7f2fdd178564";

            var deliveryType =
                "DeliveryHome";

            // Тимчасовий слот.
            // Пізніше підставимо вибраний користувачем слот.
            var timeslotStart =
                "2026-08-26T06:00:00+00:00";

            var timeslotEnd =
                "2026-08-26T07:30:00+00:00";

            var result =
                await _silpoMcpService.FindProductsAsync(
                    _tokenStore.AccessToken,
                    branchId,
                    deliveryType,
                    timeslotStart,
                    timeslotEnd,
                    products);

            return Content(
                result,
                "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new
                {
                    message = "Помилка отримання товарів Silpo.",
                    error = ex.Message
                });
        }
    }


    // =========================================================
    // ВИДАЛЕНО РОБОТУ З БАЗОЮ
    // =========================================================
    //
    // Цей endpoint залишаємо тільки для сумісності
    // зі старим index.js.
    //
    // Тепер він НЕ бере товари з InMemory.
    //
    // Пізніше можемо повністю прибрати його з JS.
    // =========================================================

    [HttpGet("products")]
    public IActionResult GetProducts()
    {
        return Ok(new
        {
            success = true,
            message =
                "Товари більше не завантажуються з локальної бази. " +
                "Використовується Silpo MCP."
        });
    }


    // =========================================================
    // ДОПОМІЖНІ МЕТОДИ
    // =========================================================

    private static decimal ExtractBudget(string text)
    {
        var match = Regex.Match(
            text,
            @"(\d+(?:[.,]\d+)?)\s*(грн|uah|₴)",
            RegexOptions.IgnoreCase);

        if (match.Success)
        {
            return decimal.Parse(
                match.Groups[1].Value.Replace(',', '.'),
                System.Globalization.CultureInfo.InvariantCulture);
        }

        var fallback = Regex.Match(
            text,
            @"до\s*(\d+(?:[.,]\d+)?)",
            RegexOptions.IgnoreCase);

        if (fallback.Success)
        {
            return decimal.Parse(
                fallback.Groups[1].Value.Replace(',', '.'),
                System.Globalization.CultureInfo.InvariantCulture);
        }

        return 0;
    }


    private static string[] ExtractProducts(string text)
    {
        var knownProducts = new[]
        {
            "молоко",
            "хліб",
            "яйця",
            "яйце",
            "сир",
            "йогурт",
            "сметана",

            "яблука",
            "яблуко",
            "банани",
            "банан",
            "груша",
            "груші",

            "огірки",
            "огірок",
            "помідори",
            "помідор",
            "картопля",
            "капуста",
            "буряк",
            "морква",

            "куряче філе",
            "курятина",
            "курка",
            "свинина",
            "яловичина",

            "лосось",
            "риба",

            "вода",
            "сік"
        };

        var result = new List<string>();

        foreach (var product in knownProducts)
        {
            if (text.Contains(
                    product,
                    StringComparison.OrdinalIgnoreCase))
            {
                result.Add(product);
            }
        }

        return result
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}