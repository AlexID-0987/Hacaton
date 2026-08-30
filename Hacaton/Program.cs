using Hacaton.Services;

namespace Hacaton;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        
        builder.Services.AddOpenApi();

        // Silpo OAuth
        builder.Services.AddHttpClient<SilpoOAuthService>();

        // Silpo MCP
        builder.Services.AddHttpClient<SilpoMcpService>();

        // OAuth token
        builder.Services.AddSingleton<SilpoTokenStore>();

        var app = builder.Build();
        
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.UseRouting();

        app.MapControllers();

        app.MapGet("/", () =>
            Results.Redirect("/index.html"));

        app.Run();
    }
}