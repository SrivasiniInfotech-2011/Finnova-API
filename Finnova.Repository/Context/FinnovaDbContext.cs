using Microsoft.EntityFrameworkCore;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Context;

public class FinnovaDbContext : DbContext
{
    public FinnovaDbContext(DbContextOptions<FinnovaDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<LookupValue> LookupValues => Set<LookupValue>();
    public DbSet<Nationality> Nationalities => Set<Nationality>();
    public DbSet<NationalityAuditEntry> NationalityAuditEntries => Set<NationalityAuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinnovaDbContext).Assembly);
    }
}
