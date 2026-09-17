
using Hacaton.Controllers;
using Hacaton.Models;
using Hacaton.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace Hacaton.Tests.Controllers;

[TestFixture]
public class AssistantControllerTests
{
    private AssistantController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        // HttpClient
        var httpClient = new HttpClient();

        // Token store
        var tokenStore = new SilpoTokenStore();

        // Silpo MCP service
        var silpoMcpService = new SilpoMcpService(
            httpClient,
            tokenStore);

        // Configuration for AiAgentService
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenRouter:ApiKey"] = "test-key",
                ["OpenRouter:Model"] = "test-model"
            })
            .Build();

        // AI Agent service
        var aiAgentService = new AiAgentService(
            httpClient,
            configuration,
            silpoMcpService,
            tokenStore);

        // Controller
        _controller = new AssistantController(
            aiAgentService,
            silpoMcpService,
            tokenStore);
    }


    [Test]
    public async Task Ask_WhenRequestIsNull_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.Ask(null!);

        // Assert
        Assert.That(
            result,
            Is.TypeOf<BadRequestObjectResult>());
    }


    [Test]
    public async Task Ask_WhenMessageIsEmpty_ReturnsBadRequest()
    {
        // Arrange
        var request = new UserRequest
        {
            Message = "",
            Address = "Київ"
        };

        // Act
        var result = await _controller.Ask(request);

        // Assert
        Assert.That(
            result,
            Is.TypeOf<BadRequestObjectResult>());
    }


    [Test]
    public async Task Ask_WhenMessageIsWhitespace_ReturnsBadRequest()
    {
        // Arrange
        var request = new UserRequest
        {
            Message = "   ",
            Address = "Київ"
        };

        // Act
        var result = await _controller.Ask(request);

        // Assert
        Assert.That(
            result,
            Is.TypeOf<BadRequestObjectResult>());
    }
}

