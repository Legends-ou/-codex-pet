namespace PetDesktop.Core.Animation;

public static class LookDirectionQuantizer
{
    public static int? Quantize(double horizontalDelta, double verticalDelta, double deadZone, double maxRadius)
    {
        if (double.IsNaN(horizontalDelta) || double.IsNaN(verticalDelta) ||
            double.IsInfinity(horizontalDelta) || double.IsInfinity(verticalDelta))
        {
            return null;
        }

        if (deadZone < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deadZone), deadZone, "Dead zone must be non-negative.");
        }

        if (maxRadius <= deadZone)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRadius), maxRadius, "Maximum radius must be greater than the dead zone.");
        }

        var distance = Math.Sqrt((horizontalDelta * horizontalDelta) + (verticalDelta * verticalDelta));
        if (distance <= deadZone || distance > maxRadius)
        {
            return null;
        }

        var degreesClockwiseFromNorth = Math.Atan2(horizontalDelta, -verticalDelta) * (180d / Math.PI);
        if (degreesClockwiseFromNorth < 0)
        {
            degreesClockwiseFromNorth += 360d;
        }

        return (int)Math.Floor((degreesClockwiseFromNorth + 11.25d) / 22.5d) % 16;
    }
}
