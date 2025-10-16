using SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;
using SSW_x_Vonage_Clean_Architecture.Architecture.UnitTests.Common;
using SSW_x_Vonage_Clean_Architecture.Infrastructure.Persistence;

namespace SSW_x_Vonage_Clean_Architecture.Architecture.UnitTests;

public class Presentation : TestBase
{
    private static readonly Type IDbContext = typeof(IApplicationDbContext);
    private static readonly Type DbContext = typeof(ApplicationDbContext);

    [Fact]
    public void Endpoints_ShouldNotReferenceDbContext()
    {
        var types = Types
            .InAssembly(PresentationAssembly)
            .That()
            .HaveNameEndingWith("Endpoints");

        var result = types
            .ShouldNot()
            .HaveDependencyOnAny(DbContext.FullName, IDbContext.FullName)
            .GetResult();

        result.Should().BeSuccessful();
    }
}