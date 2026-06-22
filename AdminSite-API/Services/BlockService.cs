using FullProject.Data;
using FullProject.DTOs;
using FullProject.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Contracts.Admin;
using GlobalManager.Services.AssetService;

namespace FullProject.Services
{
    public class BlockService
    {
        private readonly MongoDbContext _context;
        private readonly R2AssetService _r2Assets;
        private static readonly Ganss.Xss.HtmlSanitizer _sanitizer = new();

        public BlockService(MongoDbContext context, R2AssetService r2Assets)
        {
            _context = context;
            _r2Assets = r2Assets;
        }

        private static Dictionary<string, string> SanitizeDictionary(Dictionary<string, string>? input)
        {
            if (input == null) return new();
            return input.ToDictionary(kv => kv.Key,
                kv => _sanitizer.Sanitize(kv.Value ?? string.Empty));
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // ADMIN WORKSPACE BACKEND METHODS (DRAFT EXCLUSIVE)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public async Task<List<Block>> GetBySectionAsync(string pageId, string sectionId)
        {
            var page = await _context.PagesDraft.Find(p => p.Id == pageId).FirstOrDefaultAsync();
            if (page is null) return new();
            var section = await _context.SectionsDraft.Find(s => s.Id == sectionId).FirstOrDefaultAsync();
            if (section is null) return new();
            return await _context.BlocksDraft
                .Find(b => b.PageStableId == page.StableId && b.SectionStableId == section.StableId)
                .SortBy(b => b.Order)
                .ToListAsync();
        }

        public async Task<List<Block>> GetByPageAsync(string pageId)
        {
            var page = await _context.PagesDraft.Find(p => p.Id == pageId).FirstOrDefaultAsync();
            if (page is null) return new();
            return await _context.BlocksDraft
                .Find(b => b.PageStableId == page.StableId)
                .SortBy(b => b.Order)
                .ToListAsync();
        }

        public async Task<Block?> GetByIdAsync(string pageId, string sectionId, string blockId)
        {
            if (!ObjectId.TryParse(pageId, out _) ||
                !ObjectId.TryParse(sectionId, out _) ||
                !ObjectId.TryParse(blockId, out _))
            {
                return null;
            }

            var page = await _context.PagesDraft.Find(p => p.Id == pageId).FirstOrDefaultAsync();
            if (page is null) return null;
            var section = await _context.SectionsDraft.Find(s => s.Id == sectionId).FirstOrDefaultAsync();
            if (section is null) return null;
            return await _context.BlocksDraft
                .Find(b => b.PageStableId == page.StableId &&
                           b.SectionStableId == section.StableId &&
                           b.Id == blockId)
                .FirstOrDefaultAsync();
        }
        public async Task<Block> CreateAsync(string pageId, string sectionId, BlockCreateDto dto)
        {
            // Fetch page and section to get their StableIds
            var page = await _context.PagesDraft
                .Find(p => p.Id == pageId).FirstOrDefaultAsync()
                ?? throw new ArgumentException("Page not found");
            var section = await _context.SectionsDraft
                .Find(s => s.Id == sectionId).FirstOrDefaultAsync()
                ?? throw new ArgumentException("Section not found");

            var count = await _context.BlocksDraft.CountDocumentsAsync(
                b => b.PageStableId == page.StableId &&
                     b.SectionStableId == section.StableId);

            Block block = dto switch
            {
                TextBlockCreateDto t => new TextBlock
                {
                    Title = SanitizeDictionary(t.Title),
                    Content = SanitizeDictionary(t.Content)
                },
                ImageBlockCreateDto img => new ImageBlock
                {
                    ImageUrl = img.ImageUrl,
                    AltText = img.AltText
                },
                VideoBlockCreateDto v => new VideoBlock
                {
                    EmbedUrl = CleanVideoUrl(v.EmbedUrl) ?? string.Empty,
                    Title = v.Title
                },
                FileBlockCreateDto f => new FileBlock
                {
                    FileUrl = f.FileUrl,
                    Filename = f.Filename,
                    FileType = f.FileType
                },
                MapBlockCreateDto m => new MapBlock
                {
                    CenterLat = m.CenterLat,
                    CenterLng = m.CenterLng,
                    DefaultZoom = m.DefaultZoom,
                    Pins = m.Pins.Select(p => new MapPin
                    {
                        Id = string.IsNullOrEmpty(p.Id)
                            ? ObjectId.GenerateNewId().ToString() : p.Id,
                        Label = p.Label,
                        Lat = p.Lat,
                        Lng = p.Lng,
                        Href = CleanUrl(p.Href)
                    }).ToList()
                },
                FormBlockCreateDto form => new FormBlock
                {
                    Fields = form.Fields.Select(f => new FormField
                    {
                        Name = f.Name,
                        Type = f.Type,
                        Label = f.Label,
                        Required = f.Required,
                        Options = f.Options,
                        Order = f.Order
                    }).ToList(),
                    SubmitButtonLabel = form.SubmitButtonLabel
                },
                CardBlockCreateDto card => new CardBlock
                {
                    Icon = card.Icon,
                    Title = card.Title,
                    Description = SanitizeDictionary(card.Description),
                    ImageUrl = card.ImageUrl,
                    ButtonLabel = card.ButtonLabel,
                    Href = CleanUrl(card.Href),
                    Action = card.Action,
                    FormDefinitionId = card.FormDefinitionId
                },
                ButtonBlockCreateDto button => new ButtonBlock
                {
                    Label = button.Label,
                    Href = CleanUrl(button.Href),
                    Action = button.Action,
                    FormDefinitionId = button.FormDefinitionId,
                    Style = NormalizeButtonStyle(button.Style)
                },
                MetricBlockCreateDto metric => new MetricBlock
                {
                    Icon = metric.Icon,
                    Label = metric.Label,
                    Value = metric.Value,
                    Prefix = metric.Prefix,
                    Suffix = metric.Suffix,
                    Description = SanitizeDictionary(metric.Description)
                },
                BulletListBlockCreateDto list => new BulletListBlock
                {
                    Title = list.Title,
                    Items = list.Items.Select(MapBulletItem).ToList()
                },
                StepBlockCreateDto step => new StepBlock
                {
                    Icon = step.Icon,
                    StepLabel = step.StepLabel,
                    Title = step.Title,
                    Description = SanitizeDictionary(step.Description)
                },
                IconBlockCreateDto icon => new IconBlock
                {
                    Icon = icon.Icon,
                    Label = icon.Label,
                    Description = SanitizeDictionary(icon.Description)
                },
                ContainerBlockCreateDto container => new ContainerBlock
                {
                    Title = SanitizeDictionary(container.Title),
                    LayoutMode = NormalizeContainerLayout(container.LayoutMode),
                    Columns = Math.Clamp(container.Columns, 1, 6),
                    Gap = NormalizeBlockGap(container.Gap)
                },
                _ => throw new ArgumentException("Unknown block type")
            };

            block.StableId = Guid.NewGuid().ToString();
            block.ColumnSlotId = dto.ColumnSlotId;
            block.BlockZone = NormalizeBlockZone(dto.BlockZone);
            block.ParentBlockId = dto.ParentBlockId;
            block.PageStableId = page.StableId;       // â† GUID
            block.SectionStableId = section.StableId; // â† GUID
            block.Visible = dto.Visible;
            block.Order = (int)count;
            block.Layout = MapLayout(dto.Layout);
            block.Version = 1;
            block.CreatedAt = DateTime.UtcNow;
            block.UpdatedAt = DateTime.UtcNow;
            block.Buttons = dto.Buttons?.Select(MapButton).ToList() ?? new();

            await _context.BlocksDraft.InsertOneAsync(block);
            return block;
        }

        public async Task<Block?> UpdateAsync(string pageId, string sectionId,
            string blockId, BlockUpdateDto dto)
        {
            var existing = await GetByIdAsync(pageId, sectionId, blockId);
            if (existing is null) return null;

            var baseUpdates = new List<UpdateDefinition<Block>>
            {
                Builders<Block>.Update.Set(b => b.UpdatedAt, DateTime.UtcNow),
                Builders<Block>.Update.Inc(b => b.Version, 1)
            };

            if (dto.Visible.HasValue)
                baseUpdates.Add(Builders<Block>.Update.Set(b => b.Visible, dto.Visible.Value));
            if (dto.Buttons is not null)
                baseUpdates.Add(Builders<Block>.Update.Set(b => b.Buttons,
                    dto.Buttons.Select(MapButton).ToList()));
            if (dto.BlockZone is not null)
                baseUpdates.Add(Builders<Block>.Update.Set(b => b.BlockZone, NormalizeBlockZone(dto.BlockZone)));
            if (dto.ParentBlockId is not null)
                baseUpdates.Add(Builders<Block>.Update.Set(b => b.ParentBlockId, string.IsNullOrWhiteSpace(dto.ParentBlockId) ? null : dto.ParentBlockId));
            if (dto.Layout is not null)
                baseUpdates.Add(Builders<Block>.Update.Set(b => b.Layout, MapLayout(dto.Layout)));

            var baseUpdate = Builders<Block>.Update.Combine(baseUpdates);

            switch (existing, dto)
            {
                case (TextBlock _, TextBlockUpdateDto tDto):
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(baseUpdate,
                            Builders<Block>.Update
                                .Set(b => ((TextBlock)b).Title, SanitizeDictionary(tDto.Title))
                                .Set(b => ((TextBlock)b).Content, SanitizeDictionary(tDto.Content))));
                    break;

                case (ImageBlock _, ImageBlockUpdateDto imgDto):
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(baseUpdate,
                            Builders<Block>.Update
                                .Set(b => ((ImageBlock)b).ImageUrl, imgDto.ImageUrl)
                                .Set(b => ((ImageBlock)b).AltText, imgDto.AltText)));
                    break;

                case (VideoBlock _, VideoBlockUpdateDto vDto):
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(baseUpdate,
                            Builders<Block>.Update
                                .Set(b => ((VideoBlock)b).EmbedUrl,
                                    CleanVideoUrl(vDto.EmbedUrl) ?? string.Empty)
                                .Set(b => ((VideoBlock)b).Title, vDto.Title)));
                    break;

