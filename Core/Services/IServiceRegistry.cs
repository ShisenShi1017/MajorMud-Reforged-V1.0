namespace MajorMud.Reforged.Core.Services;

public interface IServiceRegistry
{
    void Register<TService>(TService service) where TService : class, IReforgedService;
    TService? Resolve<TService>() where TService : class, IReforgedService;
    IReadOnlyCollection<string> RegisteredServiceNames { get; }
}
