using DirectMediator;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

// -------------------------------------------------------------------
// Request types used only by CachingBehaviorTests.
// NO IRequestHandler implementation is defined for these types, so the
// source generator does NOT discover them and they do not affect the
// generated QueryDispatcher / Mediator constructors.
// -------------------------------------------------------------------
public record CacheableQuery(int Id) : ICacheableRequest<string>
{
    public string CacheKey => $"q:{Id}";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(10);
}

public record CacheableQueryWithNullDuration(int Id) : ICacheableRequest<string>
{
    public string CacheKey => $"qnull:{Id}";
    public TimeSpan? CacheDuration => null; // use CachingBehaviorOptions.DefaultDuration
}

public record NonCacheableTestRequest(int Id) : IRequest<string>;

// -------------------------------------------------------------------
// Tests exercise CachingBehavior<TRequest,TResponse> in isolation —
// no generated dispatchers needed.
// -------------------------------------------------------------------
public class CachingBehaviorTests
{
    private static (IMemoryCache cache, CachingBehavior<CacheableQuery, string> behavior)
        BuildBehavior(TimeSpan? defaultDuration = null)
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IMemoryCache>();
        var options = new CachingBehaviorOptions
        {
            DefaultDuration = defaultDuration ?? TimeSpan.FromMinutes(5)
        };
        return (cache, new CachingBehavior<CacheableQuery, string>(cache, options));
    }

    [Fact]
    public async Task CachingBehavior_CachesResponse_OnSecondCall()
    {
        var (_, behavior) = BuildBehavior();
        var callCount = 0;
        RequestHandlerDelegate<string> next = () => Task.FromResult($"result:{++callCount}");

        var request = new CacheableQuery(1);
        var first  = await behavior.Handle(request, default, next);
        var second = await behavior.Handle(request, default, next);

        Assert.Equal("result:1", first);
        Assert.Equal("result:1", second);   // served from cache
        Assert.Equal(1, callCount);          // handler invoked only once
    }

    [Fact]
    public async Task CachingBehavior_ReturnsFreshResult_ForDifferentKeys()
    {
        var (_, behavior) = BuildBehavior();
        var callCount = 0;
        RequestHandlerDelegate<string> next = () => Task.FromResult($"result:{++callCount}");

        var r1 = await behavior.Handle(new CacheableQuery(1), default, next);
        var r2 = await behavior.Handle(new CacheableQuery(2), default, next);

        Assert.Equal("result:1", r1);
        Assert.Equal("result:2", r2);
        Assert.Equal(2, callCount); // each distinct key calls the handler
    }

    [Fact]
    public async Task CachingBehavior_DoesNotCache_NonCacheableRequest()
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        var cache   = services.BuildServiceProvider().GetRequiredService<IMemoryCache>();
        var options = new CachingBehaviorOptions();
        var behavior = new CachingBehavior<NonCacheableTestRequest, string>(cache, options);

        var callCount = 0;
        RequestHandlerDelegate<string> next = () => Task.FromResult($"nc:{++callCount}");

        var request = new NonCacheableTestRequest(5);
        await behavior.Handle(request, default, next);
        await behavior.Handle(request, default, next);

        Assert.Equal(2, callCount); // handler invoked every time — no caching
    }

    [Fact]
    public async Task CachingBehavior_CanEvictEntry_ViaIMemoryCache()
    {
        var (cache, behavior) = BuildBehavior();
        var callCount = 0;
        RequestHandlerDelegate<string> next = () => Task.FromResult($"result:{++callCount}");

        var request = new CacheableQuery(7);

        await behavior.Handle(request, default, next);
        Assert.Equal(1, callCount);

        cache.Remove("q:7"); // manual eviction

        await behavior.Handle(request, default, next);
        Assert.Equal(2, callCount); // re-invoked after eviction
    }

    [Fact]
    public async Task CachingBehavior_UsesDefaultDuration_WhenRequestCacheDurationIsNull()
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        var cache   = services.BuildServiceProvider().GetRequiredService<IMemoryCache>();
        var options = new CachingBehaviorOptions { DefaultDuration = TimeSpan.FromMinutes(5) };
        var behavior = new CachingBehavior<CacheableQueryWithNullDuration, string>(cache, options);

        var callCount = 0;
        RequestHandlerDelegate<string> next = () => Task.FromResult($"result:{++callCount}");

        var request = new CacheableQueryWithNullDuration(1);
        await behavior.Handle(request, default, next);
        await behavior.Handle(request, default, next);

        Assert.Equal(1, callCount); // second call served from cache with default duration
    }

    [Fact]
    public void AddDirectMediatorCaching_RegistersCachingBehavior()
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddDirectMediatorCaching();

        var provider = services.BuildServiceProvider();
        var behaviors = provider.GetServices(typeof(IPipelineBehavior<CacheableQuery, string>));

        Assert.Contains(behaviors, b => b is CachingBehavior<CacheableQuery, string>);
    }

    [Fact]
    public void AddDirectMediatorCaching_RegistersCustomDefaultDuration()
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddDirectMediatorCaching(defaultCacheDuration: TimeSpan.FromSeconds(30));

        var provider = services.BuildServiceProvider();
        var opts = provider.GetRequiredService<CachingBehaviorOptions>();

        Assert.Equal(TimeSpan.FromSeconds(30), opts.DefaultDuration);
    }
}
