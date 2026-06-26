namespace FullProject.Services.CloneServices
{
    public enum CloneProfile
    {
        /// <summary>
        /// Draft page graph is copied into the published collections.
        /// Stable identity is preserved, source document ids are recorded, and publish metadata is set.
        /// </summary>
        PublishSnapshot,

        /// <summary>
        /// Published page graph is copied back into draft collections.
        /// Stable identity is preserved, while draft documents remain editable.
        /// </summary>
        DraftResetSnapshot,

        /// <summary>
        /// Existing content is copied as a separate editable item.
        /// Stable identity must be regenerated and publish metadata cleared.
        /// </summary>
        DuplicateAsNewContent,

        /// <summary>
        /// Draft canvas block data is captured into a reusable preset definition.
        /// The preset should not depend on the source page graph.
        /// </summary>
        PresetCapture,

        /// <summary>
        /// A reusable preset is inserted into a target page graph.
        /// Stable identity and parent relationships must be regenerated or remapped.
        /// </summary>
        PresetApply
    }
}
