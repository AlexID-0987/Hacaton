using System.Text.RegularExpressions;
using Hacaton.Models;

namespace Hacaton.Services;

public class RuleBasedProductRecommendationService : IProductRecommendationService
{
    public Task<AssistantResponse> Generate(IEnumerable<Product> products, string message)
    {
        var text = message.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(new AssistantResponse
            {
                Message = "Напишіть, що саме ви хочете купити або який бюджет маєте.",
                Budget = 0,
                TotalPrice = 0
            });
        }

        var budget = ExtractBudget(text);
        var mealType = ExtractMealType(text);
        var productCategory = ExtractProductCategory(text);
        var allProducts = products
            .Where(p => p.InStock)
            .ToList();

        if (!allProducts.Any())
        {
            return Task.FromResult(new AssistantResponse
            {
                Message = "У базі немає доступних товарів.",
                Budget = budget,
                TotalPrice = 0
            });
        }

        var preferred = SelectPreferredProducts(allProducts, mealType, productCategory)
            .DistinctBy(p => p.Name)
            .ToList();

        var filtered = preferred.Any() ? preferred : allProducts.OrderBy(p => p.Price).ToList();
        var recommended = new List<RecommendationItem>();
        var remainingBudget = budget > 0 ? budget : 300m;

        foreach (var product in filtered)
        {
            if (remainingBudget <= 0)
            {
                break;
            }

            var maxQuantity = budget > 0 ? Math.Max(1, (int)Math.Floor(remainingBudget / product.Price)) : 2;
            var quantity = Math.Min(maxQuantity, 3);
            if (quantity <= 0)
            {
                continue;
            }

            var total = product.Price * quantity;
            if (budget > 0 && total > remainingBudget)
            {
                quantity = Math.Max(1, (int)Math.Floor(remainingBudget / product.Price));
                total = product.Price * quantity;
            }

            if (quantity <= 0 || total <= 0)
            {
                continue;
            }

            recommended.Add(new RecommendationItem
            {
                Name = product.Name,
                Quantity = quantity,
                UnitPrice = product.Price,
                Total = total,
                ImageUrl = product.ImageUrl
            });

            remainingBudget -= total;

            if (recommended.Count >= 6)
            {
                break;
            }
        }

        if (recommended.Count == 0)
        {
            var fallback = filtered.Take(4).Select(p => new RecommendationItem
            {
                Name = p.Name,
                Quantity = 1,
                UnitPrice = p.Price,
                Total = p.Price,
                ImageUrl = p.ImageUrl
            }).ToList();

            return Task.FromResult(new AssistantResponse
            {
                Message = budget > 0
                    ? $"Я підібрав базові товари в межах бюджету {budget} грн."
                    : "Ось кілька корисних варіантів товарів.",
                Budget = budget,
                TotalPrice = fallback.Sum(i => i.Total),
                Items = fallback
            });
        }

        var totalPrice = recommended.Sum(i => i.Total);
        var baseMessage = mealType is not null
            ? $"Я підібрав товари для {mealType} на суму до {budget} грн."
            : budget > 0
                ? $"Я підібрав товари на суму до {budget} грн."
                : "Ось рекомендація товарів для вашого запиту.";

        return Task.FromResult(new AssistantResponse
        {
            Message = baseMessage,
            Budget = budget,
            TotalPrice = totalPrice,
            Items = recommended
        });
    }

    private static List<Product> SelectPreferredProducts(IEnumerable<Product> products, string? mealType, string? productCategory)
    {
        var all = products.ToList();

        if (productCategory is not null)
        {
            var filtered = all.Where(p => p.Category.Contains(productCategory, StringComparison.OrdinalIgnoreCase)).ToList();
            if (filtered.Count > 0)
            {
                return filtered.OrderByDescending(p => p.Price).ThenBy(p => p.Name).ToList();
            }
        }

        var preferredNames = mealType switch
        {
            "сніданок" => new[] { "Яйця", "Молоко", "Хліб", "Йогурт", "Яблука", "Банани", "Сир", "Огірки" },
            "обід" => new[] { "Куряче філе", "Огірки", "Помідори", "Картопля", "Яблука", "Сметана", "Яловичина" },
            "вечеря" => new[] { "Лосось", "Куряче філе", "Картопля", "Капуста", "Яблука", "Груша", "Молоко" },
            "пікнік" => new[] { "Хліб", "Банани", "Яблука", "Вода", "Йогурт", "Сир", "Огірки", "Яйця" },
            _ => new[] { "Яйця", "Яблука", "Хліб", "Картопля", "Молоко", "Банани", "Куряче філе", "Огірки" }
        };

        var orderByName = preferredNames
            .Select((name, index) => new { name, index })
            .ToDictionary(x => x.name, x => x.index);

        var ordered = all
            .OrderBy(p => orderByName.TryGetValue(p.Name, out var idx) ? idx : int.MaxValue)
            .ThenBy(p => p.Price)
            .ToList();

        return ordered;
    }

    private static decimal ExtractBudget(string text)
    {
        var match = Regex.Match(text, @"(\d+(?:[.,]\d+)?)\s*(грн|uah|₴)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return decimal.Parse(match.Groups[1].Value.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);
        }

        var fallback = Regex.Match(text, @"до\s*(\d+(?:[.,]\d+)?)");
        if (fallback.Success)
        {
            return decimal.Parse(fallback.Groups[1].Value.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);
        }

        return 0m;
    }

    private static string? ExtractMealType(string text)
    {
        var mealTypes = new[]
        {
            "сніданок", "обід", "вечеря", "пікнік", "пикнік", "list", "список"
        };

        foreach (var meal in mealTypes)
        {
            if (text.Contains(meal, StringComparison.OrdinalIgnoreCase))
            {
                return meal switch
                {
                    "сніданок" => "сніданок",
                    "обід" => "обід",
                    "вечеря" => "вечеря",
                    "пікнік" or "пикнік" => "пікнік",
                    "list" or "список" => "список",
                    _ => meal
                };
            }
        }

        return null;
    }

    private static string? ExtractProductCategory(string text)
    {
        var categories = new[]
        {
            "борщ", "овочі", "фрукти", "молочні", "напої",
            "хліб", "м'ясо", "мясо", "риба"
        };

        foreach (var category in categories)
        {
            if (text.Contains(category, StringComparison.OrdinalIgnoreCase))
            {
                return category;
            }
        }

        return null;
    }
}
