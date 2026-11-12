using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Arc.Avalonia.Markdown.Controls;

/// <summary>
/// Pointer interaction for links that are rendered as text runs (<see cref="ArcMarkdownLinkInline"/>).
/// <para>
/// The label is ordinary <see cref="Span"/> text, so the link shares the prose baseline/line box, wraps with
/// the paragraph and can be selected/copied; this helper turns a tap on the link's character cells into the
/// same activation path the old embedded <c>HyperlinkButton</c> had: <see cref="ArcMarkdownRenderContext.LinkClickSink"/>
/// first (host may mark the click handled) and otherwise <see cref="ArcMarkdownLinkMenu"/>. Hover shows a hand
/// cursor and the link title, and a drag (scroll) cancels the pending activation through
/// <see cref="ArcMarkdownTapGuard"/>.
/// </para>
/// </summary>
internal static class ArcMarkdownLinkInteraction
{
    private static readonly ConditionalWeakTable<TextBlock, LinkGesture> Gestures = new();

    /// <summary>Wires link gestures onto a text block created for a block render pass (idempotent).</summary>
    internal static void Attach(TextBlock host, ArcMarkdownRenderContext context)
    {
        if (Gestures.TryGetValue(host, out _))
        {
            return;
        }

        var gesture = new LinkGesture(host, context);
        Gestures.Add(host, gesture);
        gesture.Attach();
    }

    private sealed class LinkGesture
    {
        private readonly TextBlock _host;
        private readonly ArcMarkdownRenderContext _context;
        private readonly ArcMarkdownTapGuard _tapGuard = new();
        private readonly List<(int Offset, ArcMarkdownLinkInline Link)> _map = [];

        private List<(Rect Rect, ArcMarkdownLinkInline Link)>? _rects;
        private (double Width, double Height, int Lines)? _rectsSignature;
        private bool _mapBuilt;
        private bool _hasLinks;
        private bool _pressed;
        private Point _pressPoint;
        private ArcMarkdownLinkInline? _pressedLink;
        private Cursor? _restingCursor;
        private Cursor? _handCursor;
        private bool _handCursorResolved;
        private ArcMarkdownLinkInline? _hovered;

        internal LinkGesture(TextBlock host, ArcMarkdownRenderContext context)
        {
            _host = host;
            _context = context;
        }

        internal void Attach()
        {
            _restingCursor = _host.Cursor;
            _tapGuard.Attach(_host);
            _host.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
            _host.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            _host.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
            _host.AddHandler(InputElement.PointerCaptureLostEvent, OnPointerCaptureLost, RoutingStrategies.Tunnel, handledEventsToo: true);
            _host.AddHandler(InputElement.PointerExitedEvent, OnPointerExited, RoutingStrategies.Tunnel, handledEventsToo: true);
            // The streaming tail reuses its text block and swaps the inline tree on every flush; a cached
            // character map would keep the old offsets and old link instances.
            _host.Inlines!.CollectionChanged += (_, _) => Invalidate();
        }

        private void Invalidate()
        {
            _mapBuilt = false;
            _hasLinks = false;
            _map.Clear();
            _rects = null;
            _rectsSignature = null;
            _hovered = null;
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            var hit = HitTest(e.GetPosition(_host));
            if (ReferenceEquals(hit, _hovered))
            {
                return;
            }

            _hovered = hit;
            _host.Cursor = hit is null ? _restingCursor : HandCursor();
            if (hit?.Title is { Length: > 0 } title)
            {
                ToolTip.SetTip(_host, title);
            }
            else
            {
                ToolTip.SetTip(_host, null);
            }
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _pressed = false;
            _pressedLink = null;
            var point = e.GetPosition(_host);
            var hit = HitTest(point);
            if (hit is null)
            {
                return;
            }

            // Right click/two-finger tap opens the link menu immediately, like the old button Path.
            if (e.GetCurrentPoint(_host).Properties.IsRightButtonPressed)
            {
                e.Handled = true;
                Activate(hit);
                return;
            }

            // Keep the tap for the link: do not let a SelectableTextBlock start a selection/caret gesture.
            e.Handled = true;
            _pressed = true;
            _pressedLink = hit;
            _pressPoint = point;
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_pressed)
            {
                return;
            }

            _pressed = false;
            var link = _pressedLink;
            _pressedLink = null;
            if (link is null)
            {
                return;
            }

            var point = e.GetPosition(_host);
            var moved = Math.Abs(point.X - _pressPoint.X) > ArcMarkdownTapGuard.Slop ||
                        Math.Abs(point.Y - _pressPoint.Y) > ArcMarkdownTapGuard.Slop;
            if (moved || _tapGuard.IsCancelled)
            {
                return;
            }

            // A tap must land on the same link (not on prose after a drag-free jitter).
            if (!ReferenceEquals(HitTest(point), link))
            {
                return;
            }

