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
    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();
    public DbSet<CandidateEducation> CandidateEducations => Set<CandidateEducation>();
    public DbSet<AdmissionApplication> AdmissionApplications => Set<AdmissionApplication>();
    public DbSet<ApplicationScore> ApplicationScores => Set<ApplicationScore>();
    public DbSet<ApplicationStatusTransition> ApplicationStatusTransitions => Set<ApplicationStatusTransition>();
    public DbSet<ApplicationStatusHistory> ApplicationStatusHistories => Set<ApplicationStatusHistory>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Email là định danh đăng nhập; ràng buộc cả ở database để tránh đăng ký trùng đồng thời.
        builder.Entity<ApplicationUser>()
            .HasIndex(user => user.NormalizedEmail)
            .HasDatabaseName("EmailIndex")
            .IsUnique()
            .HasFilter("[NormalizedEmail] IS NOT NULL");

        builder.Entity<CandidateProfile>(entity =>
        {
            entity.Property(x => x.FullName).HasMaxLength(100);
            entity.Property(x => x.DateOfBirth).HasColumnType("date");
            entity.Property(x => x.PhoneNumber).IsUnicode(false).HasMaxLength(10);
            entity.Property(x => x.ContactEmail).HasMaxLength(256);
            entity.HasOne<ApplicationUser>().WithOne()
                .HasForeignKey<CandidateProfile>(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Education).WithOne()
                .HasForeignKey<CandidateEducation>(x => x.CandidateProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<CandidateEducation>()
            .Property(x => x.SchoolName).HasMaxLength(200);

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



        // ===== S3-01: hồ sơ đăng ký xét tuyển =====
        builder.Entity<AdmissionApplication>(entity =>
        {
            entity.HasIndex(a => a.ApplicationCode).IsUnique();   // mã hồ sơ không được trùng
            entity.Property(a => a.ApplicationCode).HasMaxLength(30).IsRequired();
            entity.Property(a => a.FullName).HasMaxLength(100);
            entity.Property(a => a.DateOfBirth).HasColumnType("date");
            entity.Property(a => a.PhoneNumber).IsUnicode(false).HasMaxLength(10);
            entity.Property(a => a.ContactEmail).HasMaxLength(256);
            entity.Property(a => a.SchoolName).HasMaxLength(200);
            // Một thí sinh không nộp trùng cùng một nguyện vọng (cùng ngành + phương thức + đợt)
            entity.HasIndex(a => new { a.UserId, a.RoundMajorMethodId }).IsUnique();
            entity.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(a => a.RoundMajorMethod).WithMany()
                .HasForeignKey(a => a.RoundMajorMethodId)
                .OnDelete(DeleteBehavior.Restrict);   // không cho xóa cấu hình đã có hồ sơ
            entity.HasOne(a => a.Status).WithMany()
                .HasForeignKey(a => a.StatusId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApplicationScore>(entity =>
        {
            entity.Property(x => x.FieldKey).HasMaxLength(50).IsRequired();
            entity.Property(x => x.FieldLabel).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Value).HasPrecision(7, 2);
            entity.HasIndex(x => new { x.ApplicationId, x.FieldKey }).IsUnique();
            entity.HasOne(x => x.Application).WithMany(a => a.Scores)
                .HasForeignKey(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ===== S4-01: chuyển trạng thái và lịch sử xử lý hồ sơ =====
        builder.Entity<ApplicationStatusTransition>(entity =>
        {
            entity.HasKey(t => new { t.FromStatusId, t.ToStatusId });   // mỗi cặp (Từ, Đến) chỉ có một dòng
            entity.HasOne(t => t.FromStatus).WithMany()
                .HasForeignKey(t => t.FromStatusId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(t => t.ToStatus).WithMany()
                .HasForeignKey(t => t.ToStatusId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApplicationStatusHistory>(entity =>
        {
            entity.Property(h => h.Note).HasMaxLength(500);
            entity.HasIndex(h => new { h.ApplicationId, h.ChangedAt });   // xem lịch sử của một hồ sơ theo thời gian
            entity.HasOne(h => h.Application).WithMany()
                .HasForeignKey(h => h.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(h => h.FromStatus).WithMany()
                .HasForeignKey(h => h.FromStatusId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(h => h.ToStatus).WithMany()
                .HasForeignKey(h => h.ToStatusId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(h => h.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);   // không xóa được tài khoản cán bộ đã xử lý hồ sơ
        });
    }
}
