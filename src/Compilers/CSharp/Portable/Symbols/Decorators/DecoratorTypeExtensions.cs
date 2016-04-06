namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    internal static class DecoratorTypeExtensions
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
    }
}
