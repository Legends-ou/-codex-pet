using System.Windows;

namespace PetDesktop.App.Imaging;

public static class MaskRegionBuilder
{
    public static IReadOnlyList<Int32Rect> BuildRuns(AlphaHitTestMask mask)
    {
        ArgumentNullException.ThrowIfNull(mask);

        var runs = new List<Int32Rect>();

        for (var y = 0; y < mask.Height; y++)
        {
            var x = 0;
            while (x < mask.Width)
            {
                while (x < mask.Width && !mask.IsHit(x, y))
                {
                    x++;
                }

                var start = x;
                while (x < mask.Width && mask.IsHit(x, y))
                {
                    x++;
                }

                if (start < x)
                {
                    runs.Add(new Int32Rect(start, y, x - start, 1));
                }
            }
        }

        return runs;
    }
}
