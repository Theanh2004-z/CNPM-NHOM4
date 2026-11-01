using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TuyenSinhOnline.Models;

namespace TuyenSinhOnline.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Major> Majors => Set<Major>();
    public DbSet<AdmissionRound> AdmissionRounds => Set<AdmissionRound>();
    public DbSet<AdmissionMethod> AdmissionMethods => Set<AdmissionMethod>();
    public DbSet<RoundMajor> RoundMajors => Set<RoundMajor>();
    public DbSet<RoundMajorMethod> RoundMajorMethods => Set<RoundMajorMethod>();
    public DbSet<ApplicationStatus> ApplicationStatuses => Set<ApplicationStatus>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Email là định danh đăng nhập; ràng buộc cả ở database để tránh đăng ký trùng đồng thời.
        builder.Entity<ApplicationUser>()
            .HasIndex(user => user.NormalizedEmail)
            .HasDatabaseName("EmailIndex")
            .IsUnique()
            .HasFilter("[NormalizedEmail] IS NOT NULL");

        builder.Entity<Major>(entity =>
        {
            entity.HasIndex(m => m.Code).IsUnique();
            entity.Property(m => m.Code).HasMaxLength(20).IsRequired();
            entity.Property(m => m.Name).HasMaxLength(200).IsRequired();
            entity.Property(m => m.Description).HasMaxLength(1000);
        });

        builder.Entity<AdmissionRound>(entity =>
        {
            entity.HasIndex(r => r.Code).IsUnique();
            entity.Property(r => r.Code).HasMaxLength(30).IsRequired();
            entity.Property(r => r.Name).HasMaxLength(200).IsRequired();
        });

        builder.Entity<AdmissionMethod>(entity =>
        {
            entity.HasIndex(m => m.Code).IsUnique();
            entity.Property(m => m.Code).HasMaxLength(30).IsRequired();
            entity.Property(m => m.Name).HasMaxLength(200).IsRequired();
        });

        builder.Entity<RoundMajor>(entity =>
        {
            entity.HasIndex(rm => new { rm.RoundId, rm.MajorId }).IsUnique();
            entity.HasOne(rm => rm.Round)
                .WithMany(r => r.RoundMajors)
                .HasForeignKey(rm => rm.RoundId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(rm => rm.Major)
                .WithMany(m => m.RoundMajors)
                .HasForeignKey(rm => rm.MajorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RoundMajorMethod>(entity =>
        {
            entity.HasIndex(rmm => new { rmm.RoundMajorId, rmm.MethodId }).IsUnique();
            entity.HasOne(rmm => rmm.RoundMajor)
                .WithMany(rm => rm.RoundMajorMethods)
                .HasForeignKey(rmm => rmm.RoundMajorId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(rmm => rmm.Method)
                .WithMany(m => m.RoundMajorMethods)
                .HasForeignKey(rmm => rmm.MethodId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApplicationStatus>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Code).HasMaxLength(30).IsRequired();
            entity.Property(s => s.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(s => s.Code).IsUnique();
        });
    }
}
