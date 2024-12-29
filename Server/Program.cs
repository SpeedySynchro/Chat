using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Server;

/// <summary>
/// The runner for the web services
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        // Add controller services
        builder.Services.AddControllers();
        var app = builder.Build();
        var server = new ChatServer();
        server.Configure(app);
        
        // Middleware for routing
        app.UseRouting();

        // Register the controllers
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers(); // Automatically recognizes all controllers
        });

        // Start the app
        app.Run();
    }
}