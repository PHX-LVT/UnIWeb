using Contracts.Forms;
using FullProject.Models;
using FullProject.Security.Forms;
using FullProject.Services.Metrics;
using MongoDB.Bson;

namespace FullProject.Services.FormServices;

public sealed class PublicFormSubmissionService
{
    private readonly FormDefinitionService _definitions;
    private readonly FormValidationService _validation;
    private readonly FormSubmissionSecurityService _security;
    private readonly FormSubmissionService _submissions;
    private readonly ILogger<PublicFormSubmissionService> _logger;
    private readonly VisitorMetricService _metrics;

    public PublicFormSubmissionService(
        FormDefinitionService definitions,
        FormValidationService validation,
        FormSubmissionSecurityService security,
        FormSubmissionService submissions,
        ILogger<PublicFormSubmissionService> logger,
        VisitorMetricService metrics)
    {
        _definitions = definitions;
        _validation = validation;
        _security = security;
        _submissions = submissions;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<(int StatusCode, PublicFormSubmitResponse Response)> SubmitAsync(
        string formKey,
        PublicFormSubmitRequest request)
    {
        var normalizedKey = FormDefinitionService.NormalizeKey(formKey);
        if (normalizedKey is null)
            return NotFound();

        var definition = await _definitions.GetActiveByKeyAsync(normalizedKey);
        if (definition is null)
            return NotFound();

        var definitionErrors = _validation.ValidateDefinition(definition);
        if (definitionErrors.Count > 0)
        {
            _logger.LogError("Active form definition {FormKey} is invalid: {Errors}", normalizedKey, string.Join("; ", definitionErrors));
            return (StatusCodes.Status503ServiceUnavailable, new PublicFormSubmitResponse
            {
                Accepted = false,
                Message = "This form is temporarily unavailable."
            });
        }

        var language = NormalizeLanguage(request.Language);
        var data = request.Data ?? new Dictionary<string, string>();
        var fieldErrors = _validation.Validate(definition, data, language);
        if (fieldErrors.Count > 0)
        {
            return (StatusCodes.Status422UnprocessableEntity, new PublicFormSubmitResponse
            {
                Accepted = false,
                Message = "Validation failed.",
                FieldErrors = fieldErrors
            });
        }

        var securityInput = new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase)
        {
            ["__website"] = request.Honeypot ?? string.Empty
        };
        var securityResult = await _security.ValidateAsync(
            normalizedKey,
            securityInput,
            _validation.BuildSecurityRules(definition));
        if (!securityResult.Accepted)
        {
            return (securityResult.StatusCode, new PublicFormSubmitResponse
            {
                Accepted = false,
                Message = securityResult.Message
            });
        }

        var now = DateTime.UtcNow;
        var submission = new FormSubmission
        {
            Id = ObjectId.GenerateNewId().ToString(),
            FormId = definition.Id,
            FormKey = definition.Key,
            FormName = FormValidationService.ResolveText(definition.Name, language, definition.Key),
            Language = language,
            SourcePage = NormalizeSourcePage(request.SourcePage, securityResult.Data.GetValueOrDefault("PageUrl")),
            Status = FormSubmissionStatus.New,
            Fields = definition.Fields
                .OrderBy(field => field.Order)
                .Where(field => securityResult.Data.ContainsKey(field.Key) &&
                                !string.Equals(field.Key, "PageUrl", StringComparison.OrdinalIgnoreCase))
                .Select(field => new FormSubmissionFieldSnapshot
                {
                    Key = field.Key,
                    Label = FormValidationService.ResolveText(field.Label, language, field.Key),
                    Type = field.Type,
                    Value = securityResult.Data[field.Key],
                    Order = field.Order
                })
                .ToList(),
            Data = new(securityResult.Data),
            Security = new FormSubmissionSecurity
            {
                IpAddress = securityResult.IpAddress,
                UserAgent = securityResult.UserAgent,
                Fingerprint = securityResult.Fingerprint
            },
            SubmittedAt = now,
            UpdatedAt = now
        };

        await _submissions.CreateAsync(submission);
        await _metrics.IncrementAsync(VisitorMetricService.FormSubmission, "form", definition.Key, submission.SourcePage);

        return (StatusCodes.Status201Created, new PublicFormSubmitResponse
        {
            Accepted = true,
            SubmissionId = submission.Id,
            Message = "Form submitted successfully."
        });
    }

    private static (int, PublicFormSubmitResponse) NotFound() =>
        (StatusCodes.Status404NotFound, new PublicFormSubmitResponse
        {
            Accepted = false,
            Message = "Form not found."
        });

    private static string NormalizeLanguage(string? language)
    {
        var normalized = language?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) || normalized.Length > 12 ? "en" : normalized;
    }

    private static string NormalizeSourcePage(string? sourcePage, string? fallbackSourcePage = null)
    {
        var source = !string.IsNullOrWhiteSpace(sourcePage)
            ? sourcePage.Trim()
            : fallbackSourcePage?.Trim() ?? string.Empty;
        if (source.Length > 1_000) source = source[..1_000];
        return source.StartsWith('/') ||
               Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? source
            : string.Empty;
    }
}
