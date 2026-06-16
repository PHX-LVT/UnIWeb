using FullProject.DTOs;
using FullProject.Models;
using MongoDB.Driver;

namespace FullProject.Services
{
    public class GlobalButtonsService
    {
        private readonly IMongoCollection<GlobalButton> _col;

        public GlobalButtonsService(IMongoDatabase db)
        {
            _col = db.GetCollection<GlobalButton>("global_buttons");
        }

        public async Task<List<GlobalButton>> GetAllAsync() =>
            await _col.Find(_ => true).SortBy(b => b.Order).ToListAsync();

        public async Task<GlobalButton> CreateAsync(GlobalButtonCreateDto dto)
        {
            var count = await _col.CountDocumentsAsync(_ => true);
            var btn = new GlobalButton
            {
                LabelText = NormalizeLabel(dto.LabelText),
                Action = dto.Action,
                Href = dto.Href,
                Position = dto.Position,
                Order = (int)count
            };
            await _col.InsertOneAsync(btn);
            return btn;
        }

        public async Task<GlobalButton?> UpdateAsync(string id, GlobalButtonUpdateDto dto)
        {
            var updates = new List<UpdateDefinition<GlobalButton>>();
            if (dto.LabelText != null)
            {
                var labelText = NormalizeLabel(dto.LabelText);
                updates.Add(Builders<GlobalButton>.Update.Set(b => b.LabelText, labelText));
            }
            if (dto.Action != null) updates.Add(Builders<GlobalButton>.Update.Set(b => b.Action, dto.Action.Value));
            if (dto.Href != null) updates.Add(Builders<GlobalButton>.Update.Set(b => b.Href, dto.Href));
            if (dto.Position != null) updates.Add(Builders<GlobalButton>.Update.Set(b => b.Position, dto.Position.Value));

            if (!updates.Any())
                return await _col.Find(b => b.Id == id).FirstOrDefaultAsync();

            return await _col.FindOneAndUpdateAsync<GlobalButton>(
                b => b.Id == id,
                Builders<GlobalButton>.Update.Combine(updates),
                new FindOneAndUpdateOptions<GlobalButton, GlobalButton> { ReturnDocument = ReturnDocument.After });
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var result = await _col.DeleteOneAsync(b => b.Id == id);
            return result.DeletedCount > 0;
        }

        public async Task<bool> SetVisibilityAsync(string id, bool visible)
        {
            var result = await _col.UpdateOneAsync(b => b.Id == id,
                Builders<GlobalButton>.Update.Set(b => b.Visible, visible));
            return result.ModifiedCount > 0;
        }

        public async Task ReorderAsync(List<string> orderedIds)
        {
            var writes = orderedIds.Select((id, i) =>
                new UpdateOneModel<GlobalButton>(
                    Builders<GlobalButton>.Filter.Eq(b => b.Id, id),
                    Builders<GlobalButton>.Update.Set(b => b.Order, i))
            ).Cast<WriteModel<GlobalButton>>().ToList();

            if (writes.Count == 0) return;
            await _col.BulkWriteAsync(writes);
        }

        private static Dictionary<string, string> NormalizeLabel(Dictionary<string, string>? labelText)
        {
            var normalized = labelText?
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
                .ToDictionary(kv => kv.Key.Trim().ToLowerInvariant(), kv => kv.Value ?? string.Empty)
                ?? new Dictionary<string, string>();

            if (!normalized.TryGetValue("en", out var english) || string.IsNullOrWhiteSpace(english))
            {
                normalized["en"] = string.Empty;
            }

            return normalized;
        }
    }
}
