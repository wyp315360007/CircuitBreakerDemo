using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace CircuitBreakerDemo
{
    /// <summary>
    /// net core 实现自定义断路器中间件
    /// </summary>
    public class CircuitBreakerMiddleware : IDisposable
    {
        private readonly RequestDelegate _next;
        private readonly ConcurrentDictionary<string, AsyncPolicy> _asyncPolicyDict;
        private readonly ILogger<CircuitBreakerMiddleware> _logger;
        private readonly IConfiguration _configuration;

        public CircuitBreakerMiddleware(RequestDelegate next, ILogger<CircuitBreakerMiddleware> logger, IConfiguration configuration)
        {
            this._next = next;
            this._logger = logger;
            this._configuration = configuration; //未来url的断路规则可以从config文件里读取，增加灵活性
            logger.LogInformation($"{nameof(CircuitBreakerMiddleware)}.Ctor()");
            this._asyncPolicyDict = new ConcurrentDictionary<string, AsyncPolicy>(Environment.ProcessorCount, 31);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var request = context.Request;
            try
            {
                await this._asyncPolicyDict.GetOrAdd(string.Concat(request.Method, request.GetEncodedPathAndQuery())
                                                     , key => Policy.Handle<Exception>()
                                                                    .AdvancedCircuitBreakerAsync
                                                                    (
                                                                        //备注：20秒内，请求次数达到10次以上，失败率达到20%后开启断路器，断路器一旦被打开至少要保持5秒钟的打开状态。
                                                                        failureThreshold: 0.2D,                       //失败率达到20%熔断开启
                                                                        minimumThroughput: 10,                        //最多调用10次
                                                                        samplingDuration: TimeSpan.FromSeconds(20),   //评估故障持续时长20秒
                                                                        durationOfBreak: TimeSpan.FromSeconds(5),     //恢复正常使用前电路保持打开状态的最少时长5秒
                                                                        onBreak: (exception, breakDelay, context) =>  //断路器打开时触发事件，程序不能使用
                                                                        {
                                                                            var ex = exception.InnerException ?? exception;
                                                                            this._logger.LogError($"{key} => 进入打开状态，中断持续时长：{breakDelay}，错误类型：{ex.GetType().Name}，信息：{ex.Message}");
                                                                        },
                                                                        onReset: context =>                           //断路器关闭状态触发事件，断路器关闭
                                                                        {
                                                                            this._logger.LogInformation($"{key} => 进入关闭状态，程序恢复正常使用");
                                                                        },
                                                                        onHalfOpen: () =>                             //断路器进入半打开状态触发事件，断路器准备再次尝试操作执行
                                                                        {
                                                                            this._logger.LogInformation($"{key} => 进入半开状态，重新尝试接收请求");
                                                                        }
                                                                    )
                                                     )
                                            .ExecuteAsync(async () => await this._next(context))
                                            ;
            }
            catch (BrokenCircuitException exception)
            {
                this._logger.LogError($"{nameof(BrokenCircuitException)}.InnerException.Message：{exception.InnerException.Message}");
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
            //    {//针对某一个控制器的某一个action，单独设置断路
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
