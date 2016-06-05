using Microsoft.CodeAnalysis.CSharp.Symbols;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal static class MetaUtils
    {
        #region Simple special types

        private static readonly ImmutableHashSet<SpecialType> _simpleSpecialTypes = ImmutableHashSet.Create(
            SpecialType.System_Boolean,
            SpecialType.System_Byte,
            SpecialType.System_Char,
            SpecialType.System_Decimal,
            SpecialType.System_Double,
            SpecialType.System_Int16,
            SpecialType.System_Int32,
            SpecialType.System_Int64,
            SpecialType.System_SByte,
            SpecialType.System_Single,
            SpecialType.System_String,
            SpecialType.System_UInt16,
            SpecialType.System_UInt32,
            SpecialType.System_UInt64);

        #endregion

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

            // Any non-void source type is assignable to object
            if (targetType.IsObjectType())
            {
                return true;
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

        public static bool CheckTypeIsGeneric(TypeSymbol type)
        {
            return (type.Kind == SymbolKind.NamedType || type.Kind == SymbolKind.ErrorType) && !type.GetMemberTypeParameters().IsEmpty;
        }

        public static bool CheckTypeIsVisible(TypeSymbol type)
        {
            if (type.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }

            return type.ContainingType == null || CheckTypeIsVisible(type.ContainingType);
        }

        public static bool CheckIsSimpleStaticValueType(TypeSymbol type, CSharpCompilation compilation)
        {
            if (_simpleSpecialTypes.Contains(type.SpecialType))
            {
                return true;
            }
            if (type == compilation.GetWellKnownType(WellKnownType.System_Type))
            {
                return true;
            }
            if (type.IsEnumType())
            {
                return true;
            }
            return false;
        }

        public static string GetTypeFullName(TypeSymbol type)
        {
            // TODO: Handle generic type arguments
            TypeSymbol containingType = type.ContainingType;
            if (containingType == null)
            {
                NamespaceSymbol @namespace = type.ContainingNamespace;
                if (@namespace == null)
                {
                    return type.MetadataName;
                }
                else
                {
                    return $"{GetNamespaceFullName(@namespace)}.{type.MetadataName}";
                }
            }
            else
            {
                return $"{GetTypeFullName(containingType)}+{type.MetadataName}";
            }
        }

        public static string GetNamespaceFullName(NamespaceSymbol @namespace)
        {
            NamespaceSymbol containingNamespace = @namespace.ContainingNamespace;
            if (containingNamespace == null || string.IsNullOrEmpty(containingNamespace.Name))
            {
                return @namespace.Name;
            }
            else
            {
                return $"{GetNamespaceFullName(containingNamespace)}.{@namespace.Name}";
            }
        }

        public static BoundExpression ConvertIfNeeded(TypeSymbol targetType, BoundExpression sourceExpression, CSharpCompilation compilation)
        {
            if (sourceExpression.Type == targetType)
            {
                return sourceExpression;
            }

            if (sourceExpression.Kind == BoundKind.Conversion)
            {
                var conversion = (BoundConversion)sourceExpression;
                ConversionKind conversionKind = conversion.ConversionKind;
                if (conversion.Operand.Type == targetType
                    && (conversionKind == ConversionKind.Boxing || conversionKind == ConversionKind.ImplicitReference))
                {
                    // Instead of creating an inverse conversion, we simply strip the inner conversion
                    return conversion.Operand;
                }
            }

            return Convert(targetType, sourceExpression, compilation);
        }

        private static BoundExpression Convert(TypeSymbol targetType, BoundExpression sourceExpression, CSharpCompilation compilation)
        {
            HashSet<DiagnosticInfo> useSiteDiagnostics = null;
            Conversion c = compilation.Conversions.ClassifyConversionFromExpression(sourceExpression, targetType, ref useSiteDiagnostics);
            Debug.Assert(useSiteDiagnostics.IsNullOrEmpty());

            // If this happens, we should probably check if the method has ObsoleteAttribute.
            Debug.Assert((object)c.Method == null, "Why are we synthesizing a user-defined conversion after initial binding?");

            return Convert(targetType, sourceExpression, c, compilation);
        }

        private static BoundExpression Convert(TypeSymbol targetType, BoundExpression sourceExpression, Conversion conversion, CSharpCompilation compilation, bool isChecked = false)
        {
            // NOTE: We can see user-defined conversions at this point because there are places in the bound tree where
            // the binder stashes Conversion objects for later consumption (e.g. foreach, nullable, increment).
            if ((object)conversion.Method != null && conversion.Method.Parameters[0].Type != sourceExpression.Type)
            {
                sourceExpression = Convert(conversion.Method.Parameters[0].Type, sourceExpression, compilation);
            }

            return new BoundConversion(sourceExpression.Syntax, sourceExpression, conversion, isChecked, true, null, targetType) { WasCompilerGenerated = true };
        }
    }
}
