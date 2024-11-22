using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace Forge.Cli.Infra;

public sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceCollection builder;
    private IServiceProvider provider = default!;

    public TypeRegistrar(IServiceCollection builder)
    {
        this.builder = builder;
    }

    public ITypeResolver Build()
    {
        provider = builder.BuildServiceProvider();
        return new TypeResolver(provider);
    }

    public IServiceProvider Provider => provider;

    public void Register(Type service, Type implementation)
    {
        builder.AddSingleton(service, implementation);
    }

    public void RegisterInstance(Type service, object implementation)
    {
        builder.AddSingleton(service, implementation);
    }

    public void RegisterLazy(Type service, Func<object> func)
    {
        if (func is null)
        {
            throw new ArgumentNullException(nameof(func));
        }

        builder.AddSingleton(service, (provider) => func());
    }
}