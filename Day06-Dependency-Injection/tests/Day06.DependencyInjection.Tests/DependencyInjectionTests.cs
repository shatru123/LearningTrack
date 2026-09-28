using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Day06.DependencyInjection.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void Lifetimes_ValidateTransientScopedAndSingletonBehaviors()
    {
        var services = new ServiceCollection();
        services.AddTransient<ITransientOperation, TransientOperation>();
        services.AddScoped<IScopedOperation, ScopedOperation>();
        services.AddSingleton<ISingletonOperation, SingletonOperation>();

        var provider = services.BuildServiceProvider();

        IScopedOperation scopedRef1;
        using (var scope1 = provider.CreateScope())
        {
            var t1 = scope1.ServiceProvider.GetRequiredService<ITransientOperation>();
            var t2 = scope1.ServiceProvider.GetRequiredService<ITransientOperation>();
            Assert.NotEqual(t1.InstanceId, t2.InstanceId); // Transient is always new

            scopedRef1 = scope1.ServiceProvider.GetRequiredService<IScopedOperation>();
            var s2 = scope1.ServiceProvider.GetRequiredService<IScopedOperation>();
            Assert.Equal(scopedRef1.InstanceId, s2.InstanceId); // Scoped is identical within scope
        }

        // Scope ended: verify disposal
        Assert.True(scopedRef1.IsDisposed);

        using (var scope2 = provider.CreateScope())
        {
            var scopedRef2 = scope2.ServiceProvider.GetRequiredService<IScopedOperation>();
            Assert.NotEqual(scopedRef1.InstanceId, scopedRef2.InstanceId); // New scope has new instance

            var single1 = scope2.ServiceProvider.GetRequiredService<ISingletonOperation>();
            var single2 = provider.GetRequiredService<ISingletonOperation>();
            Assert.Equal(single1.InstanceId, single2.InstanceId); // Singleton is always identical
        }
    }

    [Fact]
    public void CaptiveDependency_ThrowsException_WhenValidateScopesIsTrue()
    {
        var services = new ServiceCollection();
        services.AddScoped<IScopedOperation, ScopedOperation>();
        services.AddSingleton<BadSingletonWithCaptiveDependency>();

        // 1. When ValidateOnBuild is true, BuildServiceProvider immediately detects the captive dependency!
        Assert.Throws<AggregateException>(() =>
        {
            services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });
        });

        // 2. When ValidateOnBuild is false, resolution detects the scope violation at runtime!
        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = false
        });

        Assert.Throws<InvalidOperationException>(() =>
        {
            provider.GetRequiredService<BadSingletonWithCaptiveDependency>();
        });
    }

    [Fact]
    public void ScopeFactory_ResolvesCaptiveDependencyProblem_Safely()
    {
        var services = new ServiceCollection();
        services.AddScoped<IScopedOperation, ScopedOperation>();
        services.AddSingleton<GoodSingletonWithScopeFactory>();

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        var singleton = provider.GetRequiredService<GoodSingletonWithScopeFactory>();
        Guid id1 = singleton.ExecuteInScopedContext();
        Guid id2 = singleton.ExecuteInScopedContext();

        // Each invocation creates and disposes its own isolated scope
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public async Task EventService_AggregatesRepositoryAndAvailability_CorrectlyUsingMoq()
    {
        var mockRepo = new Mock<IEventRepository>();
        var mockAvailability = new Mock<IAvailabilityClient>();

        mockRepo.Setup(r => r.GetEventByIdAsync(1001))
            .ReturnsAsync(new EventDto(1001, "Arsenal vs Chelsea", 150m, 10));

        mockAvailability.Setup(a => a.CheckRealtimeSeatsAsync(1001))
            .ReturnsAsync(37);

        var service = new EventService(mockRepo.Object, mockAvailability.Object);
        var result = await service.GetEventAsync(1001);

        Assert.NotNull(result);
        Assert.Equal(1001, result.Id);
        Assert.Equal("Arsenal vs Chelsea", result.Title);
        Assert.Equal(37, result.AvailableSeats); // Updated from realtime client!

        mockRepo.Verify(r => r.GetEventByIdAsync(1001), Times.Once);
        mockAvailability.Verify(a => a.CheckRealtimeSeatsAsync(1001), Times.Once);
    }
}
