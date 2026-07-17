using Contracts.Forms;

namespace FullProject.Settings;

public sealed class FormDesignV2RuntimeSettings
{
    public FormDesignCompatibilityMode Mode { get; set; } = FormDesignCompatibilityMode.V1Only;
    public bool EnableMigrationApply { get; set; }
    public bool EnableOrderWrites { get; set; }

    public bool CanReadV2 => Mode is FormDesignCompatibilityMode.DualReadV1Write or
        FormDesignCompatibilityMode.DualReadV2Write;

    // Migration apply is a short-lived operational gate. Authoring remains enabled
    // after that gate is closed once the runtime has moved to DualReadV2Write.
    public bool CanWriteV2 => Mode == FormDesignCompatibilityMode.DualReadV2Write;
}
