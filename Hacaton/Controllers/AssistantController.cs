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
   

}
