using FullProject.Models;
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

        public async Task<List<FormSubmission>> GetAllAsync() =>
            await _col.Find(_ => true)
                      .SortByDescending(s => s.SubmittedAt)
                      .ToListAsync();

        public async Task CreateAsync(FormSubmission submission) =>
            await _col.InsertOneAsync(submission);

        public async Task<bool> DeleteAsync(string submissionId)
        {
            var result = await _col.DeleteOneAsync(s => s.Id == submissionId);
            return result.DeletedCount > 0;
        }
    }
}
