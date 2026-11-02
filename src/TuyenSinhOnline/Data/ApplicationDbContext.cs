using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TuyenSinhOnline.Models;

namespace TuyenSinhOnline.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // 1. Các bảng nghiệp vụ tuyển sinh (Phục vụ US-03 và các phần sau)
    public virtual DbSet<AdmissionMethod> AdmissionMethods { get; set; }
    public virtual DbSet<AdmissionRound> AdmissionRounds { get; set; }
    public virtual DbSet<ApplicationStatus> ApplicationStatuses { get; set; }
    public virtual DbSet<Major> Majors { get; set; }
    public virtual DbSet<RoundMajor> RoundMajors { get; set; }
    public virtual DbSet<RoundMajorMethod> RoundMajorMethods { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Cấu hình ràng buộc cho User của Identity
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasIndex(e => e.NormalizedEmail, "EmailIndex")
                .IsUnique()
                .HasFilter("([NormalizedEmail] IS NOT NULL)");

            entity.HasIndex(e => e.NormalizedUserName, "UserNameIndex")
                .IsUnique()
                .HasFilter("([NormalizedUserName] IS NOT NULL)");

            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.NormalizedEmail).HasMaxLength(256);
            entity.Property(e => e.NormalizedUserName).HasMaxLength(256);
            entity.Property(e => e.UserName).HasMaxLength(256);
        });

        // Cấu hình ràng buộc cho bảng AdmissionMethods
        builder.Entity<AdmissionMethod>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Admissio__3214EC075A67711C");
            entity.HasIndex(e => e.Code, "UQ_AdmissionMethods_Code").IsUnique();
            entity.Property(e => e.Code).HasMaxLength(30);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(200);
        });

        // Cấu hình ràng buộc cho bảng AdmissionRounds
        builder.Entity<AdmissionRound>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Admissio__3214EC071E0265CA");
            entity.HasIndex(e => e.Code, "UQ_AdmissionRounds_Code").IsUnique();
            entity.Property(e => e.Code).HasMaxLength(30);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(200);
        });

        // Cấu hình ràng buộc cho bảng ApplicationStatuses
        builder.Entity<ApplicationStatus>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Applicat__3214EC07D4936CAD");
            entity.HasIndex(e => e.Code, "UQ_ApplicationStatuses_Code").IsUnique();
            entity.Property(e => e.Code).HasMaxLength(30);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        // Cấu hình ràng buộc cho bảng Majors
        builder.Entity<Major>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Majors__3214EC0790908F8A");
            entity.HasIndex(e => e.Code, "UQ_Majors_Code").IsUnique();
            entity.Property(e => e.Code).HasMaxLength(20);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(200);
        });

        // Cấu hình quan hệ RoundMajors
        builder.Entity<RoundMajor>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__RoundMaj__3214EC074183A782");
            entity.HasIndex(e => new { e.RoundId, e.MajorId }, "UQ_RoundMajors_Round_Major").IsUnique();
            entity.Property(e => e.IsEnabled).HasDefaultValue(true);

            entity.HasOne(d => d.Major).WithMany(p => p.RoundMajors)
                .HasForeignKey(d => d.MajorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RoundMajors_Majors");

            entity.HasOne(d => d.Round).WithMany(p => p.RoundMajors)
                .HasForeignKey(d => d.RoundId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RoundMajors_AdmissionRounds");
        });

        // Cấu hình quan hệ RoundMajorMethods
        builder.Entity<RoundMajorMethod>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__RoundMaj__3214EC07C92E492B");
            entity.HasIndex(e => new { e.RoundMajorId, e.MethodId }, "UQ_RoundMajorMethods").IsUnique();
            entity.Property(e => e.IsEnabled).HasDefaultValue(true);

            entity.HasOne(d => d.Method).WithMany(p => p.RoundMajorMethods)
                .HasForeignKey(d => d.MethodId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RoundMajorMethods_AdmissionMethods");

            entity.HasOne(d => d.RoundMajor).WithMany(p => p.RoundMajorMethods)
                .HasForeignKey(d => d.RoundMajorId)
                .HasConstraintName("FK_RoundMajorMethods_RoundMajors");
        });
    }
}