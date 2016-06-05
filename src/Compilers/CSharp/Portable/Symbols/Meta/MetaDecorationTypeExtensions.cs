using Microsoft.CodeAnalysis.CSharp.Meta;
using Roslyn.Utilities;
using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.CSharp.Symbols.Meta
{
    internal static class MetaDecorationTypeExtensions
    {
        public static SourceMemberMethodSymbol FindDecoratorMethod(this SourceNamedTypeSymbol decoratorType, DecoratedMemberKind targetMemberKind)
        {
            CSharpCompilation compilation = decoratorType.DeclaringCompilation;
            ImmutableArray<Symbol> candidateMethods;
            switch (targetMemberKind)
            {
                case DecoratedMemberKind.Constructor:
                    candidateMethods = decoratorType.GetMembers("DecorateConstructor");
                    foreach (SourceMemberMethodSymbol method in candidateMethods)
                    {
                        if (method.ContainingType == decoratorType
                            && method.Arity == 0
                            && method.IsOverride
                            && method.ParameterCount == 3
                            && method.Parameters[0].Type == compilation.GetWellKnownType(WellKnownType.System_Reflection_ConstructorInfo)
                            && method.Parameters[1].Type.IsObjectType()
                            && method.Parameters[2].Type.IsArray()
                            && ((ArrayTypeSymbol)method.Parameters[2].Type).ElementType.IsObjectType()
                            && method.GetConstructedLeastOverriddenMethod(decoratorType).ContainingType == compilation.GetWellKnownType(WellKnownType.CSharp_Meta_Decorator))
                        {
                            return method;
                        }
                    }
                    return null;

                case DecoratedMemberKind.Destructor:
                    candidateMethods = decoratorType.GetMembers("DecorateDestructor");
                    foreach (SourceMemberMethodSymbol method in candidateMethods)
                    {
                        if (method.ContainingType == decoratorType
                            && method.Arity == 0
                            && method.IsOverride
                            && method.ParameterCount == 2
                            && method.Parameters[0].Type == compilation.GetWellKnownType(WellKnownType.System_Reflection_MethodInfo)
                            && method.Parameters[1].Type.IsObjectType()
                            && method.GetConstructedLeastOverriddenMethod(decoratorType).ContainingType == compilation.GetWellKnownType(WellKnownType.CSharp_Meta_Decorator))
                        {
                            return method;
                        }
                    }
                    return null;

                case DecoratedMemberKind.IndexerGet:
                    candidateMethods = decoratorType.GetMembers("DecorateIndexerGet");
                    foreach (SourceMemberMethodSymbol method in candidateMethods)
                    {
                        if (method.ContainingType == decoratorType
                            && method.Arity == 0
                            && method.IsOverride
                            && method.ParameterCount == 3
                            && method.Parameters[0].Type == compilation.GetWellKnownType(WellKnownType.System_Reflection_PropertyInfo)
                            && method.Parameters[1].Type.IsObjectType()
                            && method.Parameters[2].Type.IsArray()
                            && ((ArrayTypeSymbol)method.Parameters[2].Type).ElementType.IsObjectType()
                            && method.GetConstructedLeastOverriddenMethod(decoratorType).ContainingType == compilation.GetWellKnownType(WellKnownType.CSharp_Meta_Decorator))
                        {
                            return method;
                        }
                    }
                    return null;

                case DecoratedMemberKind.IndexerSet:
                    candidateMethods = decoratorType.GetMembers("DecorateIndexerSet");
                    foreach (SourceMemberMethodSymbol method in candidateMethods)
                    {
                        if (method.ContainingType == decoratorType
                            && method.Arity == 0
                            && method.IsOverride
                            && method.ParameterCount == 4
                            && method.Parameters[0].Type == compilation.GetWellKnownType(WellKnownType.System_Reflection_PropertyInfo)
                            && method.Parameters[1].Type.IsObjectType()
                            && method.Parameters[2].Type.IsObjectType()
                            && method.Parameters[3].Type.IsArray()
                            && ((ArrayTypeSymbol)method.Parameters[3].Type).ElementType.IsObjectType()
                            && method.GetConstructedLeastOverriddenMethod(decoratorType).ContainingType == compilation.GetWellKnownType(WellKnownType.CSharp_Meta_Decorator))
                        {
                            return method;
                        }
                    }
                    return null;

                case DecoratedMemberKind.Method:
                    candidateMethods = decoratorType.GetMembers("DecorateMethod");
                    foreach (SourceMemberMethodSymbol method in candidateMethods)
                    {
                        if (method.ContainingType == decoratorType
                            && method.Arity == 0
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

                case DecoratedMemberKind.PropertyGet:
                    candidateMethods = decoratorType.GetMembers("DecoratePropertyGet");
                    foreach (SourceMemberMethodSymbol method in candidateMethods)
                    {
                        if (method.ContainingType == decoratorType
                            && method.Arity == 0
                            && method.IsOverride
                            && method.ParameterCount == 2
                            && method.Parameters[0].Type == compilation.GetWellKnownType(WellKnownType.System_Reflection_PropertyInfo)
                            && method.Parameters[1].Type.IsObjectType()
                            && method.GetConstructedLeastOverriddenMethod(decoratorType).ContainingType == compilation.GetWellKnownType(WellKnownType.CSharp_Meta_Decorator))
                        {
                            return method;
                        }
                    }
                    return null;

                case DecoratedMemberKind.PropertySet:
                    candidateMethods = decoratorType.GetMembers("DecoratePropertySet");
                    foreach (SourceMemberMethodSymbol method in candidateMethods)
                    {
                        if (method.ContainingType == decoratorType
                            && method.Arity == 0
                            && method.IsOverride
                            && method.ParameterCount == 3
                            && method.Parameters[0].Type == compilation.GetWellKnownType(WellKnownType.System_Reflection_PropertyInfo)
                            && method.Parameters[1].Type.IsObjectType()
                            && method.Parameters[2].Type.IsObjectType()
                            && method.GetConstructedLeastOverriddenMethod(decoratorType).ContainingType == compilation.GetWellKnownType(WellKnownType.CSharp_Meta_Decorator))
                        {
                            return method;
                        }
                    }
                    return null;

                default:
                    throw ExceptionUtilities.Unreachable;
            }
        }

        public static SourceMemberMethodSymbol FindMetaclassApplicationMethod(this SourceNamedTypeSymbol metaclassType)
        {
            CSharpCompilation compilation = metaclassType.DeclaringCompilation;
            ImmutableArray<Symbol> candidateMethods = metaclassType.GetMembers("ApplyToType");
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
