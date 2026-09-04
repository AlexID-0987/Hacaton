using Hacaton.Models;
using Hacaton.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hacaton.Controllers;

[ApiController]
[Route("api/assistant")]
public class AssistantController : ControllerBase
{
    private readonly AiAgentService _aiAgentService;

    public AssistantController(
        AiAgentService aiAgentService)
    {
        _aiAgentService = aiAgentService;
    }

    [HttpPost]
    public async Task<IActionResult> Ask(
        [FromBody] UserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new
            {
                success = false,
                message = "Введіть запит."
            });
        }

        try
        {
            var answer =
                await _aiAgentService.AskAsync(
                    request.Message);

            return Ok(new
            {
                success = true,
                message = answer
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Помилка AI Assistant.",
                error = ex.Message
            });
        }
    }
}