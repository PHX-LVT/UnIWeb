using Ganss.Xss;

namespace FullProject.Security
{
    public sealed class ContentSanitizer
    {
        private readonly HtmlSanitizer _sanitizer = new();

        public ContentSanitizer()
        {
            _sanitizer.AllowedAttributes.Add("class");
            _sanitizer.AllowedAttributes.Add("target");
            _sanitizer.AllowedAttributes.Add("rel");
            _sanitizer.AllowedAttributes.Add("style");
            _sanitizer.AllowedCssProperties.Add("color");
            _sanitizer.AllowedCssProperties.Add("font-size");
        }

        public string SanitizeHtml(string value) =>
            _sanitizer.Sanitize(value ?? string.Empty);
    }
}
