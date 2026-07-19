using PetDesktop.App.Menus;

namespace PetDesktop.App.Tests;
public sealed class PetCommandModelTests
{
    [Theory]
    [InlineData(DistributionKind.Installed)]
    [InlineData(DistributionKind.Portable)]
    public void BuildShowsStartupForEveryFullApplicationBuild(DistributionKind kind)
    {
        var menu = PetCommandModel.Build(kind, PetCommandState.Default);
        Assert.True(menu.Find(PetCommand.StartWithWindows)!.IsVisible);
        Assert.True(menu.Find(PetCommand.NewNote)!.IsVisible);
    }
}
