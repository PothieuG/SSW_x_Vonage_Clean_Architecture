using SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;
using SSW_x_Vonage_Clean_Architecture.Domain.Common.Base;
using SSW_x_Vonage_Clean_Architecture.Infrastructure.Persistence;
using SSW_x_Vonage_Clean_Architecture.WebApi;
using System.Reflection;

namespace SSW_x_Vonage_Clean_Architecture.Architecture.UnitTests.Common;

public abstract class TestBase
{
    protected static readonly Assembly DomainAssembly = typeof(AggregateRoot<>).Assembly;
    protected static readonly Assembly ApplicationAssembly = typeof(IApplicationDbContext).Assembly;
    protected static readonly Assembly InfrastructureAssembly = typeof(ApplicationDbContext).Assembly;
    protected static readonly Assembly PresentationAssembly = typeof(IWebApiMarker).Assembly;
}