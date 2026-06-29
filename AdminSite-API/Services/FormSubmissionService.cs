using FullProject.Models;
using Contracts.Forms;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;

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
            DateTime? to = null,
            string? formId = null)
        {
            var filters = new List<FilterDefinition<FormSubmission>>();

            if (!string.IsNullOrWhiteSpace(formId))
                filters.Add(Builders<FormSubmission>.Filter.Eq(s => s.FormId, formId.Trim()));
            if (!string.IsNullOrWhiteSpace(formKey))
                filters.Add(Builders<FormSubmission>.Filter.Eq(s => s.FormKey, formKey.Trim().ToLowerInvariant()));
            if (status is not null)
                filters.Add(Builders<FormSubmission>.Filter.Eq(s => s.Status, status.Value));
            if (from is not null)
                filters.Add(Builders<FormSubmission>.Filter.Gte(s => s.SubmittedAt, from.Value));
            if (to is not null)
            {
                var inclusiveTo = to.Value.TimeOfDay == TimeSpan.Zero
                    ? to.Value.Date.AddDays(1).AddTicks(-1)
                    : to.Value;
                filters.Add(Builders<FormSubmission>.Filter.Lte(s => s.SubmittedAt, inclusiveTo));
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = Regex.Escape(search.Trim());
                var regex = new BsonRegularExpression(term, "i");
                filters.Add(Builders<FormSubmission>.Filter.Or(
                    Builders<FormSubmission>.Filter.Regex(s => s.FormName, regex),
                    Builders<FormSubmission>.Filter.Regex(s => s.FormKey, regex),
                    Builders<FormSubmission>.Filter.Regex(s => s.SourcePage, regex),
                    Builders<FormSubmission>.Filter.Regex(s => s.InternalNotes, regex),
                    Builders<FormSubmission>.Filter.Regex(s => s.AssignedToAdminName, regex),
                    Builders<FormSubmission>.Filter.Regex("Fields.Key", regex),
                    Builders<FormSubmission>.Filter.Regex("Fields.Label", regex),
                    Builders<FormSubmission>.Filter.Regex("Fields.Value", regex)));
            }

            var filter = filters.Count == 0
                ? Builders<FormSubmission>.Filter.Empty
                : Builders<FormSubmission>.Filter.And(filters);

            return await _col.Find(filter)
                .SortByDescending(s => s.SubmittedAt)
                .ToListAsync();
        }

        public async Task CreateAsync(FormSubmission submission)
        {
            if (submission.Timeline.Count == 0)
            {
                submission.Timeline.Add(new FormSubmissionTimelineEvent
                {
                    EventType = "created",
                    Message = "Submission received.",
                    CreatedAt = submission.SubmittedAt == default ? DateTime.UtcNow : submission.SubmittedAt
                });
            }

            await _col.InsertOneAsync(submission);
        }

        public async Task<FormSubmission?> GetByIdAsync(string submissionId) =>
            await _col.Find(s => s.Id == submissionId).FirstOrDefaultAsync();

        public async Task MarkViewedAsync(string submissionId, string actorId, string actorName)
        {
            var submission = await GetByIdAsync(submissionId);
            if (submission is null || submission.IsRead) return;

            var now = DateTime.UtcNow;
            var update = Builders<FormSubmission>.Update
                .Set(s => s.IsRead, true)
                .Set(s => s.ViewedAt, now)
                .Set(s => s.ViewedByAdminId, actorId)
                .Set(s => s.UpdatedAt, now)
                .Push(s => s.Timeline, Timeline("viewed", "Submission viewed.", actorId, actorName, now));
            await _col.UpdateOneAsync(s => s.Id == submissionId, update);
        }

        public async Task<FormSubmission?> UpdateWorkflowAsync(
            string submissionId,
            FormSubmissionStatus status,
            string? internalNotes,
            string? assignedToAdminId,
            string? assignedToAdminName,
            string actorId,
            string actorName)
        {
            var submission = await GetByIdAsync(submissionId);
            if (submission is null) return null;

            var now = DateTime.UtcNow;
            var cleanNotes = string.IsNullOrWhiteSpace(internalNotes) ? null : internalNotes.Trim();
            var cleanAssigneeId = string.IsNullOrWhiteSpace(assignedToAdminId) ? null : assignedToAdminId.Trim();
            var cleanAssigneeName = string.IsNullOrWhiteSpace(cleanAssigneeId)
                ? null
                : string.IsNullOrWhiteSpace(assignedToAdminName) ? cleanAssigneeId : assignedToAdminName.Trim();
            var events = new List<FormSubmissionTimelineEvent>();

            if (submission.Status != status)
                events.Add(Timeline("status-changed", $"Status changed from {submission.Status} to {status}.", actorId, actorName, now));

            if (!string.Equals(submission.InternalNotes ?? string.Empty, cleanNotes ?? string.Empty, StringComparison.Ordinal))
                events.Add(Timeline("note-updated", string.IsNullOrWhiteSpace(cleanNotes) ? "Internal note cleared." : "Internal note updated.", actorId, actorName, now));

            if (!string.Equals(submission.AssignedToAdminId ?? string.Empty, cleanAssigneeId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                events.Add(Timeline(
                    string.IsNullOrWhiteSpace(cleanAssigneeId) ? "unassigned" : "assigned",
                    string.IsNullOrWhiteSpace(cleanAssigneeId) ? "Assignment cleared." : $"Assigned to {cleanAssigneeName}.",
                    actorId,
                    actorName,
                    now));

            var update = Builders<FormSubmission>.Update
                .Set(s => s.Status, status)
                .Set(s => s.InternalNotes, cleanNotes)
                .Set(s => s.AssignedToAdminId, cleanAssigneeId)
                .Set(s => s.AssignedToAdminName, cleanAssigneeName)
                .Set(s => s.UpdatedAt, now);

            if (events.Count > 0)
                update = update.PushEach(s => s.Timeline, events);

            var result = await _col.UpdateOneAsync(s => s.Id == submissionId, update);
            return result.MatchedCount == 0 ? null : await GetByIdAsync(submissionId);
        }

        public async Task<long> BulkStatusAsync(
            IEnumerable<string> ids,
            FormSubmissionStatus status,
            string actorId,
            string actorName)
        {
            var cleanIds = ids.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            if (cleanIds.Count == 0) return 0;
            var now = DateTime.UtcNow;
            var update = Builders<FormSubmission>.Update
                .Set(s => s.Status, status)
                .Set(s => s.UpdatedAt, now)
                .Push(s => s.Timeline, Timeline("status-changed", $"Status changed to {status}.", actorId, actorName, now));
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

        private static FormSubmissionTimelineEvent Timeline(
            string eventType,
            string message,
            string actorId,
            string actorName,
            DateTime createdAt) => new()
            {
                EventType = eventType,
                Message = message,
                ActorId = actorId,
                ActorName = actorName,
                CreatedAt = createdAt
            };
    }
}
