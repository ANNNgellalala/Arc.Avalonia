using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using DshRemote.App.ViewModels;

namespace DshRemote.App.Views;

public partial class SessionsView : UserControl
{
    private const double NarrowWidthThreshold = 460;
    private const double ArchiveWidth = 96;
    private const double SwipeThreshold = 10;
    private const double OpenThreshold = 48;

    private Border? _gestureRow;
    private Point _gestureStart;
    private bool _gestureDragging;
    private bool _gestureMoved;
    private Border? _openRow;

    public SessionsView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateResponsive();
        DataContextChanged += (_, _) => Attach();
    }

    private MainViewModel? _viewModel;

    private void Attach()
    {
        if (_viewModel is not null)
        {
            _viewModel.WorkspaceSelectionChanged -= OnWorkspaceSelectionChanged;
        }

        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null)
        {
            _viewModel.WorkspaceSelectionChanged += OnWorkspaceSelectionChanged;
        }

        UpdateResponsive();
    }

    private void OnWorkspaceSelectionChanged(object? sender, EventArgs e)
    {
        // Choosing a workspace resets the list to the top.
        SessionScroll.Offset = new Vector(0, 0);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_viewModel is not null)
        {
            _viewModel.WorkspaceSelectionChanged -= OnWorkspaceSelectionChanged;
            _viewModel = null;
        }
    }

    private void UpdateResponsive()
    {
        // Narrow phones drop the model column so the title/cwd keep their space.
        var narrow = Bounds.Width > 0 && Bounds.Width < NarrowWidthThreshold;
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.ShowSessionModel = !narrow;
        }
    }

    // ---- Row swipe (reveal the archive action) + tap to open ---------------------------------------

    private void OnSessionRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border row)
        {
            return;
        }

        var state = EnsureState(row);
        _gestureRow = row;
        _gestureStart = e.GetPosition(row);
        _gestureDragging = false;
        _gestureMoved = false;

        // Close any other revealed row.
        if (_openRow is { } other && other != row && other.Tag is RowSwipeState otherState)
        {
            otherState.Transform.X = 0;
            otherState.Open = false;
            _openRow = null;
        }

        // Do not handle the press: vertical scrolling keeps working, and taps fall through to
        // OnSessionRowPointerReleased.
    }

    private void OnSessionRowPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Border row || row != _gestureRow)
        {
            return;
        }

        var state = EnsureState(row);
        var point = e.GetPosition(row);
        var dx = point.X - _gestureStart.X;
        var dy = point.Y - _gestureStart.Y;

        if (!_gestureDragging)
        {
            if (Math.Abs(dx) > SwipeThreshold || Math.Abs(dy) > SwipeThreshold)
            {
                _gestureMoved = true;
            }

            if (Math.Abs(dx) <= SwipeThreshold || Math.Abs(dx) <= Math.Abs(dy))
            {
                return;
            }

            _gestureDragging = true;
            e.Pointer.Capture(row);
        }

        var baseOffset = state.Open ? -ArchiveWidth : 0;
        state.Transform.X = Math.Clamp(baseOffset + dx, -ArchiveWidth, 0);
        e.Handled = true;
    }

    private void OnSessionRowPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not Border row || row != _gestureRow)
        {
            return;
        }

        var state = EnsureState(row);
        _gestureRow = null;

        if (_gestureDragging)
        {
            _gestureDragging = false;
            e.Pointer.Capture(null);

            if (state.Transform.X <= -OpenThreshold)
            {
                state.Transform.X = -ArchiveWidth;
                state.Open = true;
                _openRow = row;
            }
            else
            {
                state.Transform.X = 0;
                state.Open = false;
                if (_openRow == row)
                {
                    _openRow = null;
                }
            }

            e.Handled = true;
            return;
        }

        // A tap: close the row when it is open, otherwise open the session. A vertical scroll that
        // happens to start and end on the same row must not open it.
        if (_gestureMoved)
        {
            _gestureMoved = false;
            e.Handled = true;
            return;
        }

        if (state.Open)
        {
            state.Transform.X = 0;
            state.Open = false;
            if (_openRow == row)
            {
                _openRow = null;
            }

            e.Handled = true;
            return;
        }

        if (row.DataContext is SessionListViewModel item && item.OpenCommand.CanExecute(item))
        {
            item.OpenCommand.Execute(item);
            e.Handled = true;
        }
    }

    private static RowSwipeState EnsureState(Border row)
    {
        if (row.Tag is RowSwipeState existing)
        {
            return existing;
        }

        var created = new RowSwipeState();
        row.Tag = created;
        row.RenderTransform = created.Transform;
        return created;
    }

    private sealed class RowSwipeState
    {
        public TranslateTransform Transform { get; } = new();

        public bool Open { get; set; }
    }
}
