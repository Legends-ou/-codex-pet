using PetDesktop.Core.Configuration;

namespace PetDesktop.App.Menus;

public enum DistributionKind { Installed, Portable }
public enum PetCommand { OpenManagementCenter, NewNote, ChangePet, SizeSmall, SizeStandard, SizeLarge, AlwaysOnTop, StartWithWindows, ToggleTheme, Rescan, OpenPetsFolder, Exit }
public sealed record PetCommandItem(PetCommand Command, bool IsVisible, bool IsChecked = false);
public sealed record PetCommandState(bool HasPets, PetSize Size, bool AlwaysOnTop, bool StartWithWindows)
{
    public static PetCommandState Default { get; } = new(false, PetSize.Standard, true, false);
}
public sealed class PetCommandModel
{
    private readonly IReadOnlyList<PetCommandItem> _items;
    private PetCommandModel(IReadOnlyList<PetCommandItem> items) => _items = items;
    public IReadOnlyList<PetCommandItem> Items => _items;
    public PetCommandItem? Find(PetCommand command) => _items.FirstOrDefault(item => item.Command == command);
    public static PetCommandModel Build(DistributionKind kind, PetCommandState state) => new(
    [
        new(PetCommand.OpenManagementCenter, true),
        new(PetCommand.NewNote, true),
        new(PetCommand.ChangePet, state.HasPets),
        new(PetCommand.SizeSmall, true, state.Size == PetSize.Small),
        new(PetCommand.SizeStandard, true, state.Size == PetSize.Standard),
        new(PetCommand.SizeLarge, true, state.Size == PetSize.Large),
        new(PetCommand.AlwaysOnTop, true, state.AlwaysOnTop),
        // Both delivery forms are full applications. A portable copy registers its own
        // executable path, so it can opt into Windows startup just like the installer.
        new(PetCommand.StartWithWindows, true, state.StartWithWindows),
        new(PetCommand.ToggleTheme, true),
        new(PetCommand.Rescan, true),
        new(PetCommand.OpenPetsFolder, true),
        new(PetCommand.Exit, true),
    ]);
}
