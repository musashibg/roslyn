namespace Microsoft.CodeAnalysis.CSharp.Symbols.Meta
{
    internal static class MetaDecorationTypeExtensions
    {
        public static SourceMemberMethodSymbol FindDecoratorMethod(this SourceNamedTypeSymbol decoratorType)
        {
            var candidateMethods = decoratorType.GetMembers("DecorateMethod");
            if (candidateMethods.Length == 0)
            {
                return null;
            }

            CSharpCompilation compilation = decoratorType.DeclaringCompilation;
            foreach (SourceMemberMethodSymbol method in candidateMethods)
            {
                if (method.Arity == 0
                    && method.IsOverride
                    && method.ParameterCount == 3
                    && method.Parameters[0].Type == compilation.GetWellKnownType(WellKnownType.System_Reflection_MethodInfo)
                    && method.Parameters[1].Type.IsObjectType()
                    && method.Parameters[2].Type.IsArray()
                    && ((ArrayTypeSymbol)method.Parameters[2].Type).ElementType.IsObjectType()
                    && method.GetConstructedLeastOverriddenMethod(decoratorType).ContainingType == compilation.GetWellKnownType(WellKnownType.CSharp_Meta_Decorator))
                {
                    return method;
                }
            }

            return null;
        }

        public static SourceMemberMethodSymbol FindMetaclassApplicationMethod(this SourceNamedTypeSymbol metaclassType)
        {
            var candidateMethods = metaclassType.GetMembers("ApplyToType");
            if (candidateMethods.Length == 0)
            {
                return null;
            }

            CSharpCompilation compilation = metaclassType.DeclaringCompilation;
            foreach (SourceMemberMethodSymbol method in candidateMethods)
            {
                if (method.Arity == 0
                    && method.IsOverride
                    && method.ParameterCount == 1
                    && method.Parameters[0].Type == compilation.GetWellKnownType(WellKnownType.System_Type)
                    && method.GetConstructedLeastOverriddenMethod(metaclassType).ContainingType == compilation.GetWellKnownType(WellKnownType.CSharp_Meta_Metaclass))
                {
                    return method;
                }
            }

            return null;
        }
    }
}
