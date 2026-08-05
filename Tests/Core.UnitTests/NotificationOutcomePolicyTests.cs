using System.Reflection;
using System.Text.RegularExpressions;
using Contracts.Api;
using FullProject.Controllers;
using FullProject.Filters;
using FullProject.Services.LogManagement;
using FullProject.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;

namespace Core.UnitTests;

public sealed class NotificationOutcomePolicyTests
{
    [Theory]
    [InlineData(401, "", "authentication-required")]
    [InlineData(403, "", "permission-denied")]
    [InlineData(404, "The item was not found.", "not-found")]
    [InlineData(409, "The data changed before saving.", "conflict")]
    [InlineData(413, "The file is too large.", "size-exceeded")]
    [InlineData(422, "A value is required.", "validation-failed")]
    [InlineData(429, "Too many requests.", "rate-limited")]
    [InlineData(500, "Database socket failed.", "unexpected-error")]
    public void ErrorCode_IsStableAcrossCommonFailures(int status, string message, string expected) =>
        Assert.Equal(expected, ApiOutcomePolicy.ResolveErrorCode(status, message));

    [Fact]
    public void ValidationResponses_ExposeStructuredFieldErrors()
    {
        var response = ApiResult.Unprocessable<object>(["Title is required.", "Slug is invalid."]);

        Assert.Equal(422, response.StatusCode);
        Assert.Collection(response.FieldErrors!,
            error => Assert.Equal("Title is required.", error.Message),
            error => Assert.Equal("Slug is invalid.", error.Message));
    }

    [Fact]
    public async Task OutcomeFilter_UsesTransportStatusAndAddsStableOperationMetadata()
    {
        var descriptor = Descriptor("Sections", "Update");
        var httpContext = new DefaultHttpContext { TraceIdentifier = "trace-123" };
        httpContext.Request.Method = HttpMethods.Put;
        httpContext.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(descriptor), "test"));
        var actionContext = new ActionContext(httpContext, new RouteData(), descriptor, new ModelStateDictionary());
        var response = new ApiResponse<object> { Success = true, StatusCode = 200, Message = "Rejected." };
        var result = new ObjectResult(response) { StatusCode = StatusCodes.Status403Forbidden };
        var executing = new ResultExecutingContext(actionContext, [], result, new object());

        await new ApiOutcomeFilter().OnResultExecutionAsync(executing, () => Task.FromResult(
            new ResultExecutedContext(actionContext, [], result, new object())));

        Assert.False(response.Success);
        Assert.Equal(403, response.StatusCode);
        Assert.Equal("permission-denied", response.ErrorCode);
        Assert.Equal("page-builder", response.DomainCode);
        Assert.Equal("section.updated", response.ActionCode);
        Assert.Equal("trace-123", response.TraceId);
        Assert.Equal("NotificationActionFailed", response.NotificationKey);
        Assert.Equal(["@action:section.updated", "@reason:permission-denied"], response.NotificationArgs);
    }

    [Fact]
    public async Task OutcomeFilter_NormalizesEmptyPermissionFailure()
    {
        var descriptor = Descriptor("ManagedResources", "BulkDelete");
        var httpContext = new DefaultHttpContext { TraceIdentifier = "trace-forbidden" };
        httpContext.Request.Method = HttpMethods.Delete;
        httpContext.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(descriptor), "test"));
        var actionContext = new ActionContext(httpContext, new RouteData(), descriptor, new ModelStateDictionary());
        var executing = new ResultExecutingContext(actionContext, [], new ForbidResult(), new object());

        await new ApiOutcomeFilter().OnResultExecutionAsync(executing, () => Task.FromResult(
            new ResultExecutedContext(actionContext, [], executing.Result, new object())));

        var result = Assert.IsType<ObjectResult>(executing.Result);
        var response = Assert.IsAssignableFrom<IApiResponse>(result.Value);
        Assert.Equal(403, result.StatusCode);
        Assert.False(response.Success);
        Assert.Equal("permission-denied", response.ErrorCode);
        Assert.Equal("assets", response.DomainCode);
        Assert.Equal("resource.bulk-deleted", response.ActionCode);
        Assert.Equal("NotificationActionFailed", response.NotificationKey);
    }

    [Fact]
    public void EveryMutationControllerAction_ResolvesStableAuditCodes()
    {
        var actions = typeof(BlocksController).Assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any(attribute =>
                    attribute.HttpMethods.Any(httpMethod => httpMethod is "POST" or "PUT" or "PATCH" or "DELETE")))
                .Select(method => Descriptor(type.Name.Replace("Controller", string.Empty), method.Name)))
            .ToList();

        Assert.NotEmpty(actions);
        foreach (var action in actions)
        {
            var operation = AuditActionCatalog.Resolve(action);
            Assert.False(string.IsNullOrWhiteSpace(operation.DomainCode));
            Assert.Contains('.', operation.ActionCode);
            Assert.False(string.IsNullOrWhiteSpace(operation.TargetTypeCode));
        }
    }

    [Fact]
    public void Source_DoesNotExposeRawMutationMessagesOrCaughtExceptionDetails()
    {
        var root = RepositoryRoot();
        var frontendSources = Directory.EnumerateFiles(Path.Combine(root, "AdminSite-Frontend"), "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase));
        var rawMutationNotifications = frontendSources
            .SelectMany(path => File.ReadLines(path).Select((line, index) => (path, line, index)))
            .Where(item => Regex.IsMatch(item.line, @"Http\.Notify\([^;]*(res|response)\.Message"))
            .ToList();

        var apiSources = Directory.EnumerateFiles(Path.Combine(root, "AdminSite-API"), "*.cs", SearchOption.AllDirectories);
        var exposedExceptions = apiSources
            .SelectMany(path => File.ReadLines(path).Select((line, index) => (path, line, index)))
            .Where(item => Regex.IsMatch(item.line, @"(ApiResult|BadRequest|Conflict|Unprocessable|FailAsync).*\b(ex|exception)\.Message"))
            .ToList();

        Assert.Empty(rawMutationNotifications);
        Assert.Empty(exposedExceptions);
    }

    private static ControllerActionDescriptor Descriptor(string controller, string action) => new()
    {
        ControllerName = controller,
        ActionName = action
    };

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "AdminSite-API")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
