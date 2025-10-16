using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSW_x_Vonage_Clean_Architecture.Domain.Teams;

namespace SSW_x_Vonage_Clean_Architecture.Infrastructure.Persistence.Configuration;

public class TeamConfiguration : AuditableConfiguration<Team>
{
    public override void PostConfigure(EntityTypeBuilder<Team> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .HasMaxLength(Team.NameMaxLength)
            .IsRequired();

        builder.HasMany(t => t.Missions)
            .WithOne()
            .IsRequired();

        builder.HasMany(t => t.Heroes)
            .WithOne()
            .HasForeignKey(h => h.TeamId)
            .IsRequired(false);
    }
}