namespace Contracts.Common;

public class VisibilityRequest
{
    public bool Visible { get; set; }
}

public class ReorderRequest
{
    public List<string> OrderedIds { get; set; } = new();
}


public class PublishResultDto
{
    public string PageId { get; set; } = string.Empty;
    public string StableId { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime PublishedAt { get; set; }
    public int SectionsPublished { get; set; }
    public int BlocksPublished { get; set; }
}


public class ResetResultDto
{
    public string PageId { get; set; } = string.Empty;
    public string StableId { get; set; } = string.Empty;
    public int SectionsRestored { get; set; }
    public int BlocksRestored { get; set; }

}