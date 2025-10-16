using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSW_x_Vonage_Clean_Architecture.Domain.Common.Interfaces;
using SSW_x_Vonage_Clean_Architecture.Domain.Teams;

namespace SSW_x_Vonage_Clean_Architecture.Infrastructure.Persistence.Configuration;

public class MissionConfiguration : AuditableConfiguration<Mission>
{
    public override void PostConfigure(EntityTypeBuilder<Mission> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Description)
            .HasMaxLength(Mission.DescriptionMaxLength)
            .IsRequired();
    }
}