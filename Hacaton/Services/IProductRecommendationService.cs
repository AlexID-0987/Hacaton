using Hacaton.Models;

namespace Hacaton.Services;

public interface IProductRecommendationService
{
    Task<AssistantResponse> Generate(IEnumerable<Product> products, string message);
}
