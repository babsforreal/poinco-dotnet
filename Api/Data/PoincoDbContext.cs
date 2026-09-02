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

            // Punch.CompanyId était une colonne libre : rien en base n'empêchait un punch de
            // pointer vers une entreprise inexistante (ou de survivre à sa suppression). Pas de
            // navigation Company sur Punch, donc FK shadow — même Restrict que Employee -> Company.
            entity.HasOne<Company>()
                  .WithMany()
                  .HasForeignKey(p => p.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(p => new { p.EmployeeId, p.PunchedAt });

            // (CompanyId, PunchedAt DESC) couvre exactement le GetAll de PunchesController
            // (Where CompanyId + OrderByDescending PunchedAt) : avec le simple index sur
            // CompanyId, le tri forçait un tri complet des punchs de l'entreprise à chaque page.
            // Commençant par CompanyId, il sert aussi d'index de couverture pour la FK ci-dessus.
            entity.HasIndex(p => new { p.CompanyId, p.PunchedAt })
                  .IsDescending(false, true);
        });
        modelBuilder.Entity<Admin>(entity =>
        {
            // Unicité GLOBALE (pas scopée par CompanyId) : AuthController.Login résout l'admin
            // par email seul, sans tenant dans la requête — scoper l'index rendrait cette
            // résolution ambiguë entre entreprises. Le filtre, lui, doit matcher le HasQueryFilter
            // ci-dessous, comme pour les index Employee : sans lui, un email libéré par soft-delete
            // reste bloqué en base alors qu'il n'apparaît plus dans aucune requête normale.
            entity.HasIndex(a => a.Email)
                  .IsUnique()
                  .HasFilter("[DeletedAt] IS NULL");
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