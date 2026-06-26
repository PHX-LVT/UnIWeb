namespace FullProject.Services
{
    public sealed record ContentTypeWorkflow(
        bool RequiresBody,
        bool RequiresHeroImage,
        bool RequiresFile,
        bool RequiresVideoUrl,
        bool AllowsAttachments,
        string ClickBehavior);
}
