using Hacaton.Data;
using Hacaton.Models;
using Hacaton.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Hacaton.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssistantController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IProductRecommendationService _recommendationService;

    public AssistantController(ApplicationDbContext context, IProductRecommendationService recommendationService)
    {
        _context = context;
        _recommendationService = recommendationService;
    }

    [HttpGet("products")]
    public async Task<ActionResult<IEnumerable<ProductSummaryDto>>> GetProducts()
    {
        var products = await _context.Products
            .Where(p => p.InStock)
            .Select(p => new ProductSummaryDto
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                Category = p.Category,
                ImageUrl = p.ImageUrl
            })
            .ToListAsync();

        return Ok(products);
    }

    [HttpPost]
    public async Task<ActionResult<AssistantResponse>> Ask([FromBody] UserRequest request)
    {
        var message = request.Message ?? string.Empty;

        if (string.IsNullOrWhiteSpace(message))
        {
            return BadRequest(new { message = "Повідомлення не може бути порожнім." });
        }

        var products = await _context.Products
            .Where(p => p.InStock)
            .ToListAsync();

        var response = await _recommendationService.Generate(products, message);
        return Ok(response);
    }
    
    
    [HttpGet("silpo-register")]
    public async Task<IActionResult> RegisterSilpoClient(
    [FromServices] SilpoOAuthService oauthService)
    {
        var result = await oauthService.RegisterClientAsync();

        return Content(result);
    }
    
    [HttpGet("silpo-test")]
    public async Task<IActionResult> TestSilpo(
    [FromServices] SilpoMcpService silpoMcpService,
    [FromServices] SilpoTokenStore tokenStore)
    {
        if (string.IsNullOrWhiteSpace(tokenStore.AccessToken))
        {
            return Unauthorized(
                "Спочатку авторизуйтесь через /api/silpo/login");
        }

        var result = await silpoMcpService.TestAsync(
            tokenStore.AccessToken);

        return Content(result);
    }
    [HttpGet("silpo-tools")]
    public async Task<IActionResult> GetSilpoTools(
    [FromServices] SilpoMcpService silpoMcpService,
    [FromServices] SilpoTokenStore tokenStore)
    {
        if (string.IsNullOrWhiteSpace(tokenStore.AccessToken))
        {
            return Unauthorized(
                "Спочатку авторизуйтесь через /api/silpo/login");
        }

        var result = await silpoMcpService.GetToolsAsync(
            tokenStore.AccessToken);

        return Content(result);
    }
    [HttpGet("silpo-address")]
    public async Task<IActionResult> FindSilpoAddress(
    [FromQuery] string address,
    [FromServices] SilpoMcpService silpoMcpService,
    [FromServices] SilpoTokenStore tokenStore)
    {
        if (string.IsNullOrWhiteSpace(tokenStore.AccessToken))
        {
            return Unauthorized(
                "Спочатку авторизуйтесь через /api/silpo/login");
        }

        var result = await silpoMcpService.FindAddressAsync(
            tokenStore.AccessToken,
            address);

        return Content(result);
    }
    [HttpGet("silpo-delivery-types")]
    public async Task<IActionResult> GetDeliveryTypes(
    [FromServices] SilpoMcpService silpoMcpService,
    [FromServices] SilpoTokenStore tokenStore)
    {
        if (string.IsNullOrWhiteSpace(tokenStore.AccessToken))
        {
            return Unauthorized(
                "Спочатку авторизуйтесь через /api/silpo/login");
        }

        var result = await silpoMcpService.GetDeliveryTypesAsync(
            tokenStore.AccessToken);

        return Content(result, "application/json");
    }
    [HttpGet("silpo-delivery")]
    public async Task<IActionResult> GetSilpoDelivery(
    [FromQuery] double latitude,
    [FromQuery] double longitude,
    [FromServices] SilpoMcpService silpoMcpService,
    [FromServices] SilpoTokenStore tokenStore)
    {
        if (string.IsNullOrWhiteSpace(tokenStore.AccessToken))
        {
            return Unauthorized(
                "Спочатку авторизуйтесь через /api/silpo/login");
        }

        var result = await silpoMcpService
            .GetAvailableDeliveryTypesAsync(
                tokenStore.AccessToken,
                latitude,
                longitude);

        return Content(result, "application/json");
    }
    [HttpGet("silpo-slots")]
    public async Task<IActionResult> GetSilpoSlots(
    [FromQuery] string branchId,
    [FromQuery] string deliveryType,
    [FromServices] SilpoMcpService silpoMcpService,
    [FromServices] SilpoTokenStore tokenStore)
    {
        if (string.IsNullOrWhiteSpace(tokenStore.AccessToken))
        {
            return Unauthorized(
                "Спочатку авторизуйтесь через /api/silpo/login");
        }

        var result = await silpoMcpService.GetTimeSlotsAsync(
            tokenStore.AccessToken,
            branchId,
            deliveryType);

        return Content(result, "application/json");
    }
    [HttpGet("silpo-products")]
    public async Task<IActionResult> GetSilpoProducts(
    [FromServices] SilpoMcpService silpoMcpService,
    [FromServices] SilpoTokenStore tokenStore)
    {
        if (string.IsNullOrWhiteSpace(tokenStore.AccessToken))
        {
            return Unauthorized(
                "Спочатку авторизуйтесь через /api/silpo/login");
        }

        var result = await silpoMcpService.FindProductsAsync(
            tokenStore.AccessToken,
            "1edb6b38-214b-66d6-a8e0-7f2fdd178564",
            "DeliveryHome",
            "2026-08-26T06:00:00+00:00",
            "2026-08-26T07:30:00+00:00",
            new[] { "Молоко", "Хліб", "Яйця" });

        return Content(result, "application/json");
    }
    [ApiController]
    [Route("api/silpo")]
    public class SilpoController : ControllerBase
    {
        [HttpGet("products")]
        public async Task<IActionResult> GetProducts(
            [FromQuery] string products,
            [FromServices] SilpoMcpService silpoMcpService,
            [FromServices] SilpoTokenStore tokenStore)
        {
            if (string.IsNullOrWhiteSpace(tokenStore.AccessToken))
            {
                return Unauthorized(
                    "Спочатку авторизуйтесь через /api/silpo/login");
            }

            var result = await silpoMcpService.FindProductsAsync(
                tokenStore.AccessToken,
                "1edb6b38-214b-66d6-a8e0-7f2fdd178564",
                "DeliveryHome",
                "2026-08-26T06:00:00+00:00",
                "2026-08-26T07:30:00+00:00",
                new[] { products });

            return Content(result, "application/json");
        }
    }
}
