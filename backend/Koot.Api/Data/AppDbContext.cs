using Koot.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Koot.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<AnswerOption> AnswerOptions => Set<AnswerOption>();
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<GameParticipant> GameParticipants => Set<GameParticipant>();
    public DbSet<GameAnswer> GameAnswers => Set<GameAnswer>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---- User ----
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();
        });

        // ---- Quiz ----
        modelBuilder.Entity<Quiz>(e =>
        {
            e.ToTable("quizzes");
            e.HasIndex(q => q.UserId);

            e.HasOne(q => q.User)
                .WithMany(u => u.Quizzes)
                .HasForeignKey(q => q.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Question ----
        modelBuilder.Entity<Question>(e =>
        {
            e.ToTable("questions");
            e.HasIndex(q => new { q.QuizId, q.OrderIndex });

            e.Property(q => q.Type)
                .HasConversion<int>();

            e.HasOne(q => q.Quiz)
                .WithMany(qz => qz.Questions)
                .HasForeignKey(q => q.QuizId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- AnswerOption ----
        modelBuilder.Entity<AnswerOption>(e =>
        {
            e.ToTable("answer_options");
            e.HasIndex(a => new { a.QuestionId, a.OrderIndex });

            e.HasOne(a => a.Question)
                .WithMany(q => q.AnswerOptions)
                .HasForeignKey(a => a.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- GameSession ----
        modelBuilder.Entity<GameSession>(e =>
        {
            e.ToTable("game_sessions");
            e.HasIndex(s => s.Code).IsUnique();
            e.HasIndex(s => s.Status);

            // Covers the host history query:
            //   WHERE HostUserId = ? AND Status = Finished
            //   ORDER BY EndedAt DESC
            e.HasIndex(s => new { s.HostUserId, s.Status, s.EndedAt })
                .IsDescending(false, false, true)
                .HasDatabaseName("IX_game_sessions_HostUserId_Status_EndedAt");

            e.Property(s => s.Status)
                .HasConversion<int>();

            e.HasOne(s => s.Quiz)
                .WithMany(q => q.Sessions)
                .HasForeignKey(s => s.QuizId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(s => s.HostUser)
                .WithMany(u => u.HostedSessions)
                .HasForeignKey(s => s.HostUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- GameParticipant ----
        modelBuilder.Entity<GameParticipant>(e =>
        {
            e.ToTable("game_participants");
            e.HasIndex(p => p.SessionId);
            e.HasIndex(p => new { p.SessionId, p.Nickname }).IsUnique();

            e.HasOne(p => p.Session)
                .WithMany(s => s.Participants)
                .HasForeignKey(p => p.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- RefreshToken ----
        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");

            // Fast lookup by hash on every token validation
            e.HasIndex(t => t.TokenHash).IsUnique();

            // Efficient query for active tokens per user
            e.HasIndex(t => new { t.UserId, t.RevokedAt });

            e.HasOne(t => t.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Self-referencing FK: a rotated token points to its replacement
            e.HasOne(t => t.ReplacedByToken)
                .WithMany()
                .HasForeignKey(t => t.ReplacedByTokenId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- PasswordResetToken ----
        modelBuilder.Entity<PasswordResetToken>(e =>
        {
            e.ToTable("password_reset_tokens");

            // Fast lookup by hash on every reset attempt
            e.HasIndex(t => t.TokenHash).IsUnique();

            // Efficient query for outstanding tokens per user
            e.HasIndex(t => new { t.UserId, t.UsedAt });

            e.HasOne(t => t.User)
                .WithMany(u => u.PasswordResetTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- GameAnswer ----
        modelBuilder.Entity<GameAnswer>(e =>
        {
            e.ToTable("game_answers");
            // The (SessionId, QuestionId) composite below covers SessionId-only
            // lookups, so a standalone SessionId index would be redundant.
            e.HasIndex(a => a.ParticipantId);
            e.HasIndex(a => a.QuestionId);
            e.HasIndex(a => new { a.ParticipantId, a.QuestionId }).IsUnique();

            // Covers per-question aggregation in the session-detail endpoint:
            //   WHERE SessionId = ? GROUP BY QuestionId ...
            e.HasIndex(a => new { a.SessionId, a.QuestionId })
                .HasDatabaseName("IX_game_answers_SessionId_QuestionId");

            e.HasOne(a => a.Session)
                .WithMany(s => s.Answers)
                .HasForeignKey(a => a.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(a => a.Participant)
                .WithMany(p => p.Answers)
                .HasForeignKey(a => a.ParticipantId)
                .OnDelete(DeleteBehavior.Cascade);

            // Avoid multiple cascade paths through Question; rely on session cascade.
            e.HasOne(a => a.Question)
                .WithMany(q => q.GameAnswers)
                .HasForeignKey(a => a.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.AnswerOption)
                .WithMany(o => o.GameAnswers)
                .HasForeignKey(a => a.AnswerOptionId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
