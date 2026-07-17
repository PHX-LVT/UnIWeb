using Contracts.Forms;
using Contracts.Public;
using FullProject.Models;
using FullProject.Services.FormServices;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

var definition = new FormDefinition
{
    Id = "64a000000000000000000001",
    Key = "contact",
    Name = new() { ["en"] = "Contact" },
    SubmitButtonLabel = new() { ["en"] = "Send" },
    Design = new FormDesignSettings
    {
        SchemaVersion = FormDesignPolicy.CurrentSchemaVersion,
        Shape = FormDesignShape.TwoColumns,
        WidthPx = 840,
        FieldGapPx = 16
    },
    Fields =
    [
        new FormDefinitionField { Key = "name", Type = "text", Order = 0, Label = new() { ["en"] = "Name" } },
        new FormDefinitionField { Key = "email", Type = "email", Order = 1, Label = new() { ["en"] = "Email" } },
        new FormDefinitionField { Key = "message", Type = "textarea", Order = 2, Label = new() { ["en"] = "Message" } }
    ]
};

var before = definition.ToBsonDocument();
Assert(!before.Contains("InformationItems"), "A v1 definition must not serialize an empty v2 information collection.");
Assert(!before.Contains("AuxiliaryActions"), "A v1 definition must not serialize an empty v2 action collection.");
Assert(!before["Design"].AsBsonDocument.Contains("V2"), "A v1 design must not serialize a null v2 payload.");

var dryRun = FormDesignV2MigrationPlanner.CreateDryRun(new[] { definition });
Assert(dryRun.DefinitionCount == 1 && dryRun.ConvertibleCount == 1, "Dry-run must classify a valid v1 definition as convertible.");
Assert(dryRun.Items[0].ProjectedLayout == FormOuterLayout.SplitPanel, "Dry-run must use the deterministic v1 projection.");
Assert(definition.ToBsonDocument().Equals(before), "Dry-run must not mutate its source definition.");

var submission = new FormSubmission
{
    Id = "64a000000000000000000777",
    FormId = definition.Id,
    FormKey = definition.Key,
    Data = new() { ["name"] = "Original submitter", ["email"] = "original@example.com" }
};
var submissionBefore = submission.ToBsonDocument();
_ = FormDesignV2MigrationPlanner.CreateDryRun(new[] { definition });
Assert(submission.ToBsonDocument().Equals(submissionBefore), "Migration planning must not mutate submission snapshots.");

var response = FormDefinitionService.MapPublic(definition);
Assert(response.Design.UseThemeDefaults, "An untouched legacy appearance without an inheritance marker must migrate to Theme defaults.");
Assert(response.Design.SchemaVersion == FormDesignPolicy.CurrentSchemaVersion, "API projection must retain the stored schema version.");
Assert(response.Design.V2?.OuterLayout == FormOuterLayout.SplitPanel, "API reads must include the v2 projection.");
Assert(response.Design.V2?.FieldRows.SelectMany(row => row.FieldKeys).SequenceEqual(new[] { "name", "email", "message" }) == true,
    "API projection must retain every field exactly once.");

definition.AuxiliaryActions =
[
    new FormAuxiliaryAction
    {
        Id = "action-download",
        Label = new() { ["en"] = "Download" },
        Target = new FormActionTarget
        {
            Type = FormActionTargetType.ManagedResource,
            ResourceId = "64a000000000000000000099"
        }
    }
];
var responseWithResource = FormDefinitionService.MapPublic(
    definition,
    managedResourceUrls: new Dictionary<string, string>
    {
        ["64a000000000000000000099"] = "/api/public/resources/guide"
    });
Assert(responseWithResource.AuxiliaryActions[0].Target.ResolvedHref == "/api/public/resources/guide",
    "Managed-resource action URLs must be resolved server-side for readers.");
var v1OnlyResponse = FormDefinitionService.MapPublic(
    definition,
    managedResourceUrls: new Dictionary<string, string>
    {
        ["64a000000000000000000099"] = "/api/public/resources/guide"
    },
    readStoredV2: false);
Assert(v1OnlyResponse.AuxiliaryActions.Count == 0,
    "V1Only readers must not expose stored v2-only auxiliary actions during rollback.");
definition.AuxiliaryActions.Clear();

