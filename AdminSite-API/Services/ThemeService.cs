using FullProject.DTOs;
using FullProject.Models;
using MongoDB.Driver;

namespace FullProject.Services
{
    public class ThemeService
    {
        private readonly IMongoCollection<SiteTheme> _col;

        public ThemeService(IMongoDatabase db)
        {
            _col = db.GetCollection<SiteTheme>("theme");
        }

        public async Task<SiteTheme> GetAsync()
        {
            var theme = await _col.Find(_ => true).FirstOrDefaultAsync();
            if (theme is null)
            {
                theme = new SiteTheme();
                await _col.InsertOneAsync(theme);
            }
            return theme;
        }

        public async Task<SiteTheme> UpdateAsync(ThemeUpdateDto dto)
        {
            var theme = await GetAsync();

            var updates = new List<UpdateDefinition<SiteTheme>>();
            if (dto.FontBody != null) updates.Add(Builders<SiteTheme>.Update.Set(t => t.FontBody, dto.FontBody));
            if (dto.FontHeading != null) updates.Add(Builders<SiteTheme>.Update.Set(t => t.FontHeading, dto.FontHeading));
            if (dto.TextSizeBase != null) updates.Add(Builders<SiteTheme>.Update.Set(t => t.TextSizeBase, dto.TextSizeBase));
            if (dto.TextSizeEyebrow != null) updates.Add(Builders<SiteTheme>.Update.Set(t => t.TextSizeEyebrow, dto.TextSizeEyebrow));
            if (dto.TextSizeHeading != null) updates.Add(Builders<SiteTheme>.Update.Set(t => t.TextSizeHeading, dto.TextSizeHeading));
            if (dto.TextSizeSubheading != null) updates.Add(Builders<SiteTheme>.Update.Set(t => t.TextSizeSubheading, dto.TextSizeSubheading));
            if (dto.TextSizeBody != null) updates.Add(Builders<SiteTheme>.Update.Set(t => t.TextSizeBody, dto.TextSizeBody));
            if (dto.TextSizeSmall != null) updates.Add(Builders<SiteTheme>.Update.Set(t => t.TextSizeSmall, dto.TextSizeSmall));
            if (dto.TextSizeItemTitle != null) updates.Add(Builders<SiteTheme>.Update.Set(t => t.TextSizeItemTitle, dto.TextSizeItemTitle));
            if (dto.ColorPrimary != null) updates.Add(Builders<SiteTheme>.Update.Set(t => t.ColorPrimary, dto.ColorPrimary));
            if (dto.ColorAccent != null) updates.Add(Builders<SiteTheme>.Update.Set(t => t.ColorAccent, dto.ColorAccent));
            if (dto.ColorBackground != null) updates.Add(Builders<SiteTheme>.Update.Set(t => t.ColorBackground, dto.ColorBackground));
            if (dto.ColorText != null) updates.Add(Builders<SiteTheme>.Update.Set(t => t.ColorText, dto.ColorText));
            if (dto.BorderRadius != null) updates.Add(Builders<SiteTheme>.Update.Set(t => t.BorderRadius, dto.BorderRadius));
            if (dto.ButtonSizeScale != null) updates.Add(Builders<SiteTheme>.Update.Set(t => t.ButtonSizeScale, dto.ButtonSizeScale));
            if (dto.ButtonTextSize != null) updates.Add(Builders<SiteTheme>.Update.Set(t => t.ButtonTextSize, dto.ButtonTextSize));
            if (dto.AnimationsEnabled != null) updates.Add(Builders<SiteTheme>.Update.Set(t => t.AnimationsEnabled, dto.AnimationsEnabled.Value));
            if (dto.AnimationSpeed != null) updates.Add(Builders<SiteTheme>.Update.Set(t => t.AnimationSpeed, dto.AnimationSpeed));
            if (dto.SpacingScale != null) updates.Add(Builders<SiteTheme>.Update.Set(t => t.SpacingScale, dto.SpacingScale));

            if (!updates.Any()) return theme;

            return await _col.FindOneAndUpdateAsync<SiteTheme>(
                t => t.Id == theme.Id,
                Builders<SiteTheme>.Update.Combine(updates),
                new FindOneAndUpdateOptions<SiteTheme, SiteTheme> { ReturnDocument = ReturnDocument.After });
        }
    }
}
