using BookmarkFeeder.WebApi.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace BookmarkFeeder.WebApi.Tests.Infrastructure;

public static class TestCurrentUser
{
    /// <summary>A stable owner for tests that only need "some user", not a specific one.</summary>
    public static readonly Guid DefaultOwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// Registers a pre-resolved ICurrentUserContext for tests that build their own
    /// ServiceCollection. BookmarkDbContext requires one, and without it the factory throws
    /// "Unable to resolve service for type ICurrentUserContext" the moment a context is created.
    ///
    /// Registered as a SINGLETON rather than scoped, because these tests resolve the factory
    /// straight from the root provider. In the real app it is scoped, one owner per request.
    /// </summary>
    public static IServiceCollection AddTestCurrentUser(this IServiceCollection services, Guid? ownerId = null)
    {
        var context = new CurrentUserContext();
        context.Set(ownerId ?? DefaultOwnerId);
        services.AddSingleton<ICurrentUserContext>(context);
        return services;
    }
}
