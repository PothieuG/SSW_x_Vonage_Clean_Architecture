using SSW_x_Vonage_Clean_Architecture.Domain.Heroes;
using SSW_x_Vonage_Clean_Architecture.Domain.Teams;

namespace SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Hero> Heroes { get; }
    DbSet<Team> Teams { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}