using SSW_x_Vonage_Clean_Architecture.Domain.Heroes;
using SSW_x_Vonage_Clean_Architecture.Domain.Teams;
using Vogen;

namespace SSW_x_Vonage_Clean_Architecture.Infrastructure.Persistence.Configuration;

[EfCoreConverter<TeamId>]
[EfCoreConverter<HeroId>]
[EfCoreConverter<MissionId>]
internal sealed partial class VogenEfCoreConverters;