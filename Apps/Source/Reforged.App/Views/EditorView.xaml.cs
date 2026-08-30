using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Reforged.App.ViewModels;

namespace Reforged.App.Views;

public partial class EditorView : UserControl
{
    private RoomVisualMapNodeRow? _dragNode;
    private FrameworkElement? _dragElement;
    private ItemsControl? _dragSurface;
    private Point _dragStart;
    private double _dragStartX;
    private double _dragStartY;
    private bool _dragMoved;

    public EditorView()
    {
        InitializeComponent();
    }

    private void MapRoom_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not RoomVisualMapNodeRow node) return;
        if (DataContext is EditorViewModel viewModel) viewModel.SelectVisualMapRoom(node);

        if (e.ClickCount >= 2)
        {
            CancelMapDrag();
            if (DataContext is EditorViewModel editor) editor.OpenVisualMapRoom(node);
            e.Handled = true;
            return;
        }

        var surface = FindAncestor<ItemsControl>(element);
        if (surface is null) return;
        _dragNode = node;
        _dragElement = element;
        _dragSurface = surface;
        _dragStart = e.GetPosition(surface);
        _dragStartX = node.X;
        _dragStartY = node.Y;
        _dragMoved = false;
        element.CaptureMouse();
        e.Handled = true;
    }

    private void MapRoom_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragNode is null || _dragElement is null || _dragSurface is null || e.LeftButton != MouseButtonState.Pressed) return;
        var point = e.GetPosition(_dragSurface);
        var dx = point.X - _dragStart.X;
        var dy = point.Y - _dragStart.Y;
        if (!_dragMoved && Math.Abs(dx) < 3 && Math.Abs(dy) < 3) return;
        _dragMoved = true;
        _dragNode.X = Math.Max(0, _dragStartX + dx);
        _dragNode.Y = Math.Max(0, _dragStartY + dy);
        e.Handled = true;
    }

    private void MapRoom_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragElement is not null && Mouse.Captured == _dragElement) _dragElement.ReleaseMouseCapture();
        _dragNode = null;
        _dragElement = null;
        _dragSurface = null;
        _dragMoved = false;
        e.Handled = true;
    }

    private void CancelMapDrag()
    {
        if (_dragElement is not null && Mouse.Captured == _dragElement) _dragElement.ReleaseMouseCapture();
        _dragNode = null;
        _dragElement = null;
        _dragSurface = null;
        _dragMoved = false;
    }

    private static T? FindAncestor<T>(DependencyObject? start) where T : DependencyObject
    {
        var current = start;
        while (current is not null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
