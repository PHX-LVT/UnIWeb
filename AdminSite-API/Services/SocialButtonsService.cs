using FullProject.DTOs;
using FullProject.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FullProject.Services
{
    public class SocialButtonsService
    {
        private readonly IMongoCollection<SocialButtonGroup> _col;

        public SocialButtonsService(IMongoDatabase db)
        {
            _col = db.GetCollection<SocialButtonGroup>("social");
        }

        public async Task<SocialButtonGroup> GetGroupAsync()
        {
            var group = await _col.Find(_ => true).FirstOrDefaultAsync();
            if (group is null)
            {
                group = new SocialButtonGroup
                {
                    GroupVisible = true,
                    Buttons = new List<SocialButton>
                    {
                        new() {
                            Id    = ObjectId.GenerateNewId().ToString(),
                            Label = "Facebook",
                            Icon  = "fab fa-facebook",
                            Href  = "https://facebook.com",
                            Order = 0
                        }
                    }
                };
                await _col.InsertOneAsync(group);
            }
            return group;
        }

        public async Task<SocialButton> CreateAsync(SocialButtonCreateDto dto)
        {
            var group = await GetGroupAsync();
            var btn = new SocialButton
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Label = dto.Label,
                Icon = dto.Icon,
                Href = dto.Href,
                Order = group.Buttons.Count
            };
            await _col.UpdateOneAsync(g => g.Id == group.Id,
                Builders<SocialButtonGroup>.Update.Push(g => g.Buttons, btn));
            return btn;
        }

        public async Task<SocialButton?> UpdateAsync(string id, SocialButtonUpdateDto dto)
        {
            var group = await GetGroupAsync();
            var btn = group.Buttons.FirstOrDefault(b => b.Id == id);
            if (btn is null) return null;

            if (dto.Label != null) btn.Label = dto.Label;
            if (dto.Icon != null) btn.Icon = dto.Icon;
            if (dto.Href != null) btn.Href = dto.Href;

            await _col.ReplaceOneAsync(g => g.Id == group.Id, group);
            return btn;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var group = await GetGroupAsync();
            var original = group.Buttons.Count;
            group.Buttons.RemoveAll(b => b.Id == id);
            if (group.Buttons.Count == original) return false;
            await _col.ReplaceOneAsync(g => g.Id == group.Id, group);
            return true;
        }

        public async Task<bool> SetButtonVisibilityAsync(string id, bool visible)
        {
            var group = await GetGroupAsync();
            var btn = group.Buttons.FirstOrDefault(b => b.Id == id);
            if (btn is null) return false;
            btn.Visible = visible;
            await _col.ReplaceOneAsync(g => g.Id == group.Id, group);
            return true;
        }

        public async Task SetGroupVisibilityAsync(bool visible)
        {
            var group = await GetGroupAsync();
            await _col.UpdateOneAsync(g => g.Id == group.Id,
                Builders<SocialButtonGroup>.Update.Set(g => g.GroupVisible, visible));
        }
    }
}

