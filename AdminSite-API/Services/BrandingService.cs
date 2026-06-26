using FullProject.DTOs;
using FullProject.Models;
using FullProject.Services.AssetService;
using MongoDB.Driver;

namespace FullProject.Services
{
    public class BrandingService
    {
        private readonly IMongoCollection<Branding> _col;
        private readonly AssetCleanupService _assetCleanup;

        public BrandingService(IMongoDatabase db, AssetCleanupService assetCleanup)
        {
            _col = db.GetCollection<Branding>("branding");
            _assetCleanup = assetCleanup;
        }

        public async Task<Branding> GetAsync()
        {
            var branding = await _col.Find(_ => true).FirstOrDefaultAsync();
            if (branding is null)
            {
                branding = new Branding { CompanyName = "MySite", Href = "/", LogoUrl = "" };
                await _col.InsertOneAsync(branding);
            }
            return branding;
        }


        public async Task<Branding> UpdateAsync(BrandingUpdateDto dto)
        {
            var branding = await GetAsync();
            var updates = new List<UpdateDefinition<Branding>>();

            if (dto.CompanyName != null)
                updates.Add(Builders<Branding>.Update.Set(b => b.CompanyName, dto.CompanyName));
            if (dto.Href != null)
                updates.Add(Builders<Branding>.Update.Set(b => b.Href, dto.Href));
            if (dto.LogoUrl != null)
            {
                updates.Add(Builders<Branding>.Update.Set(b => b.LogoUrl, dto.LogoUrl));
            }

            var updated = await _col.FindOneAndUpdateAsync<Branding>(
                b => b.Id == branding.Id,
                Builders<Branding>.Update.Combine(updates),
                new FindOneAndUpdateOptions<Branding> { ReturnDocument = ReturnDocument.After }
                );

            if (dto.LogoUrl != null)
                await _assetCleanup.DeleteIfUnusedAsync(branding.LogoUrl, dto.LogoUrl);

            return updated;
        }

    }
}

