namespace FullProject.Settings
{
    public class JwtSettings
    {
        public string Secret { get; set; } = string.Empty;
        public string Issuer { get; set; } = "MySiteAPI";       // default if not in config
        public string Audience { get; set; } = "MySiteClients"; // default if not in config
        public int ExpiryHour { get; set; } = 1;
    }

    public class MongoDbSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
    }

    public class AdminSeedSettings
    {
        public string AdminEmail { get; set; } = string.Empty;
        public string AdminPassword { get; set; } = string.Empty;
    }

    public class CorsSettings
    {
        public string AdminOrigin { get; set; } = string.Empty;
        public string UserOrigin { get; set; } = string.Empty;
    }
}