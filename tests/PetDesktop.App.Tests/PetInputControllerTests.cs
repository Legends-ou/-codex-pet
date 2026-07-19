using PetDesktop.App.Input;

namespace PetDesktop.App.Tests;

public sealed class PetInputControllerTests
{
    [Fact]
    public void PointerUpAfterDragDoesNotRequestWave()
    {
        var input = new PetInputController(new(4, 4));
        input.PointerDown(new(10, 10));
        var move = input.PointerMove(new(20, 10));
        var result = input.PointerUp(new(20, 10));

        Assert.True(move.StartDrag);
        Assert.True(result.CompletedDrag);
        Assert.False(result.RequestWave);
    }

    [Fact]
    public void PointerUpAfterSmallMovementRequestsWave()
    {
        var input = new PetInputController(new(4, 4));
        input.PointerDown(new(10, 10));
        input.PointerMove(new(14, 13));

        var result = input.PointerUp(new(14, 13));

        Assert.True(result.RequestWave);
        Assert.False(result.CompletedDrag);
    }

    [Fact]
    public void RightClickOpensMenuWithoutWave()
    {
        var result = new PetInputController(new(4, 4)).RightClick();

        Assert.True(result.OpenMenu);
        Assert.False(result.RequestWave);
    }

    [Fact]
    public void VerticalDragDoesNotPretendToBeHorizontalMovement()
    {
        var input = new PetInputController(new(4, 4));
        input.PointerDown(new(10, 10));

        var move = input.PointerMove(new(10, 20));

        Assert.True(move.MoveDrag);
        Assert.Equal(0, move.HorizontalDelta);
    }
}
