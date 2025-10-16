using Mono.Cecil;

namespace SSW_x_Vonage_Clean_Architecture.Architecture.UnitTests.Common;

public class IsNotEnumRule : ICustomRule
{
    public bool MeetsRule(TypeDefinition type) => !type.IsEnum;
}