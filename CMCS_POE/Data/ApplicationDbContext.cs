using CMCS_POE.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace CMCS_POE.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Claim> Claims { get; set; }
        public DbSet<Lecturer> Lecturers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicationUser>(user =>
            {
                user.Property(u => u.DisplayName).HasMaxLength(100);
                user.HasIndex(u => u.NormalizedEmail).IsUnique();
                user.HasOne<Lecturer>().WithOne().HasForeignKey<ApplicationUser>(u => u.LecturerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Claim>().Property(c => c.Status).IsConcurrencyToken();
            modelBuilder.Entity<Claim>().Property(c => c.IsPaid).IsConcurrencyToken();

            modelBuilder.Entity<Claim>()
                .HasOne(c => c.Lecturer)
                .WithMany(l => l.Claims)
                .HasForeignKey(c => c.LecturerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Lecturer>().HasData(
                new Lecturer
                {
                    LecturerId = 1,
                    FullName = "Dr. John Smith",
                    Email = "john.smith@university.ac.za",
                    Department = "Computer Science",
                    StandardHourlyRate = 250,
                    PhoneNumber = "021-555-0001",
                    IsActive = true
                },
                new Lecturer
                {
                    LecturerId = 2,
                    FullName = "Prof. Jane Doe",
                    Email = "jane.doe@university.ac.za",
                    Department = "Mathematics",
                    StandardHourlyRate = 300,
                    PhoneNumber = "021-555-0002",
                    IsActive = true
                },
                new Lecturer
                {
                    LecturerId = 3,
                    FullName = "Dr. Bob Wilson",
                    Email = "bob.wilson@university.ac.za",
                    Department = "Engineering",
                    StandardHourlyRate = 280,
                    PhoneNumber = "021-555-0003",
                    IsActive = true
                }
            );
        }
    }
}
