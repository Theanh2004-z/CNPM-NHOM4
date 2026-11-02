using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TuyenSinhOnline.Models;

namespace TuyenSinhOnline.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
<<<<<<< HEAD
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
=======
    public DbSet<Major> Majors => Set<Major>();
    public DbSet<AdmissionRound> AdmissionRounds => Set<AdmissionRound>();
    public DbSet<AdmissionMethod> AdmissionMethods => Set<AdmissionMethod>();
    public DbSet<RoundMajor> RoundMajors => Set<RoundMajor>();
    public DbSet<RoundMajorMethod> RoundMajorMethods => Set<RoundMajorMethod>();
    public DbSet<ApplicationStatus> ApplicationStatuses => Set<ApplicationStatus>();
>>>>>>> 51e7280e981fafafc42265ebe3032ba15f2dc522

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

<<<<<<< HEAD
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
=======
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
>>>>>>> 51e7280e981fafafc42265ebe3032ba15f2dc522
        });
    }
}