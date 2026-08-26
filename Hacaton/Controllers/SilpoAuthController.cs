using Hacaton.Services;
using Microsoft.AspNetCore.Mvc;
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

    public SilpoAuthController(IConfiguration configuration, SilpoTokenStore tokenStore)
    {
        _configuration = configuration;
        _tokenStore = tokenStore;
    }

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

        var savedState = Request.Cookies["silpo_oauth_state"];

        if (string.IsNullOrEmpty(savedState) ||
            savedState != state)
        {
            return BadRequest("Невірний OAuth state.");
        }

        var codeVerifier = Request.Cookies["silpo_code_verifier"];

        if (string.IsNullOrEmpty(codeVerifier))
        {
            return BadRequest("PKCE code_verifier відсутній.");
        }

        var clientId = _configuration["Silpo:ClientId"];
        var clientSecret = _configuration["Silpo:ClientSecret"];

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
                    ["redirect_uri"] =
                "http://localhost:5068/api/silpo/callback",
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

        using var json = JsonDocument.Parse(content);

        var accessToken = json.RootElement
            .GetProperty("access_token")
            .GetString();
             

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return BadRequest("Access token не отримано.");
        }

        _tokenStore.AccessToken = accessToken;

        return Ok(new
        {
            message = "Авторизація успішна. Access token отримано."
        });
    }
    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(
    [FromServices] SilpoMcpService silpoMcpService,
    [FromQuery] string[] products)
    {
        var token = _tokenStore.AccessToken;

        if (string.IsNullOrEmpty(token))
        {
            return Unauthorized(
                "Спочатку авторизуйтесь через /api/silpo/login");
        }

        if (products.Length == 0)
        {
            return BadRequest("Вкажіть хоча б один товар.");
        }

        
        var branchId = "1edb6b38-214b-66d6-a8e0-7f2fdd178564";
        var deliveryType = "DeliveryHome";

        var timeslotStart = "2026-08-26T06:00:00+00:00";
        var timeslotEnd = "2026-08-26T07:30:00+00:00";

        var result = await silpoMcpService.FindProductsAsync(
            token,
            branchId,
            deliveryType,
            timeslotStart,
            timeslotEnd,
            products);

        return Content(result, "application/json");
    }
    [HttpGet("delivery")]
    public async Task<IActionResult> GetDelivery(
    [FromQuery] string address,
    [FromServices] SilpoMcpService silpoMcpService,
    [FromServices] SilpoTokenStore tokenStore)
    {
        if (string.IsNullOrWhiteSpace(tokenStore.AccessToken))
        {
            return Unauthorized(
                "Спочатку авторизуйтесь через /api/silpo/login");
        }

       
        var addressResult = await silpoMcpService.FindAddressAsync(
            tokenStore.AccessToken,
            address);

        var jsonStart = addressResult.IndexOf('{');

        if (jsonStart < 0)
        {
            return BadRequest(addressResult);
        }

        var addressJson = addressResult[jsonStart..];

        using var addressDocument =
            JsonDocument.Parse(addressJson);

        var root = addressDocument.RootElement;

        if (!root.TryGetProperty("result", out var result))
        {
            return BadRequest(addressResult);
        }

        
        var content = result.GetProperty("content");

        if (content.GetArrayLength() == 0)
        {
            return BadRequest("Адресу не знайдено.");
        }

        var text = content[0].GetProperty("text").GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            return BadRequest("Порожня відповідь від Silpo MCP.");
        }

        using var addressData =
            JsonDocument.Parse(text);

        var addressRoot = addressData.RootElement;

        if (!addressRoot.TryGetProperty("addresses", out var addresses) ||
            addresses.GetArrayLength() == 0)
        {
            return NotFound("Адресу не знайдено.");
        }

        
        var selectedAddress = addresses[0];

        var latitude =
            selectedAddress.GetProperty("latitude").GetDouble();

        var longitude =
            selectedAddress.GetProperty("longitude").GetDouble();

        
        var deliveryResult =
            await silpoMcpService.GetAvailableDeliveryTypesAsync(
                tokenStore.AccessToken,
                latitude,
                longitude);

        return Content(deliveryResult, "application/json");
    }
    [HttpGet("time-slots")]
    public async Task<IActionResult> GetTimeSlots(
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

        if (string.IsNullOrWhiteSpace(branchId))
        {
            return BadRequest("branchId є обов'язковим.");
        }

        if (string.IsNullOrWhiteSpace(deliveryType))
        {
            return BadRequest("deliveryType є обов'язковим.");
        }

        var result = await silpoMcpService.GetTimeSlotsAsync(
            tokenStore.AccessToken,
            branchId,
            deliveryType);

        return Content(result, "application/json");
    }
}