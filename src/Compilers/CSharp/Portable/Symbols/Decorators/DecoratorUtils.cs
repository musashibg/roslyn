using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    internal static class DecoratorUtils
    {
        public static bool CheckTypeIsAssignableFrom(TypeSymbol targetType, TypeSymbol sourceType)
        {
            Debug.Assert(targetType != null && sourceType != null);

            // If the types conincide, they are trivially assignable to each other
            if (targetType == sourceType)
            {
                return true;
            }

            // If the source type is void, it is not assignable to any other type, including object
            if (sourceType.SpecialType == SpecialType.System_Void)
            {
                return false;
            }

            if (targetType.Kind == SymbolKind.NamedType)
            {
                if (targetType.IsInterfaceType())
                {
                    // If the target type is an interface, the source type is assignable to it if it implements it
                    if (sourceType.AllInterfaces.Contains((NamedTypeSymbol)targetType))
                    {
                        return true;
                    }
                }
                else
                {
                    // If the source type is derived from the target type, it is assignable to it
                    HashSet<DiagnosticInfo> useSiteDiagnostics = null;
                    if (sourceType.IsDerivedFrom(targetType, false, ref useSiteDiagnostics))
                    {
                        return true;
                    }
                }
            }

            // TODO: Consider conversions and interface covariance and contravariance?

            return false;
        }
    }
}
