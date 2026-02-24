
using TXTextControl.Web;
using TXTextControl.Web.MVC.DocumentViewer;

namespace AkTxSignatureDemo.Server;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        // Add services to the container.

        builder.Services.AddControllers();
        builder.Services.AddMemoryCache();
        builder.Services.AddHostedService<TXWebServerProcess>();

        builder.Services.AddCors(p => p.AddPolicy("corsapp", builder =>
        {
            builder.WithOrigins("*").AllowAnyMethod().AllowAnyHeader();
        }));

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        var app = builder.Build();

        app.MapDefaultEndpoints();

        app.UseDefaultFiles();
        app.MapStaticAssets();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();


        app.MapControllers();

        // *** Required for Document Editor ***
        // add the WebSocket middleware
        app.UseWebSockets();
        app.UseTXWebSocketMiddleware("127.0.0.1", 5664);

        // *** Required for Document Viewer ***
        // add the DocumentViewer middleware
        app.UseCors("corsapp");
        app.UseRouting();
        app.UseTXDocumentViewer();

        app.MapFallbackToFile("/index.html");

        app.Run();
    }
}
