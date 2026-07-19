using PetDesktop.App;
using System.Reflection;
using System.Windows.Controls;

namespace PetDesktop.App.Tests;

public sealed class NoteEditorDialogTests
{
    [Fact]
    public void ConstructorLoadsTheReminderControlsWithoutThrowing()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                _ = new NoteEditorDialog(PetDesktop.Core.Configuration.AppTheme.Dark);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }

    [Fact]
    public void SelectingWeeklyRepeatBuildsDayButtonsWithoutThrowing()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var dialog = new NoteEditorDialog(PetDesktop.Core.Configuration.AppTheme.Dark);
                var repeatInput = (ComboBox)typeof(NoteEditorDialog)
                    .GetField("RepeatInput", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(dialog)!;
                var daysPanel = (StackPanel)typeof(NoteEditorDialog)
                    .GetField("RepeatDaysPanel", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(dialog)!;

                repeatInput.SelectedIndex = 2;

                Assert.Equal(2, daysPanel.Children.Count);
                var buttons = ((WrapPanel)daysPanel.Children[1]).Children.OfType<Button>().ToArray();
                Assert.Equal(7, buttons.Length);
                Assert.All(buttons, button => Assert.NotNull(button.Template));
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }
}
