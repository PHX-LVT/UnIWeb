using FullProject.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FullProject.Filters
{
    public class GlobalExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<GlobalExceptionFilter> _logger;

        public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger)
        {
            _logger = logger;
        }

        public void OnException(ExceptionContext context)
        {
            _logger.LogError(context.Exception,
                "Unhandled API exception. TraceId: {TraceId}",
                context.HttpContext.TraceIdentifier);

            var response = ApiResult.ServerError("Operation failed: an unexpected system error occurred.")
                .WithOutcome("unexpected-error");
            response.NotificationKey = "NotificationActionFailed";
            response.NotificationArgs = ["@action:operation.changed", "@reason:unexpected-error"];
            response.TraceId = context.HttpContext.TraceIdentifier;
            context.Result = new ObjectResult(response) { StatusCode = 500 };
            context.ExceptionHandled = true;
        }
    }
}
