using FullProject.DTOs;
using FullProject.Services;
using FullProject.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FullProject.Controllers
{
    [ApiController]
    [Route("api/admin/forms")]
    [Authorize]
    public class FormsController : ControllerBase
    {
        private readonly FormSubmissionService _service;

        public FormsController(FormSubmissionService service)
        {
            _service = service;
        }

        // GET api/admin/forms/submissions
        [HttpGet("submissions")]
        public async Task<IActionResult> GetAll()
        {
            var submissions = await _service.GetAllAsync();
            return Ok(ApiResult.Ok(submissions.Select(s => new FormSubmissionResponseDto
            {
                Id = s.Id,
                PageId = s.PageId,
                SectionId = s.SectionId,
                BlockId = s.BlockId,
                Data = s.Data,
                SubmittedAt = s.SubmittedAt
            }).ToList()));
        }

        // DELETE api/admin/forms/submissions/:submissionId
        [HttpDelete("submissions/{submissionId}")]
        public async Task<IActionResult> Delete(string submissionId)
        {
            var ok = await _service.DeleteAsync(submissionId);
            if (!ok) return NotFound(ApiResult.NotFound("Submission not found."));
            return Ok(ApiResult.Ok("Submission deleted."));
        }
    }
}
