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
                "Unhandled exception: {Message}", context.Exception.Message);

            var response = ApiResult.ServerError("An unexpected error occurred.");
            context.Result = new ObjectResult(response) { StatusCode = 500 };
            context.ExceptionHandled = true;
        }
    }
}
