using PetDesktop.Core.Pets;

namespace PetDesktop.Core.Animation;

public enum ResourceAnimationPhase
{
    None,
    Loading,
    Validating,
    Failed,
}

public sealed class AnimationStateMachine
{
    private readonly PetLayout _layout;
    private readonly Random _random;
    private PetAction? _oneShot;
    private int _dragDirection;
    private ResourceAnimationPhase _resourcePhase;
    private bool _menuOpen;
    private int? _lookSector;
    private AnimationClip _currentClip;
    private int _frameIndex;
    private TimeSpan _frameElapsed;
    private TimeSpan _idleElapsed;
    private TimeSpan _nextAmbientActionDelay;

    public AnimationStateMachine(PetLayout layout, Random? random = null)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _random = random ?? Random.Shared;
        _currentClip = AnimationClip.Standard(_layout, PetAction.Idle, loops: true);
        _nextAmbientActionDelay = CreateNextAmbientActionDelay();
    }

    public PetAction? CurrentAction => _currentClip.Action;

    public int? CurrentLookSector => _currentClip.LookSector;

    public int CurrentFrameIndex => _frameIndex;

    public TimeSpan NextAmbientActionDelay => _nextAmbientActionDelay;

    public void SetDragging(int horizontalDelta)
    {
        _dragDirection = Math.Sign(horizontalDelta);
        ResolveCurrentClip();
    }

    public void SetResourcePhase(ResourceAnimationPhase phase)
    {
        if (!Enum.IsDefined(phase))
        {
            throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown resource animation phase.");
        }

        _resourcePhase = phase;
        ResolveCurrentClip();
    }

    public void SetMenuOpen(bool isOpen)
    {
        _menuOpen = isOpen;
        ResolveCurrentClip();
    }

    public void SetLookSector(int? sector)
    {
        if (sector is < 0 or > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(sector), sector, "Look sector must be from 0 through 15.");
        }

        _lookSector = _layout.HasLookDirections ? sector : null;
        ResolveCurrentClip();
    }

    public void RequestOneShot(PetAction action)
    {
        if (action is not (PetAction.Waving or PetAction.Jumping))
        {
            throw new ArgumentOutOfRangeException(nameof(action), action, "Only lightweight interactive actions can be requested as one shots.");
        }

        _oneShot = action;
        ResolveCurrentClip();
    }

    public void Tick(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed), elapsed, "Elapsed time must be non-negative.");
        }

        if (elapsed == TimeSpan.Zero)
        {
            return;
        }

        if (IsAmbientEligible())
        {
            _idleElapsed += elapsed;
            if (_idleElapsed >= _nextAmbientActionDelay)
            {
                _idleElapsed = TimeSpan.Zero;
                _nextAmbientActionDelay = CreateNextAmbientActionDelay();
                _oneShot = _random.Next(0, 2) == 0 ? PetAction.Waving : PetAction.Jumping;
                ResolveCurrentClip();
                return;
            }
        }
        else
        {
            _idleElapsed = TimeSpan.Zero;
        }

        if (_currentClip.LookSector is not null)
        {
            return;
        }

        _frameElapsed += elapsed;
        while (_frameElapsed >= TimeSpan.FromMilliseconds(_currentClip.FrameDurationsMs[_frameIndex]))
        {
            _frameElapsed -= TimeSpan.FromMilliseconds(_currentClip.FrameDurationsMs[_frameIndex]);
            _frameIndex++;
            if (_frameIndex < _currentClip.FrameDurationsMs.Count)
            {
                continue;
            }

            if (_currentClip.Loops)
            {
                _frameIndex = 0;
                continue;
            }

            if (_currentClip.Action == PetAction.Failed && _resourcePhase == ResourceAnimationPhase.Failed)
            {
                _resourcePhase = ResourceAnimationPhase.None;
            }
            else
            {
                _oneShot = null;
            }

            ResolveCurrentClip();
            return;
        }
    }

    private void ResolveCurrentClip()
    {
        var next = ResolveClip();
        if (next.Action == _currentClip.Action && next.LookSector == _currentClip.LookSector)
        {
            return;
        }

        _currentClip = next;
        _frameIndex = 0;
        _frameElapsed = TimeSpan.Zero;
    }

    private AnimationClip ResolveClip()
    {
        if (_dragDirection != 0)
        {
            return AnimationClip.Standard(
                _layout,
                _dragDirection > 0 ? PetAction.RunningRight : PetAction.RunningLeft,
                loops: true);
        }

        PetAction? resourceAction = _resourcePhase switch
        {
            ResourceAnimationPhase.Loading => PetAction.Running,
            ResourceAnimationPhase.Validating => PetAction.Review,
            ResourceAnimationPhase.Failed => PetAction.Failed,
            ResourceAnimationPhase.None => null,
            _ => throw new InvalidOperationException("Unknown resource animation phase."),
        };
        if (resourceAction is not null)
        {
            return AnimationClip.Standard(
                _layout,
                resourceAction.Value,
                loops: _resourcePhase != ResourceAnimationPhase.Failed);
        }

        if (_menuOpen)
        {
            return AnimationClip.Standard(_layout, PetAction.Waiting, loops: true);
        }

        if (_oneShot is not null)
        {
            return AnimationClip.Standard(_layout, _oneShot.Value, loops: false);
        }

        if (_layout.HasLookDirections && _lookSector is not null)
        {
            return AnimationClip.Look(_lookSector.Value);
        }

        return AnimationClip.Standard(_layout, PetAction.Idle, loops: true);
    }

    private bool IsAmbientEligible() =>
        _dragDirection == 0 &&
        _resourcePhase == ResourceAnimationPhase.None &&
        !_menuOpen &&
        _oneShot is null &&
        _lookSector is null;

    private TimeSpan CreateNextAmbientActionDelay() =>
        TimeSpan.FromMilliseconds(_random.Next(30_000, 90_001));
}
