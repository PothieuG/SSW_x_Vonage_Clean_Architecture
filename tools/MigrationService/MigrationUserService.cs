using SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;

namespace MigrationService;

public class MigrationUserService : ICurrentUserService
{
    public string? UserId => "MigrationService";
}