using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Polly.CircuitBreaker;
using Polly;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Configuration;

namespace CircuitBreakerDemo
{
    /// <summary>
    /// net core 下面自定义熔断器中间件
    /// </summary>
    public class CircuitBreakerMiddleware : IDisposable
    {
        private readonly RequestDelegate _next;
        private readonly ConcurrentDictionary<string, AsyncPolicy> _asyncPolicyDict = null;
        private readonly ILogger<CircuitBreakerMiddleware> _logger;
        private readonly IConfiguration _configuration;

        public CircuitBreakerMiddleware(RequestDelegate next, ILogger<CircuitBreakerMiddleware> logger, IConfiguration configuration)
        {
            this._next = next;
            this._logger = logger;
            this._configuration = configuration;//未来url的熔断规则可以从config文件里读取，增加灵活性
            logger.LogInformation($"{nameof(CircuitBreakerMiddleware)}.ctor()");
            this._asyncPolicyDict = new ConcurrentDictionary<string, AsyncPolicy>(Environment.ProcessorCount, 31);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var request = context.Request;
            string httpMethod = request.Method;
            string pathAndQuery = request.GetEncodedPathAndQuery();
            var asyncPolicy = this._asyncPolicyDict.GetOrAdd(string.Concat(httpMethod, pathAndQuery), key =>
            {
                return Policy.Handle<Exception>().CircuitBreakerAsync(3, TimeSpan.FromSeconds(10));
            });
            try
            {
                await asyncPolicy.ExecuteAsync(async () => await this._next(context));
            }
            catch (BrokenCircuitException ex)
            {
                this._logger.LogError($"{nameof(BrokenCircuitException)}.InnerException.Message：{ex.InnerException.Message}");
                var response = context.Response;
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.ContentType = "text/plain; charset=utf-8";
                await response.WriteAsync("Circuit Broken o(╥﹏╥)o");
            }

            //var endpoint = context.GetEndpoint();
            //if (endpoint != null)
            //{
            //    var controllerActionDescriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
            //    var controllerName = controllerActionDescriptor.ControllerName;
            //    var actionName = controllerActionDescriptor.ActionName;
            //    if (string.Equals(controllerName, "WeatherForecast", StringComparison.OrdinalIgnoreCase)
            //        && string.Equals(actionName, "Test", StringComparison.OrdinalIgnoreCase))
            //    {//针对某一个控制器的某一个action，单独设置熔断
            //        await Policy.Handle<Exception>().CircuitBreakerAsync(3, TimeSpan.FromSeconds(10)).ExecuteAsync(async () => await this._next(context));
            //    }
            //    else
            //    {
            //        await this._next(context);
            //    }
            //}
        }

        public void Dispose()
        {
            this._asyncPolicyDict.Clear();
            this._logger.LogInformation($"{nameof(CircuitBreakerMiddleware)}.Dispose()");
        }
    }
}
