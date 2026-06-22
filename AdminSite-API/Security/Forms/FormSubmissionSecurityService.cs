using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FullProject.Models;
using FullProject.Services.FormServices;
using FullProject.Settings;
using Ganss.Xss;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace FullProject.Security.Forms;

public sealed class FormSubmissionSecurityService
{
    private const string HoneypotKey = "__website";
    private static readonly HtmlSanitizer PlainTextSanitizer = CreatePlainTextSanitizer();
    private static readonly Regex ValidFieldKeyRegex = new(
        "^[A-Za-z][A-Za-z0-9_-]{0,99}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex EmailRegex = new(
        "^[^\\s@]+@[^\\s@]+\\.[^\\s@]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PhoneRegex = new(
        "^[0-9+().\\-\\s]{6,40}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IMongoCollection<FormSubmission> _submissions;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly FormSecuritySettings _settings;
    private readonly ILogger<FormSubmissionSecurityService> _logger;

    public FormSubmissionSecurityService(
        IMongoDatabase database,
        IHttpContextAccessor httpContextAccessor,
        IOptions<FormSecuritySettings> settings,
        ILogger<FormSubmissionSecurityService> logger)
    {
        _submissions = database.GetCollection<FormSubmission>("form_submissions");
        _httpContextAccessor = httpContextAccessor;
        _settings = settings.Value;
        _logger = logger;
    }

    internal async Task<FormSecurityResult> ValidateAsync(
        string formKey,
        IDictionary<string, string>? input,
        IEnumerable<FormFieldValidationRule> fieldRules)
    {
        var context = _httpContextAccessor.HttpContext;
        var ipAddress = context?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = Truncate(context?.Request.Headers.UserAgent.FirstOrDefault(), 500);
        var rules = fieldRules
            .Where(rule => !string.IsNullOrWhiteSpace(rule.Key))
            .GroupBy(rule => rule.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var source = input is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(input, StringComparer.OrdinalIgnoreCase);

        if (source.Remove(HoneypotKey, out var honeypot) && !string.IsNullOrWhiteSpace(honeypot))
            return Reject(formKey, "honeypot", "Submission rejected.", ipAddress);

        if (source.Count > Math.Clamp(_settings.MaximumFieldCount, 1, 100))
            return Reject(formKey, "field-count", "Too many form fields.", ipAddress);

        if (source.Keys.Any(key => !ValidFieldKeyRegex.IsMatch(key) || !rules.ContainsKey(key)))
            return Reject(formKey, "unexpected-field", "Invalid form field.", ipAddress);

        var clean = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules.Values)
        {
            var value = source.GetValueOrDefault(rule.Key) ?? string.Empty;
            value = SanitizePlainText(value);
            var maximumLength = Math.Clamp(rule.MaximumLength, 1, 2_000);

            if (value.Length > maximumLength)
                return Reject(formKey, "field-length", $"{rule.Key} is too long.", ipAddress);
            if (rule.Required && string.IsNullOrWhiteSpace(value))
                return Reject(formKey, "required-field", "Required form field missing.", ipAddress);
            if (!IsValidTypedValue(rule.Type, value))
                return Reject(formKey, "field-type", $"Invalid {rule.Key} value.", ipAddress);
            if (!string.IsNullOrWhiteSpace(value) &&
                rule.AllowedValues is { Count: > 0 } &&
                !rule.AllowedValues.Contains(value))
            {
                return Reject(formKey, "field-option", $"Invalid {rule.Key} option.", ipAddress);
            }

            if (!string.IsNullOrWhiteSpace(value))
                clean[rule.Key] = value;
        }

        if (clean.Sum(field => field.Key.Length + field.Value.Length) >
            Math.Clamp(_settings.MaximumPayloadCharacters, 1_000, 50_000))
        {
            return Reject(formKey, "payload-length", "Form payload too large.", ipAddress);
        }

        var fingerprint = CreateFingerprint(formKey, clean);
        var now = DateTime.UtcNow;
        var cooldownCutoff = now.AddSeconds(-Math.Clamp(_settings.CooldownSeconds, 1, 60));
        var duplicateCutoff = now.AddMinutes(-Math.Clamp(_settings.DuplicateWindowMinutes, 1, 1440));

        var cooldownFilter = Builders<FormSubmission>.Filter.And(
            Builders<FormSubmission>.Filter.Eq(submission => submission.FormKey, formKey),
            Builders<FormSubmission>.Filter.Eq("Security.IpAddress", ipAddress),
            Builders<FormSubmission>.Filter.Gte(submission => submission.SubmittedAt, cooldownCutoff));
        if (await _submissions.Find(cooldownFilter).Limit(1).AnyAsync())
            return Reject(formKey, "cooldown", "Please wait before submitting again.", ipAddress, StatusCodes.Status429TooManyRequests);

        var duplicateFilter = Builders<FormSubmission>.Filter.And(
            Builders<FormSubmission>.Filter.Eq(submission => submission.FormKey, formKey),
            Builders<FormSubmission>.Filter.Eq("Security.Fingerprint", fingerprint),
            Builders<FormSubmission>.Filter.Gte(submission => submission.SubmittedAt, duplicateCutoff));
        if (await _submissions.Find(duplicateFilter).Limit(1).AnyAsync())
            return Reject(formKey, "duplicate", "This submission was already received.", ipAddress, StatusCodes.Status409Conflict);

        return new FormSecurityResult
        {
            Accepted = true,
            StatusCode = StatusCodes.Status200OK,
            Data = clean,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Fingerprint = fingerprint
        };
    }

    private FormSecurityResult Reject(string formKey, string reason, string message, string ipAddress, int statusCode = StatusCodes.Status400BadRequest)
    {
        _logger.LogWarning(
            "Rejected public form {FormKey}. Reason: {Reason}. Client: {ClientHash}",
            formKey,
            reason,
            ShortHash(ipAddress));

        return new FormSecurityResult
        {
            Accepted = false,
            StatusCode = statusCode,
            Message = message
        };
    }

    private static bool IsValidTypedValue(string? type, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;

        return type?.Trim().ToLowerInvariant() switch
        {
            "email" => value.Length <= 254 && EmailRegex.IsMatch(value),
            "tel" or "phone" => value.Length <= 40 && PhoneRegex.IsMatch(value),
            "number" => decimal.TryParse(value, out _),
            "date" => DateTime.TryParse(value, out _),
            "checkbox" => value is "true" or "false" or "on" or "1" or "0",
            "url" => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https",
            _ => true
        };
    }

    private static string SanitizePlainText(string value)
    {
        var sanitized = PlainTextSanitizer.Sanitize(value ?? string.Empty);
        return WebUtility.HtmlDecode(sanitized)
            .Replace("\0", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static HtmlSanitizer CreatePlainTextSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedAttributes.Clear();
        return sanitizer;
    }

    private static string CreateFingerprint(string formKey, IReadOnlyDictionary<string, string> data)
    {
        var canonical = string.Join("\n", data
            .Where(field => !string.Equals(field.Key, "PageUrl", StringComparison.OrdinalIgnoreCase))
            .OrderBy(field => field.Key, StringComparer.OrdinalIgnoreCase)
            .Select(field => $"{field.Key.Trim().ToLowerInvariant()}={field.Value.Trim()}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{formKey}\n{canonical}"))).ToLowerInvariant();
    }

    private static string ShortHash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty)))[..12].ToLowerInvariant();

    private static string Truncate(string? value, int maximumLength) =>
        string.IsNullOrEmpty(value) ? string.Empty : value[..Math.Min(value.Length, maximumLength)];

}
