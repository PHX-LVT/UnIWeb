using AdminSite.Models;
using Contracts.Admin;
using static System.Collections.Specialized.BitVector32;

namespace AdminSite.Services
{
    public class AdminBlockService
    {
        private readonly IHttpService _http;
        public AdminBlockService(IHttpService http) => _http = http;

        private string Base(string pageId, string sectionId) =>
            $"api/admin/pages/{pageId}/sections/{sectionId}/blocks";

        public Task<ApiResponse<List<BlockModel>>> GetAllAsync(string pageId, string sectionId) =>
            _http.GetAsync<List<BlockModel>>(Base(pageId, sectionId));

        public Task<ApiResponse<List<BlockModel>>> GetAllForPageAsync(string pageId) =>
            _http.GetAsync<List<BlockModel>>($"api/admin/pages/{pageId}/blocks");

        public Task<ApiResponse<BlockModel>> GetByIdAsync(string pageId, string sectionId, string blockId) =>
            _http.GetAsync<BlockModel>($"{Base(pageId, sectionId)}/{blockId}");
        public Task<ApiResponse<BlockModel>> CreateAsync(
            string pageId,
            string sectionId,
            BlockCreateDto dto) =>
            _http.PostAsync<BlockModel>(Base(pageId, sectionId), dto);

        public Task<ApiResponse<BlockModel>> CreateInSlotAsync(
            string pageId,
            string sectionId,
            string slotId,
            BlockCreateDto dto)
        {
            dto.ColumnSlotId = slotId;
            return _http.PostAsync<BlockModel>(Base(pageId, sectionId), dto);
        }
        public Task<ApiResponse<BlockModel>> UpdateAsync(string pageId, string sectionId, string blockId, object dto) =>
            _http.PutAsync<BlockModel>($"{Base(pageId, sectionId)}/{blockId}", dto);

        public Task<ApiResponse<BlockModel>> UpdateLayoutAsync(string pageId, string sectionId, string blockId, BlockLayoutDto dto) =>
            _http.PutAsync<BlockModel>($"{Base(pageId, sectionId)}/{blockId}/layout", dto);

        public Task<ApiResponse<object>> DeleteAsync(string pageId, string sectionId, string blockId) =>
            _http.DeleteAsync<object>($"{Base(pageId, sectionId)}/{blockId}");

        public Task<ApiResponse<object>> SetVisibilityAsync(string pageId, string sectionId, string blockId, bool visible) =>
            _http.PutAsync<object>($"{Base(pageId, sectionId)}/{blockId}/visibility",
                new VisibilityRequest { Visible = visible });

        public Task<ApiResponse<object>> ReorderAsync(string pageId, string sectionId, List<string> orderedIds) =>
            _http.PutAsync<object>($"{Base(pageId, sectionId)}/reorder",
                new ReorderRequest { OrderedIds = orderedIds });

        public Task<ApiResponse<BlockAuthoringOperationResponseDto>> UpdateLayoutsAsync(
            string pageId, string sectionId, BlockBulkLayoutUpdateDto dto) =>
            _http.PutAsync<BlockAuthoringOperationResponseDto>($"{Base(pageId, sectionId)}/authoring/layouts", dto);

        public Task<ApiResponse<BlockAuthoringOperationResponseDto>> DuplicateAsync(
            string pageId, string sectionId, IEnumerable<string> blockIds) =>
            _http.PostAsync<BlockAuthoringOperationResponseDto>($"{Base(pageId, sectionId)}/authoring/duplicate",
                new BlockDuplicateRequestDto { BlockIds = blockIds.ToList() });

        public Task<ApiResponse<BlockAuthoringOperationResponseDto>> GroupAsync(
            string pageId, string sectionId, IEnumerable<string> blockIds, Dictionary<string, string> title) =>
            _http.PostAsync<BlockAuthoringOperationResponseDto>($"{Base(pageId, sectionId)}/authoring/group",
                new BlockGroupRequestDto { BlockIds = blockIds.ToList(), Title = title });

        public Task<ApiResponse<BlockAuthoringOperationResponseDto>> UngroupAsync(
            string pageId, string sectionId, string containerId) =>
            _http.PostAsync<BlockAuthoringOperationResponseDto>($"{Base(pageId, sectionId)}/authoring/ungroup/{containerId}", new { });

        public Task<ApiResponse<object>> DeleteGraphsAsync(
            string pageId, string sectionId, IEnumerable<string> blockIds) =>
            _http.PostAsync<object>($"{Base(pageId, sectionId)}/authoring/delete-graphs",
                new BlockDuplicateRequestDto { BlockIds = blockIds.ToList() });

        public Task<ApiResponse<BlockModel>> UpdateAuthoringLockAsync(
            string pageId, string sectionId, string blockId, BlockAuthoringLockUpdateDto dto) =>
            _http.PutAsync<BlockModel>($"{Base(pageId, sectionId)}/{blockId}/authoring-lock", dto);
    }
}
