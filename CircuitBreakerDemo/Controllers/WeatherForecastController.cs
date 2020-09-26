using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CircuitBreakerDemo.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private static Random _Random = new Random();

        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(ILogger<WeatherForecastController> logger)
        {
            this._logger = logger;
        }

        [HttpGet]
        public IEnumerable<WeatherForecast> Get()
        {
            if (_Random.Next(Summaries.Length) % 3 == 0)
            {
                throw new Exception("程序运行错误");
            }

            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = _Random.Next(-20, 55),
                Summary = Summaries[_Random.Next(Summaries.Length)]
            })
            .ToArray();
        }

        [HttpGet("test")]
        public string Test()
        {
            if (_Random.Next(Summaries.Length) % 3 == 0)
            {
                throw new Exception("程序运行错误");
            }
            return Summaries[_Random.Next(Summaries.Length)];
        }
    }
}
