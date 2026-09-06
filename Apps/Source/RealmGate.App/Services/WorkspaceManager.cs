namespace RealmGate.App.Services;

public sealed class WorkspaceManager(IErrorReporter errors) : IWorkspaceManager
{
    private readonly Dictionary<string, IWorkspace> _items =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IWorkspace> Workspaces => _items.Values;

    public IWorkspace? ActiveWorkspace { get; private set; }

    public event EventHandler? ActiveWorkspaceChanged;

    public void Register(IWorkspace workspace) =>
        _items.Add(workspace.Id, workspace);

    public async Task ActivateAsync(
        string id,
        CancellationToken token = default)
    {
        if (!_items.TryGetValue(id, out var next))
            throw new KeyNotFoundException(
                $"Workspace '{id}' is not registered.");

        if (ReferenceEquals(next, ActiveWorkspace))
            return;

        RealmGatePerformanceTrace.Mark(
            "WORKSPACE",
            $"REQUEST | Id={id} | Title={next.Title}");

        try
        {
            if (ActiveWorkspace is not null)
            {
                using (RealmGatePerformanceTrace.Measure(
                    "WORKSPACE",
                    $"Deactivate {ActiveWorkspace.Id}"))
                {
                    await ActiveWorkspace.DeactivateAsync(token);
                }
            }

            using (RealmGatePerformanceTrace.Measure(
                "WORKSPACE",
                $"Activate {id}"))
            {
                await next.ActivateAsync(token);
            }

            ActiveWorkspace = next;
            ActiveWorkspaceChanged?.Invoke(this, EventArgs.Empty);

            RealmGatePerformanceTrace.Mark(
                "WORKSPACE",
                $"ACTIVE | Id={id} | Title={next.Title}");
        }
        catch (Exception e)
        {
            RealmGatePerformanceTrace.Mark(
                "WORKSPACE",
                $"FAILED | Id={id} | {e.GetType().Name}: {e.Message}");

            errors.Report(e, $"Workspace activation failed: {id}");
            throw;
        }
    }

    public async Task CloseAsync(
        string id,
        CancellationToken token = default)
    {
        if (!_items.TryGetValue(id, out var item) || !item.CanClose)
            return;

        if (ReferenceEquals(item, ActiveWorkspace))
        {
            using (RealmGatePerformanceTrace.Measure(
                "WORKSPACE",
                $"Close {id}"))
            {
                await item.DeactivateAsync(token);
            }

            ActiveWorkspace = null;
            ActiveWorkspaceChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

public sealed class Workspace(
    string id,
    string title,
    object view,
    bool canClose = false) : IWorkspace
{
    public string Id => id;
    public string Title => title;
    public object View => view;
    public bool CanClose => canClose;

    public Task ActivateAsync(CancellationToken token) =>
        Task.CompletedTask;

    public Task DeactivateAsync(CancellationToken token) =>
        Task.CompletedTask;
}

public sealed class LazyWorkspace(
    string id,
    string title,
    Func<object> viewFactory,
    bool canClose = false) : IWorkspace
{
    private readonly Lazy<object> _view = new(
        viewFactory ?? throw new ArgumentNullException(nameof(viewFactory)),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public string Id => id;
    public string Title => title;
    public object View => _view.Value;
    public bool CanClose => canClose;

    public Task ActivateAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        using (RealmGatePerformanceTrace.Measure(
            "WORKSPACE VIEW",
            $"Create/View {id}"))
        {
            _ = View;
        }

        return Task.CompletedTask;
    }

    public Task DeactivateAsync(CancellationToken token) =>
        Task.CompletedTask;
}
