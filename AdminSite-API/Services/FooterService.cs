using FullProject.DTOs;
using FullProject.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FullProject.Services
{
    public class FooterService
    {
        private readonly IMongoCollection<Footer> _col;

        public FooterService(IMongoDatabase db)
        {
            _col = db.GetCollection<Footer>("footer");
        }

        public async Task<Footer> GetAsync()
        {
            var filter = Builders<Footer>.Filter.Empty;
            var update = Builders<Footer>.Update.SetOnInsert(f => f.CompanyName, "MySite");
            var options = new FindOneAndUpdateOptions<Footer>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After
            };

            return await _col.FindOneAndUpdateAsync(filter, update, options);
        }

        public async Task<Footer> UpdateCompanyNameAsync(string? name)
        {
            var footer = await GetAsync();
            if (name is null) return footer;
            footer.CompanyName = name;
            await _col.ReplaceOneAsync(f => f.Id == footer.Id, footer);
            return footer;
        }
        // ── Groups ────────────────────────────────────────────

        public async Task<Footer> CreateGroupAsync(string label)
        {
            var footer = await GetAsync();
            footer.Groups.Add(new FooterGroup
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Label = label,
                Order = footer.Groups.Count == 0
                    ? 0
                    : footer.Groups.Max(g => g.Order) + 1
            });
            await _col.ReplaceOneAsync(f => f.Id == footer.Id, footer);
            return footer;
        }

        public async Task<Footer?> UpdateGroupAsync(string groupId, string? label)
        {
            if (label is null) return null;
            var footer = await GetAsync();
            var group = footer.Groups.FirstOrDefault(g => g.Id == groupId);
            if (group is null) return null;
            group.Label = label;
            await _col.ReplaceOneAsync(f => f.Id == footer.Id, footer);
            return footer;
        }

        public async Task<Footer?> DeleteGroupAsync(string groupId)
        {
            var footer = await GetAsync();
            var original = footer.Groups.Count;
            footer.Groups.RemoveAll(g => g.Id == groupId);
            if (footer.Groups.Count == original) return null;
            await _col.ReplaceOneAsync(f => f.Id == footer.Id, footer);
            return footer;
        }

        public async Task<Footer?> SetGroupVisibilityAsync(string groupId, bool visible)
        {
            var footer = await GetAsync();
            var group = footer.Groups.FirstOrDefault(g => g.Id == groupId);
            if (group is null) return null;
            group.Visible = visible;
            await _col.ReplaceOneAsync(f => f.Id == footer.Id, footer);
            return footer;
        }

        public async Task<Footer> ReorderGroupsAsync(List<string> orderedIds)
        {
            var footer = await GetAsync();

            for (int i = 0; i < orderedIds.Count; i++)
            {
                var group = footer.Groups.FirstOrDefault(g => g.Id == orderedIds[i]);
                if (group is null) continue; // skip unknown IDs gracefully
                group.Order = i;
            }

            await _col.ReplaceOneAsync(f => f.Id == footer.Id, footer);
            return footer;
        }

        // ── Links (renamed from Sub-footers) ──────────────────

        public async Task<Footer?> CreateLinkAsync(string groupId, FooterLinkCreateDto dto)
        {
            var footer = await GetAsync();
            var group = footer.Groups.FirstOrDefault(g => g.Id == groupId);
            if (group is null) return null;
            group.Links.Add(new FooterLink
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Label = dto.Label,
                Href = dto.Href,
                Order = group.Links.Count == 0
                    ? 0
                    : group.Links.Max(l => l.Order) + 1
            });
            await _col.ReplaceOneAsync(f => f.Id == footer.Id, footer);
            return footer;
        }

        public async Task<Footer?> UpdateLinkAsync(string groupId, string linkId, FooterLinkUpdateDto dto)
        {
            var footer = await GetAsync();
            var group = footer.Groups.FirstOrDefault(g => g.Id == groupId);
            if (group is null) return null;
            var link = group.Links.FirstOrDefault(l => l.Id == linkId);
            if (link is null) return null;
            if (dto.Label != null) link.Label = dto.Label;
            if (dto.Href != null) link.Href = dto.Href;
            await _col.ReplaceOneAsync(f => f.Id == footer.Id, footer);
            return footer;
        }

        public async Task<Footer?> DeleteLinkAsync(string groupId, string linkId)
        {
            var footer = await GetAsync();
            var group = footer.Groups.FirstOrDefault(g => g.Id == groupId);
            if (group is null) return null;
            var original = group.Links.Count;
            group.Links.RemoveAll(l => l.Id == linkId);
            if (group.Links.Count == original) return null;
            await _col.ReplaceOneAsync(f => f.Id == footer.Id, footer);
            return footer;
        }


        public async Task<Footer?> SetLinkVisibilityAsync(string groupId, string linkId, bool visible)
        {
            var footer = await GetAsync();
            var group = footer.Groups.FirstOrDefault(g => g.Id == groupId);
            if (group is null) return null;
            var link = group.Links.FirstOrDefault(l => l.Id == linkId);
            if (link is null) return null;
            link.Visible = visible;
            await _col.ReplaceOneAsync(f => f.Id == footer.Id, footer);
            return footer;
        }

        public async Task<Footer?> ReorderLinksAsync(string groupId, List<string> orderedIds)
        {
            var footer = await GetAsync();
            var group = footer.Groups.FirstOrDefault(g => g.Id == groupId);
            if (group is null) return null;

            var linkIds = group.Links.Select(l => l.Id).ToHashSet();
            var cleanOrderedIds = orderedIds
                .Where(linkIds.Contains)
                .Distinct()
                .ToList();

            if (cleanOrderedIds.Count != linkIds.Count)
            {
                return null;
            }

            var orderedLinks = cleanOrderedIds
                .Select(id => group.Links.First(l => l.Id == id))
                .ToList();

            for (var i = 0; i < orderedLinks.Count; i++)
            {
                orderedLinks[i].Order = i;
            }

            group.Links = orderedLinks;
            await _col.ReplaceOneAsync(f => f.Id == footer.Id, footer);
            return footer;
        }
    }
}
