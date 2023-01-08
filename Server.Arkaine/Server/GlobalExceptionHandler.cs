using Microsoft.AspNetCore.Mvc;
using Server.Arkaine.Notification;
using System.Net;

namespace Server.Arkaine
{
    public class GlobalExceptionHandler : IMiddleware
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly INotifier _notifier;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, INotifier notifier)
        {
            _logger = logger;
            _notifier = notifier;
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
                await _notifier.Send(ex.Message);

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