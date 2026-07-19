using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using PetDesktop.App.Input;
using PetDesktop.App.Imaging;

namespace PetDesktop.App.Windows;

public sealed class LayeredPetWindow : IDisposable
{
    private readonly HwndSource _source;
    private readonly PetInputController _input = new(new(4, 4));
    private bool _disposed;
    private bool _hasCapture;
    private int _dragOffsetX;
    private int _dragOffsetY;
    private PetPointerPoint? _lastReportedPosition;
    private bool _isVisible;

    public LayeredPetWindow(int initialWidth, int initialHeight)
    {
        ValidateDimensions(initialWidth, initialHeight);

        var previousDpiContext = NativeMethods.SetThreadDpiAwarenessContext(
            NativeMethods.DpiAwarenessContextPerMonitorAwareV2);
        if (previousDpiContext == nint.Zero)
        {
            throw new InvalidOperationException(
                "SetThreadDpiAwarenessContext returned NULL while entering per-monitor-v2 awareness.");
        }

        try
        {
            var parameters = new HwndSourceParameters("Pet Desktop layered pet")
            {
                Width = initialWidth,
                Height = initialHeight,
                WindowStyle = NativeMethods.WsPopup,
                ExtendedWindowStyle = NativeMethods.WsExLayered
                    | NativeMethods.WsExToolWindow
                    | NativeMethods.WsExTopmost
                    | NativeMethods.WsExNoActivate,
                UsesPerPixelOpacity = true,
                // Keep the native HWND off-screen until the first complete
                // layered frame has been committed. This prevents a transient
                // top-left flash on slower compositors during startup.
                PositionX = -32000,
                PositionY = -32000,
            };

            _source = new HwndSource(parameters);
            _source.AddHook(WindowProcedure);
        }
        finally
        {
            _ = NativeMethods.SetThreadDpiAwarenessContext(previousDpiContext);
        }
    }

    public nint Handle => _source.Handle;

