using Microsoft.EntityFrameworkCore;
using Sub.Api.Models;

namespace Sub.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<ConnectedEmailAccount> ConnectedEmailAccounts =>
        Set<ConnectedEmailAccount>();

    public DbSet<Subscription> Subscriptions =>
        Set<Subscription>();

    public DbSet<SubscriptionEvidence> SubscriptionEvidences =>
        Set<SubscriptionEvidence>();

    public DbSet<CancellationRequest> CancellationRequests =>
        Set<CancellationRequest>();
}