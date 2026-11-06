using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using RealmGate.Desktop.ViewModels;

namespace RealmGate.Desktop.Controls;

public sealed class CreatorMapCanvas : Control
{
    public static readonly StyledProperty<IEnumerable<CreatorMapRoomRow>?> RoomsProperty =
        AvaloniaProperty.Register<CreatorMapCanvas, IEnumerable<CreatorMapRoomRow>?>(nameof(Rooms));
    public static readonly StyledProperty<IEnumerable<CreatorMapEdgeRow>?> EdgesProperty =
        AvaloniaProperty.Register<CreatorMapCanvas, IEnumerable<CreatorMapEdgeRow>?>(nameof(Edges));

    private static readonly Typeface LabelTypeface = new(
        new FontFamily("Segoe UI,Inter,DejaVu Sans,Sans-Serif"),
        FontStyle.Normal,
        FontWeight.Normal,
        FontStretch.Normal);
    private static readonly IBrush CanvasBrush = new SolidColorBrush(Color.Parse("#10141B"));
    private static readonly IBrush GridBrush = new SolidColorBrush(Color.Parse("#202936"));
    private static readonly IBrush EdgeBrush = new SolidColorBrush(Color.Parse("#64748B"));
    private static readonly IBrush RoomBrush = new SolidColorBrush(Color.Parse("#263445"));
    private static readonly IBrush SpecialRoomBrush = new SolidColorBrush(Color.Parse("#5B3E19"));
    private static readonly IBrush RoomBorderBrush = new SolidColorBrush(Color.Parse("#9FB2C8"));
    private static readonly IBrush RoomTextBrush = new SolidColorBrush(Color.Parse("#F4F7FA"));
    private static readonly IBrush MutedTextBrush = new SolidColorBrush(Color.Parse("#AAB7C7"));

    private const double CanvasMargin = 28;
    private const double DetailedWidth = 116;
    private const double DetailedHeight = 42;
    private readonly List<CreatorMapRoomRow> _trackedRows = [];
    private INotifyCollectionChanged? _roomCollection;
    private INotifyCollectionChanged? _edgeCollection;
    private CreatorMapRoomRow? _draggedRoom;
    private Vector _dragOffset;
    private Point _lastPointer;
    private bool _isPanning;
    private double _zoom = 1;
    private Vector _pan;

