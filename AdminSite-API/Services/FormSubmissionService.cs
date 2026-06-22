using FullProject.Models;
using Contracts.Forms;
using MongoDB.Driver;

namespace FullProject.Services
{
    public class FormSubmissionService
    {
        private readonly IMongoCollection<FormSubmission> _col;

        public FormSubmissionService(IMongoDatabase db)
        {
            _col = db.GetCollection<FormSubmission>("form_submissions");
        }

        public async Task<List<FormSubmission>> GetAllAsync(
            string? formKey = null,
            FormSubmissionStatus? status = null,
            string? search = null,
            DateTime? from = null,
            DateTime? to = null)
        {
            var filters = new List<FilterDefinition<FormSubmission>>();

            if (!string.IsNullOrWhiteSpace(formKey))
                filters.Add(Builders<FormSubmission>.Filter.Eq(s => s.FormKey, formKey.Trim().ToLowerInvariant()));
            if (status is not null)
                filters.Add(Builders<FormSubmission>.Filter.Eq(s => s.Status, status.Value));
            if (from is not null)
                filters.Add(Builders<FormSubmission>.Filter.Gte(s => s.SubmittedAt, from.Value));
            if (to is not null)
                filters.Add(Builders<FormSubmission>.Filter.Lte(s => s.SubmittedAt, to.Value));
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                filters.Add(Builders<FormSubmission>.Filter.Or(
                    Builders<FormSubmission>.Filter.Regex(s => s.FormName, new MongoDB.Bson.BsonRegularExpression(term, "i")),
                    Builders<FormSubmission>.Filter.Regex(s => s.SourcePage, new MongoDB.Bson.BsonRegularExpression(term, "i")),
                    Builders<FormSubmission>.Filter.Regex("Fields.Value", new MongoDB.Bson.BsonRegularExpression(term, "i"))));
            }

            var filter = filters.Count == 0
                ? Builders<FormSubmission>.Filter.Empty
                : Builders<FormSubmission>.Filter.And(filters);

            return await _col.Find(filter)
                .SortByDescending(s => s.SubmittedAt)
                .ToListAsync();
        }

        public async Task CreateAsync(FormSubmission submission) =>
            await _col.InsertOneAsync(submission);

        public async Task<FormSubmission?> GetByIdAsync(string submissionId) =>
            await _col.Find(s => s.Id == submissionId).FirstOrDefaultAsync();

        public async Task<bool> UpdateAsync(string submissionId, FormSubmissionStatus status, string? internalNotes)
        {
            var update = Builders<FormSubmission>.Update
                .Set(s => s.Status, status)
                .Set(s => s.InternalNotes, string.IsNullOrWhiteSpace(internalNotes) ? null : internalNotes.Trim())
                .Set(s => s.UpdatedAt, DateTime.UtcNow);
            var result = await _col.UpdateOneAsync(s => s.Id == submissionId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<long> BulkStatusAsync(IEnumerable<string> ids, FormSubmissionStatus status)
        {
            var cleanIds = ids.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            if (cleanIds.Count == 0) return 0;
            var update = Builders<FormSubmission>.Update
                .Set(s => s.Status, status)
                .Set(s => s.UpdatedAt, DateTime.UtcNow);
            var result = await _col.UpdateManyAsync(s => cleanIds.Contains(s.Id), update);
            return result.ModifiedCount;
        }

        public async Task<long> BulkDeleteAsync(IEnumerable<string> ids)
        {
            var cleanIds = ids.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            if (cleanIds.Count == 0) return 0;
            var result = await _col.DeleteManyAsync(s => cleanIds.Contains(s.Id));
            return result.DeletedCount;
        }

        public async Task<bool> DeleteAsync(string submissionId)
        {
            var result = await _col.DeleteOneAsync(s => s.Id == submissionId);
            return result.DeletedCount > 0;
        }
    }
}