    public PetPointerPoint Position
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!NativeMethods.GetWindowRect(Handle, out var rectangle))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not retrieve the pet window position.");
            }

            return new PetPointerPoint(rectangle.Left, rectangle.Top);
        }
    }

    public event Action<PetInputResult>? Input;
    public event Action<PetPointerPoint>? PositionChanged;

    public void SetAlwaysOnTop(bool enabled)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!NativeMethods.SetWindowPos(
                Handle,
                enabled ? NativeMethods.HwndTopmost : NativeMethods.HwndNotopmost,
                0,
                0,
                0,
                0,
                NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not update the pet window z-order.");
        }
    }

    public void UpdateFrame(
        int screenX,
        int screenY,
        int width,
        int height,
        byte[] premultipliedBgra,
        bool revealWindow = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _source.Dispatcher.VerifyAccess();
        ArgumentNullException.ThrowIfNull(premultipliedBgra);
        ValidateDimensions(width, height);

        var pixelCount = checked((long)width * height);
        var byteCount = checked(pixelCount * 4);
        if (pixelCount > Array.MaxLength
            || byteCount > Array.MaxLength
            || premultipliedBgra.LongLength != byteCount)
        {
            throw new ArgumentException(
                "Frame data length must equal width multiplied by height multiplied by four.",
                nameof(premultipliedBgra));
        }

        var alpha = ExtractAndValidateAlpha(premultipliedBgra, checked((int)pixelCount));
        var mask = new AlphaHitTestMask(width, height, alpha);
        var runs = MaskRegionBuilder.BuildRuns(mask);

        var uploadResult = UpdateLayeredBitmap(screenX, screenY, width, height, premultipliedBgra);

        LayeredFrameCommitCoordinator.CompleteAfterSuccessfulUpload(
            uploadResult,
            () => ApplyWindowRegion(runs),
            HideAfterFailedFrame);

        if (revealWindow)
        {
            RevealPreparedFrame();
        }

        ReportPositionIfChanged(new PetPointerPoint(screenX, screenY));
    }

    public void RevealPreparedFrame()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _source.Dispatcher.VerifyAccess();
        if (_isVisible)
        {
            return;
        }

        _ = NativeMethods.ShowWindow(Handle, NativeMethods.SwShowNoActivate);
        _isVisible = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _source.Dispatcher.VerifyAccess();
        ReleaseDragCapture();
        _source.RemoveHook(WindowProcedure);
        _source.Dispose();
        _disposed = true;
    }

    private static void ValidateDimensions(int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be positive.");
        }
    }

    private static byte[] ExtractAndValidateAlpha(byte[] pixels, int pixelCount)
    {
        var alpha = new byte[pixelCount];
        for (var pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
        {
            var byteIndex = pixelIndex * 4;
            var pixelAlpha = pixels[byteIndex + 3];

            if (pixels[byteIndex] > pixelAlpha
                || pixels[byteIndex + 1] > pixelAlpha
                || pixels[byteIndex + 2] > pixelAlpha)
            {
                throw new ArgumentException(
                    "Every BGRA color channel must be premultiplied by its alpha value.",
                    nameof(pixels));
            }

            alpha[pixelIndex] = pixelAlpha;
        }

        return alpha;
    }

    private LayeredBitmapUploadResult UpdateLayeredBitmap(
        int screenX,
        int screenY,
        int width,
        int height,
        byte[] pixels)
    {
        var screenDc = NativeMethods.GetDC(nint.Zero);
        if (screenDc == nint.Zero)
        {
            throw new InvalidOperationException("GetDC returned NULL for the desktop window.");
        }

        nint memoryDc = nint.Zero;
        nint bitmap = nint.Zero;
        nint previousBitmap = nint.Zero;
        Exception? operationException = null;
        string? cleanupFailure = null;

        try
        {
            memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
            if (memoryDc == nint.Zero)
            {
                throw new InvalidOperationException("CreateCompatibleDC returned NULL.");
            }

            var bitmapInfo = new NativeMethods.BitmapInfo
            {
                Header = new NativeMethods.BitmapInfoHeader
                {
                    Size = checked((uint)Marshal.SizeOf<NativeMethods.BitmapInfoHeader>()),
                    Width = width,
                    Height = -height,
                    Planes = 1,
                    BitCount = 32,
                    Compression = NativeMethods.BiRgb,
                    SizeImage = checked((uint)(width * height * 4L)),
                },
            };

            bitmap = NativeMethods.CreateDIBSection(
                memoryDc,
                bitmapInfo,
                NativeMethods.DibRgbColors,
                out var bitmapBits,
                nint.Zero,
                0);

            if (bitmap == nint.Zero || bitmapBits == nint.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the frame bitmap.");
            }

            Marshal.Copy(pixels, 0, bitmapBits, pixels.Length);

            previousBitmap = NativeMethods.SelectObject(memoryDc, bitmap);
            if (previousBitmap == nint.Zero || previousBitmap == NativeMethods.HgdiError)
            {
                throw new InvalidOperationException(
                    $"SelectObject returned the failure sentinel {previousBitmap} while selecting the frame bitmap.");
            }

            var destination = new NativeMethods.Point(screenX, screenY);
            var size = new NativeMethods.Size(width, height);
            var source = new NativeMethods.Point(0, 0);
            var blend = new NativeMethods.BlendFunction(
                NativeMethods.AcSrcOver,
                0,
                byte.MaxValue,
                NativeMethods.AcSrcAlpha);

            if (!NativeMethods.UpdateLayeredWindow(
                    Handle,
                    screenDc,
                    destination,
                    size,
                    memoryDc,
                    source,
                    0,
                    blend,
                    NativeMethods.UlwAlpha))
            {
                var error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error, $"Could not update the layered window (Win32 error {error}).");
            }
        }
        catch (Exception exception)
        {
            operationException = exception;
            throw;
        }
        finally
        {
            var cleanupFailures = new List<string>();
            var cleanupOrder = GdiSelectionCleanupOrder.BitmapThenDeviceContext;

            if (memoryDc != nint.Zero
                && previousBitmap != nint.Zero
                && previousBitmap != NativeMethods.HgdiError)
            {
                var restoreResult = NativeMethods.SelectObject(memoryDc, previousBitmap);
                cleanupOrder = GdiSelectionCleanupPolicy.GetOrderAfterRestore(restoreResult);

                if (cleanupOrder == GdiSelectionCleanupOrder.DeviceContextThenBitmap)
                {
                    cleanupFailures.Add(
                        $"SelectObject returned the failure sentinel {restoreResult} while restoring the previous bitmap.");
                }
            }

            if (cleanupOrder == GdiSelectionCleanupOrder.DeviceContextThenBitmap)
            {
                DeleteMemoryDeviceContext(ref memoryDc, cleanupFailures);
                DeleteBitmap(ref bitmap, cleanupFailures);
            }
            else
            {
                DeleteBitmap(ref bitmap, cleanupFailures);
                DeleteMemoryDeviceContext(ref memoryDc, cleanupFailures);
            }

            if (NativeMethods.ReleaseDC(nint.Zero, screenDc) == 0)
            {
                cleanupFailures.Add("ReleaseDC returned 0 for the desktop DC.");
            }

            if (cleanupFailures.Count > 0)
            {
                var cleanupFailureMessage = string.Join(" ", cleanupFailures);
                if (operationException is not null)
                {
                    LayeredFrameCommitCoordinator.AppendCleanupFailure(
                        operationException,
                        cleanupFailureMessage);
                }
                else
                {
                    cleanupFailure = cleanupFailureMessage;
                }
            }
        }

        return cleanupFailure is null
            ? LayeredBitmapUploadResult.Success
            : LayeredBitmapUploadResult.WithCleanupFailure(cleanupFailure);
    }

    private static void DeleteMemoryDeviceContext(
        ref nint memoryDc,
        List<string> cleanupFailures)
    {
        if (memoryDc == nint.Zero)
        {
            return;
        }

        var deviceContextToDelete = memoryDc;
        memoryDc = nint.Zero;

        if (!NativeMethods.DeleteDC(deviceContextToDelete))
        {
            cleanupFailures.Add($"DeleteDC returned 0 for memory DC {deviceContextToDelete}.");
        }
    }

    private static void DeleteBitmap(ref nint bitmap, List<string> cleanupFailures)
    {
        if (bitmap == nint.Zero)
        {
            return;
        }

        var bitmapToDelete = bitmap;
        bitmap = nint.Zero;

        if (!NativeMethods.DeleteObject(bitmapToDelete))
        {
            cleanupFailures.Add($"DeleteObject returned 0 for bitmap {bitmapToDelete}.");
        }
    }

    private static void ReportCleanupFailures(
        Exception? operationException,
        List<string> cleanupFailures)
    {
        if (cleanupFailures.Count == 0)
        {
            return;
        }

        var message = string.Join(" ", cleanupFailures);
        if (operationException is not null)
        {
            LayeredFrameCommitCoordinator.AppendCleanupFailure(operationException, message);
            return;
        }

        throw new InvalidOperationException(message);
    }

    private void ApplyWindowRegion(IReadOnlyList<Int32Rect> runs)
    {
        var combinedRegion = NativeMethods.CreateRectRgn(0, 0, 0, 0);
        if (combinedRegion == nint.Zero)
        {
            throw new InvalidOperationException("CreateRectRgn returned NULL for the empty base region.");
        }

        var ownershipTransferred = false;
        Exception? regionOperationException = null;
        try
        {
            foreach (var run in runs)
            {
                var runRegion = NativeMethods.CreateRectRgn(
                    run.X,
                    run.Y,
                    checked(run.X + run.Width),
                    checked(run.Y + run.Height));

                if (runRegion == nint.Zero)
                {
                    throw new InvalidOperationException(
                        $"CreateRectRgn returned NULL for run ({run.X}, {run.Y}, {run.Width}, {run.Height}).");
                }

                Exception? runOperationException = null;
                try
                {
                    if (NativeMethods.CombineRgn(
                            combinedRegion,
                            combinedRegion,
                            runRegion,
                            NativeMethods.RgnOr) == NativeMethods.RegionError)
                    {
                        throw new InvalidOperationException("Could not combine the scanline window regions.");
                    }
                }
                catch (Exception exception)
                {
                    runOperationException = exception;
                    throw;
                }
                finally
                {
                    if (!NativeMethods.DeleteObject(runRegion))
                    {
                        ReportCleanupFailures(
                            runOperationException,
                            [$"DeleteObject returned 0 for scanline region {runRegion}."]);
                    }
                }
            }

            if (NativeMethods.SetWindowRgn(Handle, combinedRegion, redraw: true) == 0)
            {
                throw new InvalidOperationException("SetWindowRgn returned 0 while applying the frame region.");
            }

            // A successful SetWindowRgn transfers this HRGN to the system.
            ownershipTransferred = true;
        }
        catch (Exception exception)
        {
            regionOperationException = exception;
            throw;
        }
        finally
        {
            if (!ownershipTransferred && !NativeMethods.DeleteObject(combinedRegion))
            {
                ReportCleanupFailures(
                    regionOperationException,
                    [$"DeleteObject returned 0 for combined region {combinedRegion}."]);
            }
        }
    }

    private nint WindowProcedure(
        nint window,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        switch (message)
        {
            case NativeMethods.WmLeftButtonDown:
                RecordPointerDown();
                handled = true;
                break;

            case NativeMethods.WmMouseMove:
                HandlePointerMove(window);
                break;

            case NativeMethods.WmLeftButtonUp:
                HandlePointerUp();
                ReleaseDragCapture();
                handled = true;
                break;

            case NativeMethods.WmRightButtonUp:
                PublishInput(_input.RightClick());
                handled = true;
                break;

            case NativeMethods.WmCancelMode:
                ReleaseDragCapture();
                handled = true;
                break;

            case NativeMethods.WmCaptureChanged:
                _hasCapture = false;
                break;

            case NativeMethods.WmDestroy:
                ReleaseDragCapture();
                break;
        }

        return nint.Zero;
    }

    private void RecordPointerDown()
    {
        if (NativeMethods.GetCursorPos(out var cursor))
        {
            _input.PointerDown(new PetPointerPoint(cursor.X, cursor.Y));
        }
    }

    private void HandlePointerMove(nint window)
    {
        if (!NativeMethods.GetCursorPos(out var cursor))
        {
            return;
        }

        var result = _input.PointerMove(new PetPointerPoint(cursor.X, cursor.Y));
        if (result.StartDrag)
        {
            BeginDragCapture(window);
        }

        if (result.MoveDrag)
        {
            ContinueDrag(window);
        }

        PublishInput(result);
    }

    private void HandlePointerUp()
    {
        if (!NativeMethods.GetCursorPos(out var cursor))
        {
            return;
        }

        PublishInput(_input.PointerUp(new PetPointerPoint(cursor.X, cursor.Y)));
    }

    private void PublishInput(PetInputResult result)
    {
        if (result != PetInputResult.None)
        {
            Input?.Invoke(result);
        }
    }

    private void BeginDragCapture(nint window)
    {
        if (!NativeMethods.GetCursorPos(out var cursor)
            || !NativeMethods.GetWindowRect(window, out var windowRect))
        {
            return;
        }

        _dragOffsetX = cursor.X - windowRect.Left;
        _dragOffsetY = cursor.Y - windowRect.Top;

        _ = NativeMethods.SetCapture(window);
        _hasCapture = NativeMethods.GetCapture() == window;
    }

    private void ContinueDrag(nint window)
    {
        if (!_hasCapture || NativeMethods.GetCapture() != window)
        {
            _hasCapture = false;
            return;
        }

        if (!NativeMethods.GetCursorPos(out var cursor))
        {
            return;
        }

        _ = NativeMethods.SetWindowPos(
            window,
            nint.Zero,
            cursor.X - _dragOffsetX,
            cursor.Y - _dragOffsetY,
            0,
            0,
            NativeMethods.SwpNoSize
                | NativeMethods.SwpNoZOrder
                | NativeMethods.SwpNoActivate);
        ReportPositionIfChanged(new PetPointerPoint(cursor.X - _dragOffsetX, cursor.Y - _dragOffsetY));
    }

    private void ReportPositionIfChanged(PetPointerPoint position)
    {
        if (_lastReportedPosition == position)
        {
            return;
        }

        _lastReportedPosition = position;
        PositionChanged?.Invoke(position);
    }

    private void HideAfterFailedFrame()
    {
        _ = NativeMethods.ShowWindow(Handle, NativeMethods.SwHide);
        _isVisible = false;
    }

    private void ReleaseDragCapture()
    {
        if (!_hasCapture)
        {
            return;
        }

        _hasCapture = false;
        if (NativeMethods.GetCapture() == Handle)
        {
            _ = NativeMethods.ReleaseCapture();
        }
    }
}
