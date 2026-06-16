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
        public Task<ApiResponse<BlockModel>> CreateAsync(string pageId, string sectionId, string type) =>
            _http.PostAsync<BlockModel>(Base(pageId, sectionId), type 
                switch
            {
                "text" => (BlockCreateDto)new TextBlockCreateDto(),
                "image" => new ImageBlockCreateDto(),
                "video" => new VideoBlockCreateDto(),
                "file" => new FileBlockCreateDto(),
                "map" => new MapBlockCreateDto(),
                "form" => new FormBlockCreateDto(),
                "card" => new CardBlockCreateDto(),
                "button" => new ButtonBlockCreateDto(),
                "metric" => new MetricBlockCreateDto(),
                "bullet-list" => new BulletListBlockCreateDto(),
                "step" => new StepBlockCreateDto(),
                "icon" => new IconBlockCreateDto(),
                "container" => new ContainerBlockCreateDto(),
                    _ => new TextBlockCreateDto()
            });


        public Task<ApiResponse<BlockModel>> CreateInSlotAsync(string pageId, string sectionId, string slotId, string type) =>
             _http.PostAsync<BlockModel>(Base(pageId, sectionId), type 
                 switch
              {
                "text" => (BlockCreateDto)new TextBlockCreateDto { ColumnSlotId = slotId },
                "image" => new ImageBlockCreateDto { ColumnSlotId = slotId },
                "video" => new VideoBlockCreateDto { ColumnSlotId = slotId },
                "file" => new FileBlockCreateDto { ColumnSlotId = slotId },
                "map" => new MapBlockCreateDto { ColumnSlotId = slotId },
                "form" => new FormBlockCreateDto { ColumnSlotId = slotId },
                "card" => new CardBlockCreateDto { ColumnSlotId = slotId },
                "button" => new ButtonBlockCreateDto { ColumnSlotId = slotId },
                "metric" => new MetricBlockCreateDto { ColumnSlotId = slotId },
                "bullet-list" => new BulletListBlockCreateDto { ColumnSlotId = slotId },
                "step" => new StepBlockCreateDto { ColumnSlotId = slotId },
                "icon" => new IconBlockCreateDto { ColumnSlotId = slotId },
                "container" => new ContainerBlockCreateDto { ColumnSlotId = slotId },
                    _ => new TextBlockCreateDto { ColumnSlotId = slotId }
              });
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
    }
}
