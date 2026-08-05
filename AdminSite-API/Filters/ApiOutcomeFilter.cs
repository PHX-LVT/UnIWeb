using Contracts.Api;
using FullProject.Services.LogManagement;
using FullProject.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FullProject.Filters;

public sealed class ApiOutcomeFilter : IAsyncResultFilter
{
    private static readonly HashSet<string> MutationMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        context.Result = NormalizeEmptyFailure(context.Result);
        if (context.Result is ObjectResult { Value: IApiResponse response } objectResult)
        {
            var explicitStatus = objectResult.StatusCode;
            var actualStatus = explicitStatus ?? (response.StatusCode > 0 ? response.StatusCode : StatusCodes.Status200OK);
            objectResult.StatusCode = actualStatus;
            response.StatusCode = actualStatus;
            response.Success = actualStatus is >= 200 and < 300 && response.Success;
            response.TraceId = context.HttpContext.TraceIdentifier;

            var descriptor = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
            if (descriptor is not null)
            {
                var operation = AuditActionCatalog.Resolve(descriptor);
                response.DomainCode = operation.DomainCode;
                response.ActionCode = operation.ActionCode;
                context.HttpContext.Items[ApiOutcomePolicy.OperationDomainItem] = operation.DomainCode;
                context.HttpContext.Items[ApiOutcomePolicy.OperationActionItem] = operation.ActionCode;

                if (!response.Success)
                {
                    response.ErrorCode ??= ApiOutcomePolicy.ResolveErrorCode(actualStatus, response.Message);
                    response.NotificationKey ??= "NotificationActionFailed";
                    response.NotificationArgs ??= ApiOutcomePolicy.FailureNotificationArgs(operation.ActionCode, response.ErrorCode);
                    context.HttpContext.Items[ApiOutcomePolicy.OperationErrorItem] = response.ErrorCode;
                }
                else if (MutationMethods.Contains(context.HttpContext.Request.Method))
                {
                    response.NotificationKey ??= "NotificationActionSucceeded";
                    response.NotificationArgs ??= ApiOutcomePolicy.SuccessNotificationArgs(operation.ActionCode);
                }
            }
        }

        await next();
    }

    private static IActionResult NormalizeEmptyFailure(IActionResult result) => result switch
    {
        ForbidResult => Failure(
            StatusCodes.Status403Forbidden,
            "Permission denied.",
            "permission-denied"),
        UnauthorizedResult or ChallengeResult => Failure(
            StatusCodes.Status401Unauthorized,
            "Authentication required.",
            "authentication-required"),
        StatusCodeResult status when status.StatusCode >= 400 => Failure(
            status.StatusCode,
            ApiOutcomePolicy.SafeFallback(status.StatusCode, "operation.changed"),
            ApiOutcomePolicy.ResolveErrorCode(status.StatusCode, null)),
        _ => result
    };

    private static ObjectResult Failure(int statusCode, string message, string errorCode) => new(
        ApiResponse<object>.Fail(message, statusCode, errorCode: errorCode))
    {
        StatusCode = statusCode
    };
}
