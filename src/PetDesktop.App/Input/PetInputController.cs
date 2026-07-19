namespace PetDesktop.App.Input;

public readonly record struct PetPointerPoint(int X, int Y);

public sealed record PetInputResult(bool StartDrag, bool MoveDrag, bool CompletedDrag, bool RequestWave, bool OpenMenu, int HorizontalDelta)
{
    public static PetInputResult None { get; } = new(false, false, false, false, false, 0);
}

public sealed class PetInputController
{
    private readonly PetPointerPoint _dragThreshold;
    private PetPointerPoint? _pointerDown;
    private PetPointerPoint? _lastPoint;
    private bool _dragging;

    public PetInputController(PetPointerPoint dragThreshold)
    {
        if (dragThreshold.X < 0 || dragThreshold.Y < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dragThreshold), "Drag threshold must be non-negative.");
        }

        _dragThreshold = dragThreshold;
    }

    public void PointerDown(PetPointerPoint point)
    {
        _pointerDown = point;
        _lastPoint = point;
        _dragging = false;
    }

    public PetInputResult PointerMove(PetPointerPoint point)
    {
        if (_pointerDown is null || _lastPoint is null)
        {
            return PetInputResult.None;
        }

        var horizontalDelta = point.X - _lastPoint.Value.X;
        _lastPoint = point;
        if (!_dragging && !ExceedsThreshold(_pointerDown.Value, point))
        {
            return PetInputResult.None;
        }

        var startsDrag = !_dragging;
        _dragging = true;
        return new PetInputResult(startsDrag, true, false, false, false, horizontalDelta);
    }

    public PetInputResult PointerUp(PetPointerPoint point)
    {
        if (_pointerDown is null)
        {
            return PetInputResult.None;
        }

        var completedDrag = _dragging;
        _pointerDown = null;
        _lastPoint = null;
        _dragging = false;
        return new PetInputResult(false, false, completedDrag, !completedDrag, false, 0);
    }

    public PetInputResult RightClick()
    {
        _pointerDown = null;
        _lastPoint = null;
        _dragging = false;
        return new PetInputResult(false, false, false, false, true, 0);
    }

    private bool ExceedsThreshold(PetPointerPoint start, PetPointerPoint current) =>
        Math.Abs(current.X - start.X) > _dragThreshold.X || Math.Abs(current.Y - start.Y) > _dragThreshold.Y;
}