    public CreatorMapCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        MinHeight = 360;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += (_, _) => EndPointerOperation();
        PointerWheelChanged += OnPointerWheelChanged;
    }

    public IEnumerable<CreatorMapRoomRow>? Rooms
    {
        get => GetValue(RoomsProperty);
        set => SetValue(RoomsProperty, value);
    }

    public IEnumerable<CreatorMapEdgeRow>? Edges
    {
        get => GetValue(EdgesProperty);
        set => SetValue(EdgesProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RoomsProperty)
        {
            AttachRoomSubscriptions();
            ResetViewport();
        }
        else if (change.Property == EdgesProperty)
        {
            AttachEdgeSubscription();
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.DrawRectangle(CanvasBrush, null, Bounds);
        DrawGrid(context);

        var rooms = Rooms?.ToArray() ?? [];
        if (rooms.Length == 0)
        {
            DrawText(context, "Load a map to open the visual layout.", new Point(24, 24), 15, MutedTextBrush, 420);
            return;
        }

        var transform = CreateTransform(rooms);
        var centers = rooms.ToDictionary(
            room => room.RoomId,
            room => transform.ToScreen(room.X, room.Y),
            StringComparer.OrdinalIgnoreCase);

        var edgePen = new Pen(EdgeBrush, _zoom >= 3 ? 1.5 : 1);
        foreach (var edge in Edges ?? [])
        {
            if (!centers.TryGetValue(edge.FromRoomId, out var from) ||
                !centers.TryGetValue(edge.ToRoomId, out var to))
                continue;
            context.DrawLine(edgePen, from, to);
        }

        foreach (var room in rooms)
            DrawRoom(context, room, centers[room.RoomId]);
    }

    private void DrawGrid(DrawingContext context)
    {
        var pen = new Pen(GridBrush, 1);
        const double spacing = 40;
        var startX = ((_pan.X % spacing) + spacing) % spacing;
        var startY = ((_pan.Y % spacing) + spacing) % spacing;
        for (var x = startX; x < Bounds.Width; x += spacing)
            context.DrawLine(pen, new Point(x, 0), new Point(x, Bounds.Height));
        for (var y = startY; y < Bounds.Height; y += spacing)
            context.DrawLine(pen, new Point(0, y), new Point(Bounds.Width, y));
    }

    private void DrawRoom(DrawingContext context, CreatorMapRoomRow room, Point center)
    {
        var rect = RoomBounds(center);
        var special = !string.IsNullOrWhiteSpace(room.Marker);
        context.DrawRectangle(special ? SpecialRoomBrush : RoomBrush, new Pen(RoomBorderBrush, 1), rect);

        if (_zoom < 1.6)
            return;

        if (_zoom < 3)
        {
            DrawText(context, room.RoomNumber.ToString(CultureInfo.InvariantCulture),
                new Point(rect.X + 3, rect.Y + 1), 9, RoomTextBrush, rect.Width - 6);
            return;
        }

        DrawText(context, room.RoomNumber.ToString(CultureInfo.InvariantCulture),
            new Point(rect.X + 5, rect.Y + 3), 11, RoomTextBrush, rect.Width - 10);
        DrawText(context, room.Name,
            new Point(rect.X + 5, rect.Y + 20), 10, MutedTextBrush, rect.Width - 10);
    }

    private static void DrawText(
        DrawingContext context,
        string text,
        Point position,
        double size,
        IBrush brush,
        double width)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            LabelTypeface,
            size,
            brush)
        {
            MaxLineCount = 1,
            MaxTextWidth = Math.Max(1, width),
            Trimming = TextTrimming.CharacterEllipsis
        };
        context.DrawText(formatted, position);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        Focus();
        var point = args.GetPosition(this);
        _lastPointer = point;
        var rooms = Rooms?.ToArray() ?? [];
        var transform = CreateTransform(rooms);
        _draggedRoom = rooms.LastOrDefault(room => RoomBounds(transform.ToScreen(room.X, room.Y)).Contains(point));
        if (_draggedRoom is not null)
        {
            var center = transform.ToScreen(_draggedRoom.X, _draggedRoom.Y);
            _dragOffset = point - center;
        }
        else
        {
            _isPanning = true;
        }
        args.Pointer.Capture(this);
        args.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs args)
    {
        var point = args.GetPosition(this);
        if (_draggedRoom is not null)
        {
            var transform = CreateTransform(Rooms?.ToArray() ?? []);
            var world = transform.ToWorld(point - _dragOffset);
            _draggedRoom.X = Math.Max(0, world.X);
            _draggedRoom.Y = Math.Max(0, world.Y);
            InvalidateVisual();
            args.Handled = true;
            return;
        }

        if (_isPanning)
        {
            _pan += point - _lastPointer;
            _lastPointer = point;
            InvalidateVisual();
            args.Handled = true;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        EndPointerOperation();
        args.Pointer.Capture(null);
        args.Handled = true;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs args)
    {
        var rooms = Rooms?.ToArray() ?? [];
        if (rooms.Length == 0) return;
        var pointer = args.GetPosition(this);
        var before = CreateTransform(rooms).ToWorld(pointer);
        _zoom = Math.Clamp(_zoom * (args.Delta.Y > 0 ? 1.22 : 1 / 1.22), 0.45, 12);
        var unpanned = CreateTransform(rooms, Vector.Zero).ToScreen(before.X, before.Y);
        _pan = pointer - unpanned;
        InvalidateVisual();
        args.Handled = true;
    }

    private void EndPointerOperation()
    {
        _draggedRoom = null;
        _isPanning = false;
    }

    private Rect RoomBounds(Point center)
    {
        var width = _zoom >= 3 ? DetailedWidth : _zoom >= 1.6 ? 34 : 8;
        var height = _zoom >= 3 ? DetailedHeight : _zoom >= 1.6 ? 18 : 8;
        return new Rect(center.X - width / 2, center.Y - height / 2, width, height);
    }

    private MapTransform CreateTransform(IReadOnlyList<CreatorMapRoomRow> rooms, Vector? panOverride = null)
    {
        if (rooms.Count == 0) return new MapTransform(0, 0, 1, panOverride ?? _pan);
        var minX = rooms.Min(room => room.X);
        var minY = rooms.Min(room => room.Y);
        var maxX = rooms.Max(room => room.X);
        var maxY = rooms.Max(room => room.Y);
        var width = Math.Max(1, maxX - minX);
        var height = Math.Max(1, maxY - minY);
        var fitX = Math.Max(0.01, (Bounds.Width - CanvasMargin * 2) / width);
        var fitY = Math.Max(0.01, (Bounds.Height - CanvasMargin * 2) / height);
        var scale = Math.Max(0.01, Math.Min(fitX, fitY)) * _zoom;
        return new MapTransform(minX, minY, scale, (panOverride ?? _pan) + new Vector(CanvasMargin, CanvasMargin));
    }

    private void AttachRoomSubscriptions()
    {
        if (_roomCollection is not null) _roomCollection.CollectionChanged -= OnRoomsChanged;
        foreach (var row in _trackedRows) row.PropertyChanged -= OnRoomChanged;
        _trackedRows.Clear();
        _roomCollection = Rooms as INotifyCollectionChanged;
        if (_roomCollection is not null) _roomCollection.CollectionChanged += OnRoomsChanged;
        RefreshTrackedRows();
    }

    private void AttachEdgeSubscription()
    {
        if (_edgeCollection is not null) _edgeCollection.CollectionChanged -= OnEdgesChanged;
        _edgeCollection = Edges as INotifyCollectionChanged;
        if (_edgeCollection is not null) _edgeCollection.CollectionChanged += OnEdgesChanged;
    }

    private void OnRoomsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        RefreshTrackedRows();
        ResetViewport();
    }

    private void OnEdgesChanged(object? sender, NotifyCollectionChangedEventArgs args) => InvalidateVisual();

    private void RefreshTrackedRows()
    {
        foreach (var row in _trackedRows) row.PropertyChanged -= OnRoomChanged;
        _trackedRows.Clear();
        if (Rooms is not null) _trackedRows.AddRange(Rooms);
        foreach (var row in _trackedRows) row.PropertyChanged += OnRoomChanged;
        InvalidateVisual();
    }

    private void OnRoomChanged(object? sender, PropertyChangedEventArgs args) => InvalidateVisual();

    private void ResetViewport()
    {
        _zoom = 1;
        _pan = default;
        InvalidateVisual();
    }

    private readonly record struct MapTransform(double MinX, double MinY, double Scale, Vector Offset)
    {
        public Point ToScreen(double x, double y) =>
            new((x - MinX) * Scale + Offset.X, (y - MinY) * Scale + Offset.Y);

        public Point ToWorld(Point point) =>
            new((point.X - Offset.X) / Scale + MinX, (point.Y - Offset.Y) / Scale + MinY);
    }
}
