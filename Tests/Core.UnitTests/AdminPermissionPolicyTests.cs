using Contracts.Auth;

namespace Core.UnitTests;

public sealed class AdminPermissionPolicyTests
{
    [Fact]
    public void ExpandDependencies_AddsRequiredContentPermission()
    {
        var permissions = AdminPermissionKeys.ExpandDependencies([AdminPermissionKeys.CreateEditContent]);

        Assert.Contains(AdminPermissionKeys.CreateEditContent, permissions);
        Assert.Contains(AdminPermissionKeys.ViewContent, permissions);
    }

    [Fact]
    public void ExpandDependencies_AddsRequiredFormDefinitionPermission()
    {
        var permissions = AdminPermissionKeys.ExpandDependencies([AdminPermissionKeys.EditFormDefinitions]);

        Assert.Contains(AdminPermissionKeys.EditFormDefinitions, permissions);
        Assert.Contains(AdminPermissionKeys.ViewFormDefinitions, permissions);
    }

    [Fact]
    public void ExpandDependencies_DropsUnknownPermissionsAndDuplicates()
    {
        var permissions = AdminPermissionKeys.ExpandDependencies(
            [AdminPermissionKeys.ViewContent, AdminPermissionKeys.ViewContent, "unknown"]);

        Assert.Single(permissions);
        Assert.Equal(AdminPermissionKeys.ViewContent, permissions[0]);
    }

    [Theory]
    [InlineData(AdminPermissionKeys.CreateEditContent, AdminPermissionKeys.ViewContent, true)]
    [InlineData(AdminPermissionKeys.ViewContent, AdminPermissionKeys.CreateEditContent, false)]
    [InlineData(AdminPermissionKeys.EditFormDefinitions, AdminPermissionKeys.ViewFormDefinitions, true)]
    public void PermissionImplies_RespectsDependencyDirection(string granted, string requested, bool expected) =>
        Assert.Equal(expected, AdminPermissionKeys.PermissionImplies(granted, requested));
}
