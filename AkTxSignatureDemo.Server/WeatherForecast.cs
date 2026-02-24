namespace AkTxSignatureDemo.Server;

/// <summary>
/// Represents a single weather forecast entry returned by the sample <c>WeatherForecast</c> API.
/// This model is included as part of the default ASP.NET Core project template and serves
/// as a quick smoke-test endpoint.
/// </summary>
public class WeatherForecast
{
    /// <summary>Gets or sets the date of the forecast.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets the forecast temperature in degrees Celsius.</summary>
    public int TemperatureC { get; set; }

    /// <summary>Gets the forecast temperature converted to degrees Fahrenheit.</summary>
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

    /// <summary>Gets or sets a short text summary of the weather conditions (e.g., "Sunny", "Rainy").</summary>
    public string? Summary { get; set; }
}
