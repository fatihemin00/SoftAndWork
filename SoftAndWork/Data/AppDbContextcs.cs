using Microsoft.EntityFrameworkCore;
using SoftAndWork.Models;

namespace SoftAndWork.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<Material> Materials { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<StudySession> StudySessions { get; set; }
        public DbSet<SessionResponse> SessionResponses { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Course>()
                .HasOne(c => c.User)
                .WithMany(u => u.Courses)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SessionResponse>()
                .HasOne(sr => sr.StudySession)
                .WithMany(s => s.Responses)
                .HasForeignKey(sr => sr.StudySessionId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}