var submittedV2 = response.Design;
var persisted = FormDefinitionService.MapDesign(submittedV2);
Assert(persisted.SchemaVersion == FormDesignPolicy.CurrentSchemaVersion, "Ordinary writes must remain schema v1.");
Assert(persisted.V2 is null, "Ordinary writes must strip the read-only v2 projection.");
Assert(!persisted.ToBsonDocument().Contains("V2"), "The stripped v2 projection must not enter BSON.");

var normalizedV2Write = FormDefinitionService.NormalizeV2WriteDesign(
    definition.Id,
    submittedV2,
    response.Fields,
    definition.Name,
    definition.Introduction,
    definition.SubmitButtonLabel);
var persistedV2 = FormDefinitionService.MapDesignV2Write(normalizedV2Write);
Assert(persistedV2.SchemaVersion == FormDesignV2Policy.TargetSchemaVersion,
    "Activated v2 writes must persist schema version 2.");
Assert(persistedV2.V2 is not null,
    "Activated v2 writes must persist the governed v2 payload.");
Assert(persistedV2.V2!.FieldRows.SelectMany(row => row.FieldKeys)
        .SequenceEqual(new[] { "name", "email", "message" }),
    "Activated v2 writes must preserve every normalized field-row key exactly once.");
Assert(persistedV2.ToBsonDocument()["SchemaVersion"].AsInt32 == FormDesignV2Policy.TargetSchemaVersion,
    "Activated v2 writes must serialize the schema-v2 marker.");
Assert(persistedV2.ToBsonDocument().Contains("V2"),
    "Activated v2 writes must serialize the v2 payload.");

var customAppearance = new FormDesignSettings { AccentColor = "#ff0000" };
Assert(!FormDefinitionService.MapDesign(customAppearance).UseThemeDefaults,
    "A customized legacy appearance must remain custom during inheritance migration.");
var explicitCustomAppearance = new FormDesignSettings { UseThemeDefaults = false };
Assert(!FormDefinitionService.MapDesign(explicitCustomAppearance).UseThemeDefaults,
    "An explicit custom choice must not be reclassified as Theme inheritance.");

var second = new FormDefinition { Id = "64a000000000000000000000", Key = "about" };
var order = FormDefinitionOrderService.ReconcileForRead(
    new[] { definition.Id, definition.Id, "missing" },
    new[] { definition, second });
Assert(order.SequenceEqual(new[] { definition.Id, second.Id }), "Order reads must remove stale/duplicate IDs and append missing definitions by Key.");

var formBlock = new FormBlock
{
    Id = "64a000000000000000000444",
    FormDefinitionId = definition.Id,
    DesignSchemaVersion = FormDesignPolicy.CurrentSchemaVersion,
    FormScale = .5d,
    DefaultWidthPx = 840,
    DefaultWidthPercent = 60d,
    DefaultHeightPx = 520d
};
var formBlockDocument = formBlock.ToBsonDocument();
Assert(formBlockDocument.Contains("FormDefinitionId"), "A FormBlock must persist its governed Form Definition reference.");
Assert(!formBlockDocument.Contains("Fields") && !formBlockDocument.Contains("Design") && !formBlockDocument.Contains("SubmitButtonLabel"),
    "A FormBlock must not copy definition content or design payloads.");
Assert(FormBlockLayoutPolicy.MinimumScale == .5d && FormBlockLayoutPolicy.MaximumScale == 1d,
    "The FormBlock proportional scale contract must remain 50-100 percent.");
var defaultSize = FormBlockLayoutPolicy.CalculateDefaultSize(response.Design, FormBlockLayoutPolicy.NormalContentWidthPx);
Assert(defaultSize.WidthPercent is > 0d and <= 100d && defaultSize.HeightPx >= FormDesignPolicy.MinimumHeightPx,
    "FormBlock geometry must derive from the referenced definition's intrinsic design.");

var publicContractNames = typeof(PublicFormBlockDto).GetProperties().Select(property => property.Name)
    .Concat(typeof(FormDefinitionResponse).GetProperties().Select(property => property.Name))
    .ToHashSet(StringComparer.OrdinalIgnoreCase);
Assert(!publicContractNames.Contains("Mode") &&
       !publicContractNames.Contains("EnableMigrationApply") &&
       !publicContractNames.Contains("EnableOrderWrites"),
    "Public Form contracts must not expose admin rollout configuration.");

Console.WriteLine("Form Design v2 API compatibility coverage passed.");
