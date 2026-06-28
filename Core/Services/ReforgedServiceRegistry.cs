namespace MajorMud.Reforged.Core.Services;

/// <summary>
/// Minimal service registry used until the Launcher and applications adopt a richer dependency injection host.
/// </summary>
public sealed class ReforgedServiceRegistry : IServiceRegistry
{
    private readonly Dictionary<Type, IReforgedService> _services = new();

    public void Register<TService>(TService service) where TService : class, IReforgedService
    {
        _services[typeof(TService)] = service ?? throw new ArgumentNullException(nameof(service));
    }

    public TService? Resolve<TService>() where TService : class, IReforgedService
    {
        return _services.TryGetValue(typeof(TService), out var service) ? service as TService : null;
    }

    public IReadOnlyCollection<string> RegisteredServiceNames => _services.Values.Select(s => s.ServiceName).OrderBy(n => n).ToArray();
}