            e.Handled = true;
            Activate(link);
        }

        private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            _pressed = false;
            _pressedLink = null;
        }

        /// <summary>
        /// Hand cursor, created on first use: headless hosts have no cursor factory and must not fail a render
        /// just because a link could show a pointer cursor.
        /// </summary>
        private Cursor? HandCursor()
        {
            if (_handCursorResolved)
            {
                return _handCursor;
            }

            _handCursorResolved = true;
            try
            {
                _handCursor = new Cursor(StandardCursorType.Hand);
            }
            catch
            {
                _handCursor = null;
            }

            return _handCursor;
        }

        private void OnPointerExited(object? sender, PointerEventArgs e)
        {
            _hovered = null;
            _host.Cursor = _restingCursor;
            ToolTip.SetTip(_host, null);
        }

        private void Activate(ArcMarkdownLinkInline link)
        {
            var click = new ArcMarkdownLinkClick(link.Url, link.DisplayText);
            try
            {
                _context.LinkClickSink?.Invoke(click);
            }
            catch (Exception ex)
            {
                _context.Warn("ARC-MD-LINK-002", $"链接点击回调失败：{ex.Message}", nameof(ArcMarkdownLinkInline));
            }

            if (click.Handled)
            {
                return;
            }

            try
            {
                // The link is a text run and cannot take focus back after the flyout closes, so remember what
                // was focused before the menu and restore it (keyboard/screen-reader users keep their place).
                var previouslyFocused = TopLevel.GetTopLevel(_host)?.FocusManager?.GetFocusedElement();
                new ArcMarkdownLinkMenu(_host, link.Url, _context, () =>
                {
                    if (previouslyFocused is InputElement { Focusable: true } element)
                    {
                        element.Focus();
                    }
                }).Show();
            }
            catch (Exception ex)
            {
                _context.Warn("ARC-MD-LINK-003", $"链接菜单打开失败：{ex.Message}", nameof(ArcMarkdownLinkInline));
            }
        }

        private ArcMarkdownLinkInline? HitTest(Point point)
        {
            var rects = LinkRects();
            if (rects is null)
            {
                return null;
            }

            foreach (var entry in rects)
            {
                if (entry.Rect.Contains(point))
                {
                    return entry.Link;
                }
            }

            return null;
        }

        private List<(Rect Rect, ArcMarkdownLinkInline Link)>? LinkRects()
        {
            EnsureMap();
            if (!_hasLinks)
            {
                return null;
            }

            var layout = _host.TextLayout;
            if (layout is null)
            {
                return null;
            }

            // Character cells move when the paragraph reflows (width/font/theme change). Invalidate on a layout
            // signature instead of every LayoutUpdated pass, so hovering a long link does not rebuild on every
            // unrelated layout of the document.
            var signature = (layout.Width, layout.Height, layout.TextLines.Count);
            if (_rects is not null && _rectsSignature == signature)
            {
                return _rects;
            }

            var rects = new List<(Rect, ArcMarkdownLinkInline)>(_map.Count);
            try
            {
                foreach (var (offset, link) in _map)
                {
                    var rect = layout.HitTestTextPosition(offset);
                    if (rect.Width > 0 || rect.Height > 0)
                    {
                        rects.Add((rect, link));
                    }
                }
            }
            catch
            {
                // A layout that is being invalidated underneath us must not break pointer handling.
                return null;
            }

            _rects = rects;
            _rectsSignature = signature;
            return rects;
        }

        private void EnsureMap()
        {
            if (_mapBuilt)
            {
                return;
            }

            _mapBuilt = true;
            _map.Clear();
            if (_host.Inlines is not { Count: > 0 } inlines)
            {
                return;
            }

            var offset = 0;
            Walk(inlines, ref offset);
            _hasLinks = _map.Count > 0;
        }

        private void Walk(InlineCollection inlines, ref int offset)
        {
            foreach (var inline in inlines)
            {
                switch (inline)
                {
                    case ArcMarkdownLinkInline link:
                    {
                        var start = offset;
                        Walk(link.Inlines!, ref offset);
                        for (var index = start; index < offset; index++)
                        {
                            _map.Add((index, link));
                        }

                        break;
                    }

                    case Run run:
                        offset += run.Text?.Length ?? 0;
                        break;
                    case LineBreak:
                        // Avalonia's LineBreak run emits the platform newline sequence; the text layout indexes
                        // the whole sequence on Windows (two characters for "\r\n"), not one.
                        offset += Environment.NewLine.Length;
                        break;
                    case InlineUIContainer:
                        // Matches the text layout: an embedded control advances one character.
                        offset += 1;
                        break;
                    case Span span:
                        Walk(span.Inlines!, ref offset);
                        break;
                    default:
                        offset += 1;
                        break;
                }
            }
        }
    }
}
