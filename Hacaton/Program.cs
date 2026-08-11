
using Hacaton.Data;
using Hacaton.Services;
using Microsoft.EntityFrameworkCore;

namespace Hacaton;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddOpenApi();
        builder.Services.AddScoped<RuleBasedProductRecommendationService>();
        //builder.Services.AddHttpClient<OpenAIProductRecommendationService>();
        //builder.Services.AddScoped<IProductRecommendationService>(sp => sp.GetRequiredService<OpenAIProductRecommendationService>());
        builder.Services.AddScoped<IProductRecommendationService, RuleBasedProductRecommendationService>();

        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase("ProductAssistantDb"));

        var app = builder.Build();
        
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            SeedData.Initialize(db);
        }

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseRouting();
        app.MapControllers();

        app.MapGet("/", () => Results.Redirect("/index.html"));

        app.Run();
    }
}
