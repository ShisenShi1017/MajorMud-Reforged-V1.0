namespace RealmGate.Core.Services;

/// <summary>
/// Small dependency-free service registry for Milestone 1 application bootstrapping.
/// This is intentionally simple and can later be replaced behind the same contract.
/// </summary>
public sealed class ServiceRegistry
{
    private readonly Dictionary<Type, object> services = new();

    public void Register<TService>(TService service) where TService : notnull
        => services[typeof(TService)] = service;

    public bool TryResolve<TService>(out TService? service) where TService : class
    {
        if (services.TryGetValue(typeof(TService), out var value) && value is TService typed)
        {
            service = typed;
            return true;
        }

        service = null;
        return false;
    }

    public TService ResolveRequired<TService>() where TService : class
    {
        if (TryResolve<TService>(out var service) && service is not null)
        {
            return service;
        }

        throw new InvalidOperationException($"Required service is not registered: {typeof(TService).FullName}");
    }
}
