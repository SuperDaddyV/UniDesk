namespace UniDesk.Models;

public class WeatherInfo
{
    public string City { get; set; } = string.Empty;
    public string Temperature { get; set; } = string.Empty;     // 当前温度，如 "25°C"
    public string WeatherDesc { get; set; } = string.Empty;     // 天气描述，如 "晴"
    public string AirQuality { get; set; } = string.Empty;      // 空气质量指数
    public string[] AirQualityAttributions { get; set; } = [];  // 空气质量接口返回的来源声明
    public string Humidity { get; set; } = string.Empty;        // 湿度，如 "60%"
    public string MaxTemp { get; set; } = string.Empty;         // 最高温
    public string MinTemp { get; set; } = string.Empty;         // 最低温
    public string IconCode { get; set; } = string.Empty;        // 和风天气图标代码，如 "104"
    public string IconUri { get; set; } = string.Empty;         // 兼容旧缓存（远程 URL，不再用于显示）
    public DateTime FetchTime { get; set; }
    public bool IsExpired { get; set; }
}
