using Microsoft.AspNetCore.Mvc;

namespace AkTxSignatureDemo.Server.Controllers;

/// <summary>
/// Sample controller included from the default ASP.NET Core template.
/// Returns randomly generated weather forecast data and serves as a connectivity smoke test.
/// </summary>
[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    /// <summary>
    /// Returns five randomly generated weather forecast entries, each one day apart starting from tomorrow.
    /// </summary>
    /// <returns>An array of <see cref="WeatherForecast"/> objects.</returns>
    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get()
    {
        return Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        })
        .ToArray();
    }
}
