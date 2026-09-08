using Hacaton.Models;
using Hacaton.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hacaton.Controllers;

[ApiController]
[Route("api/assistant")]
public class AssistantController : ControllerBase
{
    private readonly AiAgentService _aiAgentService;
    private readonly SilpoMcpService _silpoMcpService;
    private readonly SilpoTokenStore _tokenStore;
    public AssistantController(
        AiAgentService aiAgentService,
        SilpoMcpService silpoMcpService,
        SilpoTokenStore tokenStore )
    {
        _aiAgentService = aiAgentService;
        _silpoMcpService = silpoMcpService;
        _tokenStore = tokenStore;
    }

    [HttpPost]
    public async Task<IActionResult> Ask([FromBody] UserRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new
            {
                success = false,
                message = "Повідомлення не може бути порожнім."
            });
        }

        var result = await _aiAgentService.AskAsync(
            request.Message,
            request.Address);

        return Content(result, "application/json");
    }
    [HttpPost("address")]
    public async Task<IActionResult> SetAddress(
    [FromBody] SetAddressRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Address))
        {
            return BadRequest(new
            {
                success = false,
                message = "Вкажіть адресу."
            });
        }

        var accessToken =
            _tokenStore.AccessToken;

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Спочатку авторизуйтесь у Silpo."
            });
        }

        try
        {
            var result =
                await _silpoMcpService
                    .InitializeBranchByAddressAsync(
                        accessToken,
                        request.Address);

            return Content(
                result,
                "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message =
                    "Помилка визначення філії Silpo.",
                error = ex.Message
            });
        }
    }
    public class SetAddressRequest
    {
        public string Address { get; set; } = "";
    }
    [HttpGet("tools")]
    public async Task<IActionResult> GetTools()
    {
        var accessToken = _tokenStore.AccessToken;

        if (string.IsNullOrWhiteSpace(accessToken))
            return Unauthorized("Спочатку авторизуйтесь у Silpo.");

        var result = await _silpoMcpService.GetToolsAsync(accessToken);

        return Content(result, "application/json");
    }
    [HttpGet("delivery-types")]
    public async Task<IActionResult> GetDeliveryTypes(
    [FromQuery] double latitude,
    [FromQuery] double longitude)
    {
        var accessToken = _tokenStore.AccessToken;

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Спочатку авторизуйтесь у Silpo."
            });
        }

        var result =
            await _silpoMcpService.GetAvailableDeliveryTypesAsync(
                accessToken,
                latitude,
                longitude);

        return Content(result, "application/json");
    }
    [HttpGet("set-lviv")]
    public async Task<IActionResult> SetLviv()
    {
        var accessToken = _tokenStore.AccessToken;

        Console.WriteLine(
            $"SET-LVIV AccessToken exists: {!string.IsNullOrWhiteSpace(accessToken)}");

        Console.WriteLine(
            $"SET-LVIV BranchId before: {_tokenStore.BranchId}");

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Unauthorized(new
            {
                success = false,
                message = "AccessToken відсутній у SilpoTokenStore."
            });
        }

        var result =
            await _silpoMcpService.SetBranchByCoordinatesAsync(
                accessToken,
                49.8138,
                24.0460);

        Console.WriteLine(
            $"SET-LVIV BranchId after: {_tokenStore.BranchId}");

        return Content(result, "application/json");
    }
    [HttpGet("time-slots")]
    public async Task<IActionResult> GetTimeSlots()
    {
        var accessToken = _tokenStore.AccessToken;

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Спочатку авторизуйтесь у Silpo."
            });
        }

        if (string.IsNullOrWhiteSpace(_tokenStore.BranchId))
        {
            return BadRequest(new
            {
                success = false,
                message = "Спочатку потрібно визначити філію Silpo."
            });
        }

        Console.WriteLine("========== TIME SLOTS ==========");
        Console.WriteLine($"BranchId: {_tokenStore.BranchId}");
        Console.WriteLine("================================");

        var result =
            await _silpoMcpService.GetTimeSlotsAsync(
                accessToken,
                "DeliveryHome");

        return Content(result, "application/json");
    }
    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(
    [FromQuery] string[] products,
    [FromQuery] string? deliveryType = null,
    [FromQuery] string? timeslotStart = null,
    [FromQuery] string? timeslotEnd = null)
    {
        var accessToken = _tokenStore.AccessToken;

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Спочатку авторизуйтесь у Silpo."
            });
        }

        if (string.IsNullOrWhiteSpace(_tokenStore.BranchId))
        {
            return BadRequest(new
            {
                success = false,
                message = "Спочатку потрібно визначити філію Silpo."
            });
        }

        if (products == null || products.Length == 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "Вкажіть хоча б один товар."
            });
        }

        // Значення за замовчуванням
        deliveryType ??= "DeliveryHome";

        // Якщо час не передали — беремо найближчий часовий інтервал.
        // Для першого тесту можна передати його вручну.
        if (string.IsNullOrWhiteSpace(timeslotStart) ||
            string.IsNullOrWhiteSpace(timeslotEnd))
        {
            return BadRequest(new
            {
                success = false,
                message = "Потрібно передати timeslotStart і timeslotEnd.",
                branchId = _tokenStore.BranchId
            });
        }

        Console.WriteLine();
        Console.WriteLine("========== GET PRODUCTS ==========");
        Console.WriteLine($"BranchId:      {_tokenStore.BranchId}");
        Console.WriteLine($"DeliveryType:  {deliveryType}");
        Console.WriteLine($"TimeslotStart: {timeslotStart}");
        Console.WriteLine($"TimeslotEnd:   {timeslotEnd}");
        Console.WriteLine($"Products:      {string.Join(", ", products)}");
        Console.WriteLine("==================================");
        Console.WriteLine();

        try
        {
            var result = await _silpoMcpService.FindProductsAsync(
                accessToken,
                deliveryType,
                timeslotStart,
                timeslotEnd,
                products);

            return Content(result, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Помилка отримання товарів Silpo.",
                error = ex.Message,
                branchId = _tokenStore.BranchId
            });
        }
    }
}