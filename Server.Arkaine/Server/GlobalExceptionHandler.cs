using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Threading.Channels;

namespace Server.Arkaine
{
    public class GlobalExceptionHandler : IMiddleware
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

                var problem = new ProblemDetails
                {
                    Type = ex.GetType().Name,
                    Detail = ex.Message,
                    Status = (int)HttpStatusCode.InternalServerError,
                    Title = "An error occured while processing your request",
                };

                await context.Response.WriteAsJsonAsync(problem);
            }
        }
    }
}