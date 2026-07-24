using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using SMEFLOWSystem.Application.Extensions;

namespace SMEFLOWSystem.Tests.Extensions;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_RegistersResolvableMapper()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IMapper>());
    }
}