                case (FileBlock _, FileBlockUpdateDto fDto):
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(baseUpdate,
                            Builders<Block>.Update
                                .Set(b => ((FileBlock)b).FileUrl, fDto.FileUrl)
                                .Set(b => ((FileBlock)b).Filename, fDto.Filename)
                                .Set(b => ((FileBlock)b).FileType, fDto.FileType)));
                    break;

                case (MapBlock _, MapBlockUpdateDto mDto):
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(baseUpdate,
                            Builders<Block>.Update
                                .Set(b => ((MapBlock)b).CenterLat, mDto.CenterLat)
                                .Set(b => ((MapBlock)b).CenterLng, mDto.CenterLng)
                                .Set(b => ((MapBlock)b).DefaultZoom, mDto.DefaultZoom)
                                .Set(b => ((MapBlock)b).Pins,
                                    mDto.Pins.Select(p => new MapPin
                                    {
                                        Id = string.IsNullOrEmpty(p.Id)
                                            ? ObjectId.GenerateNewId().ToString() : p.Id,
                                        Label = p.Label,
                                        Lat = p.Lat,
                                        Lng = p.Lng,
                                        Href = CleanUrl(p.Href)
                                    }).ToList())));
                    break;

                case (FormBlock _, FormBlockUpdateDto formDto):
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(baseUpdate,
                            Builders<Block>.Update
                                .Set(b => ((FormBlock)b).Fields,
                                    formDto.Fields.Select(f => new FormField
                                    {
                                        Name = f.Name,
                                        Type = f.Type,
                                        Label = f.Label,
                                        Required = f.Required,
                                        Options = f.Options,
                                        Order = f.Order
                                    }).ToList())
                                .Set(b => ((FormBlock)b).SubmitButtonLabel,
                                    formDto.SubmitButtonLabel)));
                    break;

                case (CardBlock _, CardBlockUpdateDto cardDto):
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(baseUpdate,
                            Builders<Block>.Update
                                .Set(b => ((CardBlock)b).Icon, cardDto.Icon)
                                .Set(b => ((CardBlock)b).Title, cardDto.Title)
                                .Set(b => ((CardBlock)b).Description, SanitizeDictionary(cardDto.Description))
                                .Set(b => ((CardBlock)b).ImageUrl, cardDto.ImageUrl)
                                .Set(b => ((CardBlock)b).ButtonLabel, cardDto.ButtonLabel)
                                .Set(b => ((CardBlock)b).Href, CleanUrl(cardDto.Href))
                                .Set(b => ((CardBlock)b).Action, cardDto.Action)
                                .Set(b => ((CardBlock)b).FormDefinitionId, cardDto.FormDefinitionId)));
                    break;

                case (ButtonBlock _, ButtonBlockUpdateDto buttonDto):
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(baseUpdate,
                            Builders<Block>.Update
                                .Set(b => ((ButtonBlock)b).Label, buttonDto.Label)
                                .Set(b => ((ButtonBlock)b).Href, CleanUrl(buttonDto.Href))
                                .Set(b => ((ButtonBlock)b).Action, buttonDto.Action)
                                .Set(b => ((ButtonBlock)b).FormDefinitionId, buttonDto.FormDefinitionId)
                                .Set(b => ((ButtonBlock)b).Style, NormalizeButtonStyle(buttonDto.Style))));
                    break;

                case (MetricBlock _, MetricBlockUpdateDto metricDto):
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(baseUpdate,
                            Builders<Block>.Update
                                .Set(b => ((MetricBlock)b).Icon, metricDto.Icon)
                                .Set(b => ((MetricBlock)b).Label, metricDto.Label)
                                .Set(b => ((MetricBlock)b).Value, metricDto.Value)
                                .Set(b => ((MetricBlock)b).Prefix, metricDto.Prefix)
                                .Set(b => ((MetricBlock)b).Suffix, metricDto.Suffix)
                                .Set(b => ((MetricBlock)b).Description, SanitizeDictionary(metricDto.Description))));
                    break;

                case (BulletListBlock _, BulletListBlockUpdateDto listDto):
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(baseUpdate,
                            Builders<Block>.Update
                                .Set(b => ((BulletListBlock)b).Title, listDto.Title)
                                .Set(b => ((BulletListBlock)b).Items, listDto.Items.Select(MapBulletItem).ToList())));
                    break;

                case (StepBlock _, StepBlockUpdateDto stepDto):
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(baseUpdate,
                            Builders<Block>.Update
                                .Set(b => ((StepBlock)b).Icon, stepDto.Icon)
                                .Set(b => ((StepBlock)b).StepLabel, stepDto.StepLabel)
                                .Set(b => ((StepBlock)b).Title, stepDto.Title)
                                .Set(b => ((StepBlock)b).Description, SanitizeDictionary(stepDto.Description))));
                    break;

                case (IconBlock _, IconBlockUpdateDto iconDto):
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(baseUpdate,
                            Builders<Block>.Update
                                .Set(b => ((IconBlock)b).Icon, iconDto.Icon)
                                .Set(b => ((IconBlock)b).Label, iconDto.Label)
                                .Set(b => ((IconBlock)b).Description, SanitizeDictionary(iconDto.Description))));
                    break;

                case (ContainerBlock _, ContainerBlockUpdateDto containerDto):
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(baseUpdate,
                            Builders<Block>.Update
                                .Set(b => ((ContainerBlock)b).Title, SanitizeDictionary(containerDto.Title))
                                .Set(b => ((ContainerBlock)b).LayoutMode, NormalizeContainerLayout(containerDto.LayoutMode))
                                .Set(b => ((ContainerBlock)b).Columns, Math.Clamp(containerDto.Columns, 1, 6))
                                .Set(b => ((ContainerBlock)b).Gap, NormalizeBlockGap(containerDto.Gap))));
                    break;

                default:
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId, baseUpdate);
                    break;
            }

            await DeleteReplacedAssetAsync(existing, dto);

            return await GetByIdAsync(pageId, sectionId, blockId);
        }

        public async Task<Block?> UpdateLayoutAsync(string pageId, string sectionId,
            string blockId, BlockLayoutDto dto)
        {
            var existing = await GetByIdAsync(pageId, sectionId, blockId);
            if (existing is null) return null;

            await _context.BlocksDraft.UpdateOneAsync(
                b => b.Id == blockId,
                Builders<Block>.Update
                    .Set(b => b.Layout, MergeLayout(existing.Layout, dto))
                    .Set(b => b.UpdatedAt, DateTime.UtcNow)
                    .Inc(b => b.Version, 1));

            return await GetByIdAsync(pageId, sectionId, blockId);
        }


        private async Task DeleteReplacedAssetAsync(Block existing, BlockUpdateDto dto)
        {
            switch (existing, dto)
            {
                case (ImageBlock image, ImageBlockUpdateDto imageDto) when imageDto.ImageUrl != null:
                    await _r2Assets.DeleteIfUnusedAsync(image.ImageUrl, imageDto.ImageUrl);
                    break;
                case (FileBlock file, FileBlockUpdateDto fileDto) when fileDto.FileUrl != null:
                    await _r2Assets.DeleteIfUnusedAsync(file.FileUrl, fileDto.FileUrl);
                    break;
                case (CardBlock card, CardBlockUpdateDto cardDto) when cardDto.ImageUrl != null:
                    await _r2Assets.DeleteIfUnusedAsync(card.ImageUrl, cardDto.ImageUrl);
                    break;
            }
        }

        private static BulletListItem MapBulletItem(BulletListItemDto item) => new()
        {
            Id = string.IsNullOrWhiteSpace(item.Id) ? ObjectId.GenerateNewId().ToString() : item.Id,
            Icon = item.Icon,
            Text = SanitizeDictionary(item.Text),
            Visible = item.Visible,
            Order = item.Order
        };

        private static string NormalizeButtonStyle(string? style) => style switch
        {
            "outline" => "outline",
            "ghost" => "ghost",
            _ => "filled"
        };
        public async Task<bool> DeleteAsync(string pageId, string sectionId, string blockId)
        {
            var page = await _context.PagesDraft.Find(p => p.Id == pageId).FirstOrDefaultAsync();
            if (page is null) return false;
            var section = await _context.SectionsDraft.Find(s => s.Id == sectionId).FirstOrDefaultAsync();
            if (section is null) return false;
            var result = await _context.BlocksDraft.DeleteOneAsync(
                b => b.PageStableId == page.StableId &&
                     b.SectionStableId == section.StableId &&
                     b.Id == blockId);
            return result.DeletedCount > 0;
        }

        public async Task DeleteBySectionAsync(string pageStableId, string sectionStableId) =>
            await _context.BlocksDraft.DeleteManyAsync(
                b => b.PageStableId == pageStableId && b.SectionStableId == sectionStableId);

        public async Task DeleteByColumnSlotsAsync(string pageStableId, string sectionStableId, IEnumerable<string> slotIds)
        {
            var ids = slotIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
            if (ids.Count == 0) return;

            await _context.BlocksDraft.DeleteManyAsync(
                b => b.PageStableId == pageStableId &&
                     b.SectionStableId == sectionStableId &&
                     b.ColumnSlotId != null &&
                     ids.Contains(b.ColumnSlotId));
        }

        private static BlockLayout MapLayout(BlockLayoutDto? dto)
        {
            if (dto is null) return new BlockLayout();

            return new BlockLayout
            {
                Width = NormalizeChoice(dto.Width, new[] { "auto", "full", "half", "third", "custom" }, "auto"),
                ColumnSpan = Math.Clamp(dto.ColumnSpan ?? 12, 1, 12),
                Align = NormalizeChoice(dto.Align, new[] { "stretch", "start", "center", "end" }, "stretch"),
                Justify = NormalizeChoice(dto.Justify, new[] { "start", "center", "end" }, "start"),
                Padding = NormalizeChoice(dto.Padding, new[] { "none", "small", "medium", "large" }, "none"),
                Margin = NormalizeChoice(dto.Margin, new[] { "none", "small", "medium", "large" }, "none"),
                BackgroundColor = string.IsNullOrWhiteSpace(dto.BackgroundColor) ? null : dto.BackgroundColor,
                BorderRadius = NormalizeChoice(dto.BorderRadius, new[] { "none", "small", "medium", "large" }, "none"),
                ZIndex = Math.Clamp(dto.ZIndex ?? 1, 0, 20),
                X = Math.Clamp(dto.X ?? 0, 0, 11),
                Y = Math.Clamp(dto.Y ?? 0, 0, 60),
                W = Math.Clamp(dto.W ?? 4, 1, 12),
                H = Math.Clamp(dto.H ?? 2, 1, 40)
            };
        }

        private static BlockLayout MergeLayout(BlockLayout? current, BlockLayoutDto dto)
        {
            current ??= new BlockLayout();

            return new BlockLayout
            {
                Width = dto.Width is null
                    ? current.Width
                    : NormalizeChoice(dto.Width, new[] { "auto", "full", "half", "third", "custom" }, "auto"),
                ColumnSpan = Math.Clamp(dto.ColumnSpan ?? current.ColumnSpan, 1, 12),
                Align = dto.Align is null
                    ? current.Align
                    : NormalizeChoice(dto.Align, new[] { "stretch", "start", "center", "end" }, "stretch"),
                Justify = dto.Justify is null
                    ? current.Justify
                    : NormalizeChoice(dto.Justify, new[] { "start", "center", "end" }, "start"),
                Padding = dto.Padding is null
                    ? current.Padding
                    : NormalizeChoice(dto.Padding, new[] { "none", "small", "medium", "large" }, "none"),
                Margin = dto.Margin is null
                    ? current.Margin
                    : NormalizeChoice(dto.Margin, new[] { "none", "small", "medium", "large" }, "none"),
                BackgroundColor = dto.BackgroundColor ?? current.BackgroundColor,
                BorderRadius = dto.BorderRadius is null
                    ? current.BorderRadius
                    : NormalizeChoice(dto.BorderRadius, new[] { "none", "small", "medium", "large" }, "none"),
                ZIndex = Math.Clamp(dto.ZIndex ?? current.ZIndex, 0, 20),
                X = Math.Clamp(dto.X ?? current.X, 0, 11),
                Y = Math.Clamp(dto.Y ?? current.Y, 0, 60),
                W = Math.Clamp(dto.W ?? current.W, 1, 12),
                H = Math.Clamp(dto.H ?? current.H, 1, 40)
            };
        }

        private static string NormalizeChoice(string? value, IReadOnlyCollection<string> allowed, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            return allowed.Contains(value) ? value : fallback;
        }

        private static string NormalizeBlockZone(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "default" : value.Trim().ToLowerInvariant();

        private static string NormalizeContainerLayout(string? value) => value switch
        {
            "grid" => "grid",
            "split" => "split",
            _ => "stack"
        };

        private static string NormalizeBlockGap(string? value) => value switch
        {
            "none" => "none",
            "small" => "small",
            "large" => "large",
            _ => "medium"
        };

        public async Task<bool> SetVisibilityAsync(string pageId, string sectionId,
            string blockId, bool visible)
        {
            var page = await _context.PagesDraft.Find(p => p.Id == pageId).FirstOrDefaultAsync();
            if (page is null) return false;
            var section = await _context.SectionsDraft.Find(s => s.Id == sectionId).FirstOrDefaultAsync();
            if (section is null) return false;
            var result = await _context.BlocksDraft.UpdateOneAsync(
                b => b.PageStableId == page.StableId &&
                     b.SectionStableId == section.StableId &&
                     b.Id == blockId,
                Builders<Block>.Update
                    .Set(b => b.Visible, visible)
                    .Inc(b => b.Version, 1)
                    .Set(b => b.UpdatedAt, DateTime.UtcNow));
            return result.ModifiedCount > 0;
        }

        public async Task<bool> ReorderAsync(string pageId, string sectionId, List<string> orderedIds)
        {
            var page = await _context.PagesDraft.Find(p => p.Id == pageId).FirstOrDefaultAsync();
            if (page is null) return false;
            var section = await _context.SectionsDraft.Find(s => s.Id == sectionId).FirstOrDefaultAsync();
            if (section is null) return false;

            var writes = orderedIds.Select((id, i) =>
                new UpdateOneModel<Block>(
                    Builders<Block>.Filter.Where(b =>
                        b.PageStableId == page.StableId &&
                        b.SectionStableId == section.StableId &&
                        b.Id == id),
                    Builders<Block>.Update
                        .Set(b => b.Order, i)
                        .Inc(b => b.Version, 1))
            ).Cast<WriteModel<Block>>().ToList();

            if (writes.Count == 0) return true;
            await _context.BlocksDraft.BulkWriteAsync(writes);
            return true;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // PUBLIC USER SITE RENDER METHODS (PUBLISHED EXCLUSIVE)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public async Task<List<Block>> GetPublicByPageAsync(string pageStableId) =>
            await _context.BlocksPublished
                .Find(b => b.PageStableId == pageStableId && b.Visible == true)
                .SortBy(b => b.Order)
                .ToListAsync();

        public async Task<List<Block>> GetPublicBySectionAsync(string pageStableId, string sectionStableId) =>
            await _context.BlocksPublished
                .Find(b => b.PageStableId == pageStableId &&
                           b.SectionStableId == sectionStableId &&
                           b.Visible == true)
                .SortBy(b => b.Order)
                .ToListAsync();

        public async Task<Block?> GetPublicByIdAsync(string pageStableId, string blockId) =>
            await _context.BlocksPublished
                .Find(b => b.PageStableId == pageStableId && b.Id == blockId && b.Visible == true)
                .FirstOrDefaultAsync();

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // HELPERS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private static BlockButton MapButton(BlockButtonDto dto) => new()
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Label = dto.Label,
            Action = dto.Action,
            Href = CleanUrl(dto.Href),
            FormDefinitionId = dto.FormDefinitionId,
            Visible = dto.Visible,
            Order = dto.Order
        };

        private static string? CleanUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return url;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return url.StartsWith("/", StringComparison.Ordinal) && !url.StartsWith("//", StringComparison.Ordinal)
                    || url.StartsWith("#", StringComparison.Ordinal)
                    ? url
                    : null;
            return uri.Scheme is "http" or "https" ? url : null;
        }

        private static string? CleanVideoUrl(string? url)
        {
            var cleaned = CleanUrl(url);
            if (string.IsNullOrWhiteSpace(cleaned)) return cleaned;
            if (!Uri.TryCreate(cleaned, UriKind.Absolute, out var uri)) return cleaned;
            if (uri.Scheme is not ("http" or "https")) return null;

            var host = uri.Host.ToLowerInvariant();
            string? videoId = null;

            if (host is "youtu.be")
            {
                videoId = uri.AbsolutePath.Trim('/').Split('/').FirstOrDefault();
            }
            else if (host.EndsWith("youtube.com", StringComparison.Ordinal))
            {
                if (uri.AbsolutePath.StartsWith("/embed/", StringComparison.OrdinalIgnoreCase))
                {
                    videoId = uri.AbsolutePath["/embed/".Length..].Split('/').FirstOrDefault();
                }
                else if (uri.AbsolutePath.StartsWith("/shorts/", StringComparison.OrdinalIgnoreCase))
                {
                    videoId = uri.AbsolutePath["/shorts/".Length..].Split('/').FirstOrDefault();
                }
                else
                {
                    videoId = ReadQueryValue(uri.Query, "v");
                }
            }

            return string.IsNullOrWhiteSpace(videoId)
                ? cleaned
                : $"https://www.youtube.com/embed/{videoId}";
        }

        private static string? ReadQueryValue(string query, string key)
        {
            foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split('=', 2);
                if (pair.Length == 2 &&
                    Uri.UnescapeDataString(pair[0]).Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(pair[1]);
                }
            }

            return null;
        }
    }
}


