using Microsoft.EntityFrameworkCore;
using Api.Models;

namespace Api.Data;

public class PoincoDbContext : DbContext
{
    public PoincoDbContext(DbContextOptions<PoincoDbContext> options) : base(options) { }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Punch> Punches => Set<Punch>();
    public DbSet<Admin> Admins => Set<Admin>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasIndex(c => c.Slug).IsUnique();
        });
        modelBuilder.Entity<Employee>(entity =>
        {
            entity.Property(e => e.LastPunchType).HasConversion<string>();

            // Les filtres incluent [DeletedAt] IS NULL pour matcher le HasQueryFilter
            // ci-dessous : sans ça, un PIN/numéro/carte libéré par soft-delete reste
            // bloqué en base alors qu'il n'apparaît plus dans aucune requête normale.
            entity.HasIndex(e => new { e.CompanyId, e.Pin })
                  .IsUnique()
                  .HasFilter("[DeletedAt] IS NULL");

            entity.HasIndex(e => new { e.CompanyId, e.EmployeeNumber })
                  .IsUnique()
                  .HasFilter("[EmployeeNumber] IS NOT NULL AND [DeletedAt] IS NULL");

            entity.HasIndex(e => new { e.CompanyId, e.CardUid })
                  .IsUnique()
                  .HasFilter("[CardUid] IS NOT NULL AND [DeletedAt] IS NULL");

            entity.HasOne(e => e.Company)
                  .WithMany()
                  .HasForeignKey(e => e.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(e => e.DeletedAt == null);
        });
        modelBuilder.Entity<Punch>(entity =>
        {
            entity.Property(p => p.Type).HasConversion<string>();

            entity.HasOne(p => p.Employee)
                .WithMany(e => e.Punches)
                .HasForeignKey(p => p.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(p => new { p.EmployeeId, p.PunchedAt });
            entity.HasIndex(p => p.CompanyId);
        });
        modelBuilder.Entity<Admin>(entity =>
        {
            entity.HasIndex(a => a.Email).IsUnique();
            entity.HasQueryFilter(a => a.DeletedAt == null);
        });
    }
    public override int SaveChanges()
    {
        ApplyTimestamps();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<IHasTimestamps>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}