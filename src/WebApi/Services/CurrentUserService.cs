using SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;
using System.Security.Claims;

namespace SSW_x_Vonage_Clean_Architecture.WebApi.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public string? UserId => httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
}