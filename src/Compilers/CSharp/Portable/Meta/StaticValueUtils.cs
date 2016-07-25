// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.Symbols;
using Roslyn.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal static class StaticValueUtils
    {
        public static CompileTimeValue FoldConversion(
            CSharpSyntaxNode syntax,
            CompileTimeValue sourceValue,
            ConversionKind conversionKind,
            TypeSymbol destination,
            DiagnosticBag diagnostics,
            ImmutableArray<Location> additionalDiagnosticLocations)
        {
            Debug.Assert(sourceValue.Kind == CompileTimeValueKind.Simple);

            if (conversionKind == ConversionKind.NoConversion || conversionKind == ConversionKind.Boxing || conversionKind == ConversionKind.Unboxing
                || conversionKind == ConversionKind.ImplicitReference)
            {
                return sourceValue;
            }

            ConstantValue innerValue;
            if (sourceValue is ConstantStaticValue)
            {
                innerValue = ((ConstantStaticValue)sourceValue).Value;
            }
            else if (sourceValue is EnumValue)
            {
                innerValue = ((EnumValue)sourceValue).UnderlyingValue;
            }
            else
            {
                Debug.Assert(sourceValue is TypeValue);
                return sourceValue;
            }

            TypeSymbol innerDestination;
            if (destination.IsEnumType())
            {
                innerDestination = destination.GetEnumUnderlyingType();
            }
            else
            {
                innerDestination = destination;
            }

            ConstantValue resultValue = FoldConstantConversion(
                syntax,
                innerValue,
                conversionKind,
                innerDestination,
                diagnostics,
                additionalDiagnosticLocations);

            if (innerDestination != destination)
            {
                return new EnumValue(destination, resultValue);
            }
            else
            {
                return new ConstantStaticValue(resultValue);
            }
        }

        public static CompileTimeValue FoldUnaryOperator(
            CSharpSyntaxNode syntax,
            UnaryOperatorKind kind,
            CompileTimeValue staticValue,
            SpecialType resultType,
            CSharpCompilation compilation,
            DiagnosticBag diagnostics,
            ImmutableArray<Location> additionalDiagnosticLocations)
        {
            Debug.Assert(staticValue != null && staticValue.Kind == CompileTimeValueKind.Simple);

            if (kind.IsEnum())
            {
                Debug.Assert(staticValue is EnumValue);
                return FoldEnumUnaryOperator(syntax, kind, (EnumValue)staticValue, compilation, diagnostics, additionalDiagnosticLocations);
            }
            else
            {
                Debug.Assert(staticValue is ConstantStaticValue);
                ConstantValue resultValue = FoldConstantUnaryOperator(
                    syntax,
                    kind,
                    ((ConstantStaticValue)staticValue).Value,
                    resultType,
                    diagnostics,
                    additionalDiagnosticLocations);
                return new ConstantStaticValue(resultValue);
            }
        }

        public static CompileTimeValue FoldBinaryOperator(
            CSharpSyntaxNode syntax,
            BinaryOperatorKind kind,
            CompileTimeValue leftStaticValue,
            CompileTimeValue rightStaticValue,
            SpecialType resultType,
            CSharpCompilation compilation,
            DiagnosticBag diagnostics,
            ImmutableArray<Location> additionalDiagnosticLocations)
        {
            Debug.Assert(leftStaticValue != null && rightStaticValue != null);

            if (kind.IsEnum())
            {
                Debug.Assert(leftStaticValue is EnumValue && rightStaticValue is EnumValue);
                return FoldEnumBinaryOperator(syntax, kind, (EnumValue)leftStaticValue, (EnumValue)rightStaticValue, compilation, diagnostics, additionalDiagnosticLocations);
            }
            else
            {
                ConstantValue resultValue;
                if (!(leftStaticValue is ConstantStaticValue) || !(rightStaticValue is ConstantStaticValue))
                {
                    BinaryOperatorKind @operator = kind.Operator();
                    if (@operator == BinaryOperatorKind.Equal)
                    {
                        resultValue = ConstantValue.Create(Equals(leftStaticValue, rightStaticValue));
                    }
                    else
                    {
                        Debug.Assert(@operator == BinaryOperatorKind.NotEqual);
                        resultValue = ConstantValue.Create(!Equals(leftStaticValue, rightStaticValue));
                    }
                }
                else
                {
                    Debug.Assert(leftStaticValue is ConstantStaticValue && rightStaticValue is ConstantStaticValue);
                    resultValue = FoldConstantBinaryOperator(
                        syntax,
                        kind,
                        ((ConstantStaticValue)leftStaticValue).Value,
                        ((ConstantStaticValue)rightStaticValue).Value,
                        resultType,
                        diagnostics,
                        additionalDiagnosticLocations);
                }
                return new ConstantStaticValue(resultValue);
            }
        }

        public static CompileTimeValue FoldIncrementOperator(
            CSharpSyntaxNode syntax,
            UnaryOperatorKind kind,
            CompileTimeValue staticValue,
            SpecialType resultType,
            DiagnosticBag diagnostics,
            ImmutableArray<Location> additionalDiagnosticLocations)
        {
            Debug.Assert(staticValue != null && staticValue.Kind == CompileTimeValueKind.Simple);

            if (kind.IsEnum())
            {
                Debug.Assert(staticValue is EnumValue);
                return FoldEnumIncrementOperator(syntax, kind, (EnumValue)staticValue, diagnostics, additionalDiagnosticLocations);
            }
            else
            {
                Debug.Assert(staticValue is ConstantStaticValue);
                ConstantValue resultValue = FoldConstantIncrementOperator(
                    syntax,
                    kind,
                    ((ConstantStaticValue)staticValue).Value,
                    resultType,
                    diagnostics,
                    additionalDiagnosticLocations);
                return new ConstantStaticValue(resultValue);
            }
        }

        public static CompileTimeValue LookupCustomAttributeValue(
            CSharpSyntaxNode syntax,
            TypeSymbol attributeType,
            ImmutableArray<CSharpAttributeData> attributes,
            DiagnosticBag diagnostics,
            ImmutableArray<Location> additionalDiagnosticLocations,
            out CSharpAttributeData candidateAttribute)
        {
            if (attributes.IsDefaultOrEmpty)
            {
                candidateAttribute = null;
                return new ConstantStaticValue(ConstantValue.Null);
            }

            var candidateAttributes = new List<CSharpAttributeData>();
            HashSet<DiagnosticInfo> useSiteDiagnostics = null;
            foreach (CSharpAttributeData attribute in attributes)
            {
                if (attribute.AttributeClass.IsEqualToOrDerivedFrom(attributeType, false, ref useSiteDiagnostics))
                {
                    candidateAttributes.Add(attribute);
                }
            }

            if (useSiteDiagnostics != null)
            {
                foreach (DiagnosticInfo diagnosticInfo in useSiteDiagnostics)
                {
                    diagnostics.Add(diagnosticInfo, syntax.Location);
                }
            }

            switch (candidateAttributes.Count)
            {
                case 0:
                    candidateAttribute = null;
                    return new ConstantStaticValue(ConstantValue.Null);

                case 1:
                    candidateAttribute = candidateAttributes[0];
                    return new AttributeValue(candidateAttribute);

                default:
                    diagnostics.Add(ErrorCode.ERR_StaticAmbiguousMatch, syntax.Location, additionalDiagnosticLocations);
                    throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }
        }

        public static CompileTimeValue LookupConstructors(TypeSymbol type, BindingFlags bindingFlags, ArrayTypeSymbol resultType)
        {
            ImmutableArray<CompileTimeValue>.Builder constructorsBuilder = ImmutableArray.CreateBuilder<CompileTimeValue>();
            foreach (Symbol member in type.GetMembers())
            {
                if (member.Kind != SymbolKind.Method)
                {
                    continue;
                }

                var method = (MethodSymbol)member;
                if (method.MethodKind != MethodKind.Constructor && method.MethodKind != MethodKind.StaticConstructor)
                {
                    continue;
                }

                if (method.ContainingType != type)
                {
                    continue;
                }

                if (method.DeclaredAccessibility == Accessibility.Public)
                {
                    if (!bindingFlags.HasFlag(BindingFlags.Public))
                    {
                        continue;
                    }
                }
                else
                {
                    if (!bindingFlags.HasFlag(BindingFlags.NonPublic))
                    {
                        continue;
                    }
                }

                if (method.IsStatic)
                {
                    if (!bindingFlags.HasFlag(BindingFlags.Static))
                    {
                        continue;
                    }
                }
                else
                {
                    if (!bindingFlags.HasFlag(BindingFlags.Instance))
                    {
                        continue;
                    }
                }

                constructorsBuilder.Add(new ConstructorInfoValue(method));
            }
            return new ArrayValue(resultType, constructorsBuilder.ToImmutable());
        }

        public static CompileTimeValue LookupMethods(TypeSymbol type, BindingFlags bindingFlags, ArrayTypeSymbol resultType)
        {
            ImmutableArray<CompileTimeValue>.Builder methodsBuilder = ImmutableArray.CreateBuilder<CompileTimeValue>();
            foreach (Symbol member in type.GetMembers())
            {
                if (member.Kind != SymbolKind.Method)
                {
                    continue;
                }

                var method = (MethodSymbol)member;
                if (method.MethodKind == MethodKind.Constructor || method.MethodKind == MethodKind.StaticConstructor)
                {
                    continue;
                }

                if (method.ContainingType != type && bindingFlags.HasFlag(BindingFlags.DeclaredOnly))
                {
                    continue;
                }

                if (method.DeclaredAccessibility == Accessibility.Public)
                {
                    if (!bindingFlags.HasFlag(BindingFlags.Public))
                    {
                        continue;
                    }
                }
                else
                {
                    if (!bindingFlags.HasFlag(BindingFlags.NonPublic))
                    {
                        continue;
                    }
                }

                if (method.IsStatic)
                {
                    if (!bindingFlags.HasFlag(BindingFlags.Static))
                    {
                        continue;
                    }
                    if (method.ContainingType != type && !bindingFlags.HasFlag(BindingFlags.FlattenHierarchy))
                    {
                        continue;
                    }
                }
                else
                {
                    if (!bindingFlags.HasFlag(BindingFlags.Instance))
                    {
                        continue;
                    }
                }

                methodsBuilder.Add(new MethodInfoValue(method));
            }
            return new ArrayValue(resultType, methodsBuilder.ToImmutable());
        }

        public static CompileTimeValue LookupProperties(TypeSymbol type, BindingFlags bindingFlags, ArrayTypeSymbol resultType)
        {
            ImmutableArray<CompileTimeValue>.Builder propertiesBuilder = ImmutableArray.CreateBuilder<CompileTimeValue>();
            foreach (Symbol member in type.GetMembers())
            {
                if (member.Kind != SymbolKind.Property)
                {
                    continue;
                }

                var property = (PropertySymbol)member;
                if (property.ContainingType != type && bindingFlags.HasFlag(BindingFlags.DeclaredOnly))
                {
                    continue;
                }

                if (property.DeclaredAccessibility == Accessibility.Public)
                {
                    if (!bindingFlags.HasFlag(BindingFlags.Public))
                    {
                        continue;
                    }
                }
                else
                {
                    if (!bindingFlags.HasFlag(BindingFlags.NonPublic))
                    {
                        continue;
                    }
                }

                if (property.IsStatic)
                {
                    if (!bindingFlags.HasFlag(BindingFlags.Static))
                    {
                        continue;
                    }
                    if (property.ContainingType != type && !bindingFlags.HasFlag(BindingFlags.FlattenHierarchy))
                    {
                        continue;
                    }
                }
                else
                {
                    if (!bindingFlags.HasFlag(BindingFlags.Instance))
                    {
                        continue;
                    }
                }

                propertiesBuilder.Add(new PropertyInfoValue(property));
            }
            return new ArrayValue(resultType, propertiesBuilder.ToImmutable());
        }

        private static void Error(DiagnosticBag diagnostics, ErrorCode errorCode, CSharpSyntaxNode syntax, ImmutableArray<Location> additionalLocations, params object[] args)
        {
            diagnostics.Add(errorCode, syntax.Location, additionalLocations, args);
        }

        private static bool ShouldCheckOverflow(CSharpSyntaxNode syntax)
        {
            while (syntax != null)
            {
                switch (syntax.Kind())
                {
                    case SyntaxKind.UncheckedExpression:
                    case SyntaxKind.UncheckedStatement:
                        return false;

                    case SyntaxKind.CheckedExpression:
                    case SyntaxKind.CheckedStatement:
                        return true;

                    default:
                        syntax = syntax.Parent;
                        break;
                }
            }
            return true;
        }

        private static SpecialType GetEnumPromotedType(SpecialType underlyingType)
        {
            switch (underlyingType)
            {
                case SpecialType.System_Byte:
                case SpecialType.System_SByte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                    return SpecialType.System_Int32;

                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                    return underlyingType;

                default:
                    throw ExceptionUtilities.UnexpectedValue(underlyingType);
            }
        }

        private static ConstantValue Convert(
            CSharpSyntaxNode syntax,
            TypeSymbol source,
            ConstantValue sourceConstantValue,
            TypeSymbol destination,
            CSharpCompilation compilation,
            DiagnosticBag diagnostics,
            ImmutableArray<Location> additionalDiagnosticLocations)
        {
            Conversion conversion = compilation.ClassifyConversion(source, destination);
            Debug.Assert(conversion != null);
            return FoldConstantConversion(syntax, sourceConstantValue, conversion.Kind, destination, diagnostics, additionalDiagnosticLocations);
        }

        private static ConstantValue FoldConstantConversion(
            CSharpSyntaxNode syntax,
            ConstantValue sourceConstantValue,
            ConversionKind conversionKind,
            TypeSymbol destination,
            DiagnosticBag diagnostics,
            ImmutableArray<Location> additionalDiagnosticLocations)
        {
            Debug.Assert(sourceConstantValue != null && (object)destination != null);

            if (sourceConstantValue.IsBad)
            {
                return sourceConstantValue;
            }

            switch (conversionKind)
            {
                case ConversionKind.Identity:
                    // An identity conversion to a floating-point type (for example from a cast in
                    // source code) changes the internal representation of the constant value
                    // to precisely the required precision.
                    switch (destination.SpecialType)
                    {
                        case SpecialType.System_Single:
                            return ConstantValue.Create(sourceConstantValue.SingleValue);
                        case SpecialType.System_Double:
                            return ConstantValue.Create(sourceConstantValue.DoubleValue);
                        default:
                            return sourceConstantValue;
                    }

                case ConversionKind.NullLiteral:
                case ConversionKind.ExplicitReference:
                case ConversionKind.ImplicitReference:
                    return sourceConstantValue;

                case ConversionKind.ImplicitConstant:
                    return FoldConstantNumericConversion(syntax, sourceConstantValue, destination, diagnostics, additionalDiagnosticLocations);

                case ConversionKind.ExplicitNumeric:
                case ConversionKind.ImplicitNumeric:
                case ConversionKind.ExplicitEnumeration:
                case ConversionKind.ImplicitEnumeration:
                    return FoldConstantNumericConversion(syntax, sourceConstantValue, destination, diagnostics, additionalDiagnosticLocations);
            }

            return null;
        }

        private static ConstantValue FoldConstantNumericConversion(
            CSharpSyntaxNode syntax,
            ConstantValue sourceValue,
            TypeSymbol destination,
            DiagnosticBag diagnostics,
            ImmutableArray<Location> additionalDiagnosticLocations)
        {
            Debug.Assert(sourceValue != null);
            Debug.Assert(!sourceValue.IsBad);

            SpecialType destinationType;
            if ((object)destination != null && destination.IsEnumType())
            {
                var underlyingType = ((NamedTypeSymbol)destination).EnumUnderlyingType;
                Debug.Assert((object)underlyingType != null);
                Debug.Assert(underlyingType.SpecialType != SpecialType.None);
                destinationType = underlyingType.SpecialType;
            }
            else
            {
                destinationType = destination.GetSpecialTypeSafe();
            }

            // In an unchecked context we ignore overflowing conversions on conversions from any
            // integral type, float and double to any integral type. "unchecked" actually does not
            // affect conversions from decimal to any integral type; if those are out of bounds then
            // we always give an error regardless.

            if (sourceValue.IsDecimal)
            {
                if (!CheckConstantBounds(destinationType, sourceValue))
                {
                    // NOTE: Dev10 puts a suffix, "M", on the constant value.
                    Error(diagnostics, ErrorCode.ERR_ConstOutOfRange, syntax, additionalDiagnosticLocations, sourceValue.Value + "M", destination);

                    return ConstantValue.Bad;
                }
            }
            else if (destinationType == SpecialType.System_Decimal)
            {
                if (!CheckConstantBounds(destinationType, sourceValue))
                {
                    Error(diagnostics, ErrorCode.ERR_ConstOutOfRange, syntax, additionalDiagnosticLocations, sourceValue.Value, destination);

                    return ConstantValue.Bad;
                }
            }
            else if (ShouldCheckOverflow(syntax))
            {
                if (!CheckConstantBounds(destinationType, sourceValue))
                {
                    Error(diagnostics, ErrorCode.ERR_ConstOutOfRangeChecked, syntax, additionalDiagnosticLocations, sourceValue.Value, destination);

                    return ConstantValue.Bad;
                }
            }

            return ConstantValue.Create(DoUncheckedConversion(destinationType, sourceValue), destinationType);
        }

        private static CompileTimeValue FoldEnumUnaryOperator(
            CSharpSyntaxNode syntax,
            UnaryOperatorKind kind,
            EnumValue staticValue,
            CSharpCompilation compilation,
            DiagnosticBag diagnostics,
            ImmutableArray<Location> additionalDiagnosticLocations)
        {
            Debug.Assert(staticValue != null && kind.IsEnum() && !kind.IsLifted());

            TypeSymbol enumType = staticValue.EnumType;
            TypeSymbol underlyingType = enumType.GetEnumUnderlyingType();

            // We may have to upconvert the type if it is a byte, sbyte, short or ushort, because there is no ~ operator
            SpecialType upconvertSpecialType = GetEnumPromotedType(underlyingType.SpecialType);
            TypeSymbol upconvertType = upconvertSpecialType == underlyingType.SpecialType ?
                underlyingType :
                compilation.GetSpecialType(upconvertSpecialType);

            ConstantValue underlyingValue = Convert(syntax, enumType, staticValue.UnderlyingValue, upconvertType, compilation, diagnostics, additionalDiagnosticLocations);

            UnaryOperatorKind newKind = kind.Operator().WithType(upconvertSpecialType);

            ConstantValue constantValue = FoldConstantUnaryOperator(syntax, newKind, underlyingValue, upconvertType.SpecialType, diagnostics, additionalDiagnosticLocations);
            Debug.Assert(constantValue != null);

            if (!constantValue.IsBad)
            {
                // Do an unchecked conversion if bitwise complement
                constantValue = FoldConstantNumericConversion(syntax, constantValue, underlyingType, diagnostics, additionalDiagnosticLocations);
                return new EnumValue(enumType, constantValue);
            }

            return new ConstantStaticValue(constantValue);
        }

        private static ConstantValue FoldConstantUnaryOperator(
            CSharpSyntaxNode syntax,
            UnaryOperatorKind kind,
            ConstantValue value,
            SpecialType resultType,
            DiagnosticBag diagnostics,
            ImmutableArray<Location> additionalDiagnosticLocations)
        {
            Debug.Assert(value != null && !kind.IsEnum() && !kind.IsLifted());
            // UNDONE: report errors when in a checked context.

            if (value.IsBad)
            {
                return value;
            }

            object newValue = FoldNeverOverflowUnaryOperator(kind, value);
            if (newValue != null)
            {
                return ConstantValue.Create(newValue, resultType);
            }

            if (ShouldCheckOverflow(syntax))
            {
                try
                {
                    newValue = FoldCheckedIntegralUnaryOperator(kind, value);
                }
                catch (OverflowException)
                {
                    Error(diagnostics, ErrorCode.ERR_CheckedOverflow, syntax, additionalDiagnosticLocations);
                    return ConstantValue.Bad;
                }
            }
            else
            {
                newValue = FoldUncheckedIntegralUnaryOperator(kind, value);
            }

            if (newValue != null)
            {
                return ConstantValue.Create(newValue, resultType);
            }

            return null;
        }

        private static object FoldNeverOverflowUnaryOperator(UnaryOperatorKind kind, ConstantValue value)
        {
            // Note that we do operations on single-precision floats as double-precision.
            switch (kind)
            {
                case UnaryOperatorKind.DecimalUnaryMinus:
                    return -value.DecimalValue;
                case UnaryOperatorKind.DoubleUnaryMinus:
                case UnaryOperatorKind.FloatUnaryMinus:
                    return -value.DoubleValue;
                case UnaryOperatorKind.DecimalUnaryPlus:
                    return +value.DecimalValue;
                case UnaryOperatorKind.FloatUnaryPlus:
                case UnaryOperatorKind.DoubleUnaryPlus:
                    return +value.DoubleValue;
                case UnaryOperatorKind.LongUnaryPlus:
                    return +value.Int64Value;
                case UnaryOperatorKind.ULongUnaryPlus:
                    return +value.UInt64Value;
                case UnaryOperatorKind.IntUnaryPlus:
                    return +value.Int32Value;
                case UnaryOperatorKind.UIntUnaryPlus:
                    return +value.UInt32Value;
                case UnaryOperatorKind.BoolLogicalNegation:
                    return !value.BooleanValue;
                case UnaryOperatorKind.IntBitwiseComplement:
                    return ~value.Int32Value;
                case UnaryOperatorKind.LongBitwiseComplement:
                    return ~value.Int64Value;
                case UnaryOperatorKind.UIntBitwiseComplement:
                    return ~value.UInt32Value;
                case UnaryOperatorKind.ULongBitwiseComplement:
                    return ~value.UInt64Value;
            }

            return null;
        }

        private static object FoldCheckedIntegralUnaryOperator(UnaryOperatorKind kind, ConstantValue value)
        {
            checked
            {
                switch (kind)
                {
                    case UnaryOperatorKind.LongUnaryMinus:
                        return -value.Int64Value;
                    case UnaryOperatorKind.IntUnaryMinus:
                        return -value.Int32Value;
                }
            }

            return null;
        }

        private static object FoldUncheckedIntegralUnaryOperator(UnaryOperatorKind kind, ConstantValue value)
        {
            unchecked
            {
                switch (kind)
                {
                    case UnaryOperatorKind.LongUnaryMinus:
                        return -value.Int64Value;
                    case UnaryOperatorKind.IntUnaryMinus:
                        return -value.Int32Value;
                }
            }

            return null;
        }

        private static CompileTimeValue FoldEnumIncrementOperator(
            CSharpSyntaxNode syntax,
            UnaryOperatorKind kind,
            EnumValue staticValue,
            DiagnosticBag diagnostics,
            ImmutableArray<Location> additionalDiagnosticLocations)
        {
            Debug.Assert(staticValue != null && kind.IsEnum() && !kind.IsLifted());

            TypeSymbol enumType = staticValue.EnumType;
            TypeSymbol underlyingType = enumType.GetEnumUnderlyingType();

            ConstantValue underlyingValue = staticValue.UnderlyingValue;

            UnaryOperatorKind newKind = kind.Operator().WithType(underlyingType.SpecialType);

            ConstantValue constantValue = FoldConstantIncrementOperator(syntax, newKind, underlyingValue, underlyingType.SpecialType, diagnostics, additionalDiagnosticLocations);
            Debug.Assert(constantValue != null);

            if (!constantValue.IsBad)
            {
                return new EnumValue(enumType, constantValue);
            }

            return new ConstantStaticValue(constantValue);
        }

        private static ConstantValue FoldConstantIncrementOperator(
            CSharpSyntaxNode syntax,
            UnaryOperatorKind kind,
            ConstantValue value,
            SpecialType resultType,
            DiagnosticBag diagnostics,
            ImmutableArray<Location> additionalDiagnosticLocations)
        {
            Debug.Assert(value != null && !kind.IsEnum() && !kind.IsLifted());
            // UNDONE: report errors when in a checked context.

            if (value.IsBad)
            {
                return value;
            }

            object newValue;
            if (ShouldCheckOverflow(syntax))
            {
                try
                {
                    newValue = FoldCheckedIncrementOperator(kind, value);
                }
                catch (OverflowException)
                {
                    Error(diagnostics, ErrorCode.ERR_CheckedOverflow, syntax, additionalDiagnosticLocations);
                    return ConstantValue.Bad;
                }
            }
            else
            {
                newValue = FoldUncheckedIncrementOperator(kind, value);
            }

            if (newValue != null)
            {
                return ConstantValue.Create(newValue, resultType);
            }

            return null;
        }

        private static object FoldCheckedIncrementOperator(UnaryOperatorKind kind, ConstantValue value)
        {
            checked
            {
                switch (kind)
                {
                    case UnaryOperatorKind.SBytePostfixDecrement:
                    case UnaryOperatorKind.SBytePrefixDecrement:
                        return (sbyte)(value.SByteValue - 1);
                    case UnaryOperatorKind.SBytePostfixIncrement:
                    case UnaryOperatorKind.SBytePrefixIncrement:
                        return (sbyte)(value.SByteValue + 1);
                    case UnaryOperatorKind.BytePostfixDecrement:
                    case UnaryOperatorKind.BytePrefixDecrement:
                        return (byte)(value.ByteValue - 1);
                    case UnaryOperatorKind.BytePostfixIncrement:
                    case UnaryOperatorKind.BytePrefixIncrement:
                        return (byte)(value.ByteValue + 1);
                    case UnaryOperatorKind.ShortPostfixDecrement:
                    case UnaryOperatorKind.ShortPrefixDecrement:
                        return (short)(value.Int16Value - 1);
                    case UnaryOperatorKind.ShortPostfixIncrement:
                    case UnaryOperatorKind.ShortPrefixIncrement:
                        return (short)(value.Int16Value + 1);
                    case UnaryOperatorKind.UShortPostfixDecrement:
                    case UnaryOperatorKind.UShortPrefixDecrement:
                        return (ushort)(value.UInt16Value - 1);
                    case UnaryOperatorKind.UShortPostfixIncrement:
                    case UnaryOperatorKind.UShortPrefixIncrement:
                        return (ushort)(value.UInt16Value + 1);
                    case UnaryOperatorKind.IntPostfixDecrement:
                    case UnaryOperatorKind.IntPrefixDecrement:
                        return value.Int32Value - 1;
                    case UnaryOperatorKind.IntPostfixIncrement:
                    case UnaryOperatorKind.IntPrefixIncrement:
                        return value.Int32Value + 1;
                    case UnaryOperatorKind.UIntPostfixDecrement:
                    case UnaryOperatorKind.UIntPrefixDecrement:
                        return value.UInt32Value - 1U;
                    case UnaryOperatorKind.UIntPostfixIncrement:
                    case UnaryOperatorKind.UIntPrefixIncrement:
                        return value.UInt32Value + 1U;
                    case UnaryOperatorKind.LongPostfixDecrement:
                    case UnaryOperatorKind.LongPrefixDecrement:
                        return value.Int64Value - 1L;
                    case UnaryOperatorKind.LongPostfixIncrement:
                    case UnaryOperatorKind.LongPrefixIncrement:
                        return value.Int64Value + 1L;
                    case UnaryOperatorKind.ULongPostfixDecrement:
                    case UnaryOperatorKind.ULongPrefixDecrement:
                        return value.UInt64Value - 1UL;
                    case UnaryOperatorKind.ULongPostfixIncrement:
                    case UnaryOperatorKind.ULongPrefixIncrement:
                        return value.UInt64Value + 1UL;
                    case UnaryOperatorKind.CharPostfixDecrement:
                    case UnaryOperatorKind.CharPrefixDecrement:
                        return (char)(value.CharValue - 1);
                    case UnaryOperatorKind.CharPostfixIncrement:
                    case UnaryOperatorKind.CharPrefixIncrement:
                        return (char)(value.CharValue + 1);
                    case UnaryOperatorKind.FloatPostfixDecrement:
                    case UnaryOperatorKind.FloatPrefixDecrement:
                        return value.SingleValue - 1.0F;
                    case UnaryOperatorKind.FloatPostfixIncrement:
                    case UnaryOperatorKind.FloatPrefixIncrement:
                        return value.SingleValue + 1.0F;
                    case UnaryOperatorKind.DoublePostfixDecrement:
                    case UnaryOperatorKind.DoublePrefixDecrement:
                        return value.DoubleValue - 1.0;
                    case UnaryOperatorKind.DoublePostfixIncrement:
                    case UnaryOperatorKind.DoublePrefixIncrement:
                        return value.DoubleValue + 1.0;
                    case UnaryOperatorKind.DecimalPostfixDecrement:
                    case UnaryOperatorKind.DecimalPrefixDecrement:
                        return value.DecimalValue - 1.0M;
                    case UnaryOperatorKind.DecimalPostfixIncrement:
                    case UnaryOperatorKind.DecimalPrefixIncrement:
                        return value.DecimalValue + 1.0M;
                }
            }

            return null;
        }

        private static object FoldUncheckedIncrementOperator(UnaryOperatorKind kind, ConstantValue value)
        {
            unchecked
            {
                switch (kind)
                {
                    case UnaryOperatorKind.SBytePostfixDecrement:
                    case UnaryOperatorKind.SBytePrefixDecrement:
                        return (sbyte)(value.SByteValue - 1);
                    case UnaryOperatorKind.SBytePostfixIncrement:
                    case UnaryOperatorKind.SBytePrefixIncrement:
                        return (sbyte)(value.SByteValue + 1);
                    case UnaryOperatorKind.BytePostfixDecrement:
                    case UnaryOperatorKind.BytePrefixDecrement:
                        return (byte)(value.ByteValue - 1);
                    case UnaryOperatorKind.BytePostfixIncrement:
                    case UnaryOperatorKind.BytePrefixIncrement:
                        return (byte)(value.ByteValue + 1);
                    case UnaryOperatorKind.ShortPostfixDecrement:
                    case UnaryOperatorKind.ShortPrefixDecrement:
                        return (short)(value.Int16Value - 1);
                    case UnaryOperatorKind.ShortPostfixIncrement:
                    case UnaryOperatorKind.ShortPrefixIncrement:
                        return (short)(value.Int16Value + 1);
                    case UnaryOperatorKind.UShortPostfixDecrement:
                    case UnaryOperatorKind.UShortPrefixDecrement:
                        return (ushort)(value.UInt16Value - 1);
                    case UnaryOperatorKind.UShortPostfixIncrement:
                    case UnaryOperatorKind.UShortPrefixIncrement:
                        return (ushort)(value.UInt16Value + 1);
                    case UnaryOperatorKind.IntPostfixDecrement:
                    case UnaryOperatorKind.IntPrefixDecrement:
                        return value.Int32Value - 1;
                    case UnaryOperatorKind.IntPostfixIncrement:
                    case UnaryOperatorKind.IntPrefixIncrement:
                        return value.Int32Value + 1;
                    case UnaryOperatorKind.UIntPostfixDecrement:
                    case UnaryOperatorKind.UIntPrefixDecrement:
                        return value.UInt32Value - 1U;
                    case UnaryOperatorKind.UIntPostfixIncrement:
                    case UnaryOperatorKind.UIntPrefixIncrement:
                        return value.UInt32Value + 1U;
                    case UnaryOperatorKind.LongPostfixDecrement:
                    case UnaryOperatorKind.LongPrefixDecrement:
                        return value.Int64Value - 1L;
                    case UnaryOperatorKind.LongPostfixIncrement:
                    case UnaryOperatorKind.LongPrefixIncrement:
                        return value.Int64Value + 1L;
                    case UnaryOperatorKind.ULongPostfixDecrement:
                    case UnaryOperatorKind.ULongPrefixDecrement:
                        return value.UInt64Value - 1UL;
                    case UnaryOperatorKind.ULongPostfixIncrement:
                    case UnaryOperatorKind.ULongPrefixIncrement:
                        return value.UInt64Value + 1UL;
                    case UnaryOperatorKind.CharPostfixDecrement:
                    case UnaryOperatorKind.CharPrefixDecrement:
                        return (char)(value.CharValue - 1);
                    case UnaryOperatorKind.CharPostfixIncrement:
                    case UnaryOperatorKind.CharPrefixIncrement:
                        return (char)(value.CharValue + 1);
                    case UnaryOperatorKind.FloatPostfixDecrement:
                    case UnaryOperatorKind.FloatPrefixDecrement:
                        return value.SingleValue - 1.0F;
                    case UnaryOperatorKind.FloatPostfixIncrement:
                    case UnaryOperatorKind.FloatPrefixIncrement:
                        return value.SingleValue + 1.0F;
                    case UnaryOperatorKind.DoublePostfixDecrement:
                    case UnaryOperatorKind.DoublePrefixDecrement:
                        return value.DoubleValue - 1.0;
                    case UnaryOperatorKind.DoublePostfixIncrement:
                    case UnaryOperatorKind.DoublePrefixIncrement:
                        return value.DoubleValue + 1.0;
                    case UnaryOperatorKind.DecimalPostfixDecrement:
                    case UnaryOperatorKind.DecimalPrefixDecrement:
                        return value.DecimalValue - 1.0M;
                    case UnaryOperatorKind.DecimalPostfixIncrement:
                    case UnaryOperatorKind.DecimalPrefixIncrement:
                        return value.DecimalValue + 1.0M;
                }
            }

            return null;
        }

        private static CompileTimeValue FoldEnumBinaryOperator(
            CSharpSyntaxNode syntax,
            BinaryOperatorKind kind,
            EnumValue leftStaticValue,
            EnumValue rightStaticValue,
            CSharpCompilation compilation,
            DiagnosticBag diagnostics,
            ImmutableArray<Location> additionalDiagnosticLocations)
        {
            Debug.Assert(leftStaticValue != null && rightStaticValue != null && kind.IsEnum() && !kind.IsLifted());

            // A built-in binary operation on constant enum operands is evaluated into an operation on 
            // constants of the underlying type U of the enum type E. Comparison operators are lowered as
            // simply computing U<U. All other operators are computed as (E)(U op U) or in the case of 
            // E-E, (U)(U-U).  

            TypeSymbol enumType = leftStaticValue.EnumType;
            Debug.Assert(rightStaticValue.EnumType == enumType);
            TypeSymbol underlyingType = enumType.GetEnumUnderlyingType();

            // If the underlying type is byte, sbyte, short or ushort then, we'll need
            // to convert it up to int or int? because there are no + - * & | ^ < > <= >= == != operators
            // on byte, sbyte, short or ushort. They all convert to int.

            SpecialType operandSpecialType = GetEnumPromotedType(underlyingType.SpecialType);
            TypeSymbol operandType = (operandSpecialType == underlyingType.SpecialType) ?
                underlyingType :
                compilation.GetSpecialType(operandSpecialType);

            ConstantValue leftUnderlyingValue = Convert(syntax, enumType, leftStaticValue.UnderlyingValue, operandType, compilation, diagnostics, additionalDiagnosticLocations);
            ConstantValue rightUnderlyingValue = Convert(syntax, enumType, rightStaticValue.UnderlyingValue, operandType, compilation, diagnostics, additionalDiagnosticLocations);

            BinaryOperatorKind newKind = kind.Operator().WithType(leftUnderlyingValue.SpecialType);

            SpecialType operatorType = SpecialType.None;

            switch (newKind.Operator())
            {
                case BinaryOperatorKind.Addition:
                case BinaryOperatorKind.Subtraction:
                case BinaryOperatorKind.And:
                case BinaryOperatorKind.Or:
                case BinaryOperatorKind.Xor:
                    operatorType = operandType.SpecialType;
                    break;

                case BinaryOperatorKind.LessThan:
                case BinaryOperatorKind.LessThanOrEqual:
                case BinaryOperatorKind.GreaterThan:
                case BinaryOperatorKind.GreaterThanOrEqual:
                case BinaryOperatorKind.Equal:
                case BinaryOperatorKind.NotEqual:
                    operatorType = SpecialType.System_Boolean;
                    break;

                default:
                    throw ExceptionUtilities.UnexpectedValue(newKind.Operator());
            }

            ConstantValue constantValue = FoldConstantBinaryOperator(syntax, newKind, leftUnderlyingValue, rightUnderlyingValue, operatorType, diagnostics, additionalDiagnosticLocations);
            Debug.Assert(constantValue != null);

            if (operatorType != SpecialType.System_Boolean && !constantValue.IsBad)
            {
                // We might need to convert back to the underlying type.
                constantValue = FoldConstantNumericConversion(syntax, constantValue, underlyingType, diagnostics, additionalDiagnosticLocations);
                return new EnumValue(enumType, constantValue);
            }

            return new ConstantStaticValue(constantValue);
        }

        private static bool CheckConstantBounds(SpecialType destinationType, ConstantValue value)
        {
            if (value.IsBad)
            {
                //assume that the constant was intended to be in bounds
                return true;
            }

            // Compute whether the value fits into the bounds of the given destination type without
            // error. We know that the constant will fit into either a double or a decimal, so
            // convert it to one of those and then check the bounds on that.
            var canonicalValue = CanonicalizeConstant(value);
            return canonicalValue is decimal ?
                CheckConstantBounds(destinationType, (decimal)canonicalValue) :
                CheckConstantBounds(destinationType, (double)canonicalValue);
        }

        private static bool CheckConstantBounds(SpecialType destinationType, double value)
        {
            // Dev10 checks (minValue - 1) < value < (maxValue + 1).
            // See ExpressionBinder::isConstantInRange.
            switch (destinationType)
            {
                case SpecialType.System_Byte: return (byte.MinValue - 1D) < value && value < (byte.MaxValue + 1D);
                case SpecialType.System_Char: return (char.MinValue - 1D) < value && value < (char.MaxValue + 1D);
                case SpecialType.System_UInt16: return (ushort.MinValue - 1D) < value && value < (ushort.MaxValue + 1D);
                case SpecialType.System_UInt32: return (uint.MinValue - 1D) < value && value < (uint.MaxValue + 1D);
                case SpecialType.System_UInt64: return (ulong.MinValue - 1D) < value && value < (ulong.MaxValue + 1D);
                case SpecialType.System_SByte: return (sbyte.MinValue - 1D) < value && value < (sbyte.MaxValue + 1D);
                case SpecialType.System_Int16: return (short.MinValue - 1D) < value && value < (short.MaxValue + 1D);
                case SpecialType.System_Int32: return (int.MinValue - 1D) < value && value < (int.MaxValue + 1D);
                // Note: Using <= to compare the min value matches the native compiler.
                case SpecialType.System_Int64: return (long.MinValue - 1D) <= value && value < (long.MaxValue + 1D);
                case SpecialType.System_Decimal: return ((double)decimal.MinValue - 1D) < value && value < ((double)decimal.MaxValue + 1D);
            }

            return true;
        }

        private static bool CheckConstantBounds(SpecialType destinationType, decimal value)
        {
            // Dev10 checks (minValue - 1) < value < (MaxValue + 1) + 1).
            // See ExpressionBinder::isConstantInRange.
            switch (destinationType)
            {
                case SpecialType.System_Byte: return (byte.MinValue - 1M) < value && value < (byte.MaxValue + 1M);
                case SpecialType.System_Char: return (char.MinValue - 1M) < value && value < (char.MaxValue + 1M);
                case SpecialType.System_UInt16: return (ushort.MinValue - 1M) < value && value < (ushort.MaxValue + 1M);
                case SpecialType.System_UInt32: return (uint.MinValue - 1M) < value && value < (uint.MaxValue + 1M);
                case SpecialType.System_UInt64: return (ulong.MinValue - 1M) < value && value < (ulong.MaxValue + 1M);
                case SpecialType.System_SByte: return (sbyte.MinValue - 1M) < value && value < (sbyte.MaxValue + 1M);
                case SpecialType.System_Int16: return (short.MinValue - 1M) < value && value < (short.MaxValue + 1M);
                case SpecialType.System_Int32: return (int.MinValue - 1M) < value && value < (int.MaxValue + 1M);
                case SpecialType.System_Int64: return (long.MinValue - 1M) < value && value < (long.MaxValue + 1M);
            }

            return true;
        }

        // Takes in a constant of any kind and returns the constant as either a double or decimal
        private static object CanonicalizeConstant(ConstantValue value)
        {
            switch (value.Discriminator)
            {
                case ConstantValueTypeDiscriminator.SByte: return (decimal)value.SByteValue;
                case ConstantValueTypeDiscriminator.Int16: return (decimal)value.Int16Value;
                case ConstantValueTypeDiscriminator.Int32: return (decimal)value.Int32Value;
                case ConstantValueTypeDiscriminator.Int64: return (decimal)value.Int64Value;
                case ConstantValueTypeDiscriminator.Byte: return (decimal)value.ByteValue;
                case ConstantValueTypeDiscriminator.Char: return (decimal)value.CharValue;
                case ConstantValueTypeDiscriminator.UInt16: return (decimal)value.UInt16Value;
                case ConstantValueTypeDiscriminator.UInt32: return (decimal)value.UInt32Value;
                case ConstantValueTypeDiscriminator.UInt64: return (decimal)value.UInt64Value;
                case ConstantValueTypeDiscriminator.Single:
                case ConstantValueTypeDiscriminator.Double: return value.DoubleValue;
                case ConstantValueTypeDiscriminator.Decimal: return value.DecimalValue;
                default: throw ExceptionUtilities.UnexpectedValue(value.Discriminator);
            }

            // all cases handled in the switch, above.
        }

        private static object DoUncheckedConversion(SpecialType destinationType, ConstantValue value)
        {
            // Note that we keep "single" floats as doubles internally to maintain higher precision. However,
            // we do not do so in an entirely "lossless" manner. When *converting* to a float, we do lose 
            // the precision lost due to the conversion. But when doing arithmetic, we do the arithmetic on
            // the double values.
            //
            // An example will help. Suppose we have:
            //
            // const float cf1 = 1.0f;
            // const float cf2 = 1.0e-15f;
            // const double cd3 = cf1 - cf2;
            //
            // We first take the double-precision values for 1.0 and 1.0e-15 and round them to floats,
            // and then turn them back into doubles. Then when we do the subtraction, we do the subtraction
            // in doubles, not in floats. Had we done the subtraction in floats, we'd get 1.0; but instead we
            // do it in doubles and get 0.99999999999999.
            //
            // Similarly, if we have
            //
            // const int i4 = int.MaxValue; // 2147483647
            // const float cf5 = int.MaxValue; //  2147483648.0
            // const double cd6 = cf5; // 2147483648.0
            //
            // The int is converted to float and stored internally as the double 214783648, even though the
            // fully precise int would fit into a double.

            unchecked
            {
                switch (value.Discriminator)
                {
                    case ConstantValueTypeDiscriminator.Byte:
                        byte byteValue = value.ByteValue;
                        switch (destinationType)
                        {
                            case SpecialType.System_Byte: return (byte)byteValue;
                            case SpecialType.System_Char: return (char)byteValue;
                            case SpecialType.System_UInt16: return (ushort)byteValue;
                            case SpecialType.System_UInt32: return (uint)byteValue;
                            case SpecialType.System_UInt64: return (ulong)byteValue;
                            case SpecialType.System_SByte: return (sbyte)byteValue;
                            case SpecialType.System_Int16: return (short)byteValue;
                            case SpecialType.System_Int32: return (int)byteValue;
                            case SpecialType.System_Int64: return (long)byteValue;
                            case SpecialType.System_Single:
                            case SpecialType.System_Double: return (double)byteValue;
                            case SpecialType.System_Decimal: return (decimal)byteValue;
                            default: throw ExceptionUtilities.UnexpectedValue(destinationType);
                        }
                    case ConstantValueTypeDiscriminator.Char:
                        char charValue = value.CharValue;
                        switch (destinationType)
                        {
                            case SpecialType.System_Byte: return (byte)charValue;
                            case SpecialType.System_Char: return (char)charValue;
                            case SpecialType.System_UInt16: return (ushort)charValue;
                            case SpecialType.System_UInt32: return (uint)charValue;
                            case SpecialType.System_UInt64: return (ulong)charValue;
                            case SpecialType.System_SByte: return (sbyte)charValue;
                            case SpecialType.System_Int16: return (short)charValue;
                            case SpecialType.System_Int32: return (int)charValue;
                            case SpecialType.System_Int64: return (long)charValue;
                            case SpecialType.System_Single:
                            case SpecialType.System_Double: return (double)charValue;
                            case SpecialType.System_Decimal: return (decimal)charValue;
                            default: throw ExceptionUtilities.UnexpectedValue(destinationType);
                        }
                    case ConstantValueTypeDiscriminator.UInt16:
                        ushort uint16Value = value.UInt16Value;
                        switch (destinationType)
                        {
                            case SpecialType.System_Byte: return (byte)uint16Value;
                            case SpecialType.System_Char: return (char)uint16Value;
                            case SpecialType.System_UInt16: return (ushort)uint16Value;
                            case SpecialType.System_UInt32: return (uint)uint16Value;
                            case SpecialType.System_UInt64: return (ulong)uint16Value;
                            case SpecialType.System_SByte: return (sbyte)uint16Value;
                            case SpecialType.System_Int16: return (short)uint16Value;
                            case SpecialType.System_Int32: return (int)uint16Value;
                            case SpecialType.System_Int64: return (long)uint16Value;
                            case SpecialType.System_Single:
                            case SpecialType.System_Double: return (double)uint16Value;
                            case SpecialType.System_Decimal: return (decimal)uint16Value;
                            default: throw ExceptionUtilities.UnexpectedValue(destinationType);
                        }
                    case ConstantValueTypeDiscriminator.UInt32:
                        uint uint32Value = value.UInt32Value;
                        switch (destinationType)
                        {
                            case SpecialType.System_Byte: return (byte)uint32Value;
                            case SpecialType.System_Char: return (char)uint32Value;
                            case SpecialType.System_UInt16: return (ushort)uint32Value;
                            case SpecialType.System_UInt32: return (uint)uint32Value;
                            case SpecialType.System_UInt64: return (ulong)uint32Value;
                            case SpecialType.System_SByte: return (sbyte)uint32Value;
                            case SpecialType.System_Int16: return (short)uint32Value;
                            case SpecialType.System_Int32: return (int)uint32Value;
                            case SpecialType.System_Int64: return (long)uint32Value;
                            case SpecialType.System_Single: return (double)(float)uint32Value;
                            case SpecialType.System_Double: return (double)uint32Value;
                            case SpecialType.System_Decimal: return (decimal)uint32Value;
                            default: throw ExceptionUtilities.UnexpectedValue(destinationType);
                        }
                    case ConstantValueTypeDiscriminator.UInt64:
                        ulong uint64Value = value.UInt64Value;
                        switch (destinationType)
                        {
                            case SpecialType.System_Byte: return (byte)uint64Value;
                            case SpecialType.System_Char: return (char)uint64Value;
                            case SpecialType.System_UInt16: return (ushort)uint64Value;
                            case SpecialType.System_UInt32: return (uint)uint64Value;
                            case SpecialType.System_UInt64: return (ulong)uint64Value;
                            case SpecialType.System_SByte: return (sbyte)uint64Value;
                            case SpecialType.System_Int16: return (short)uint64Value;
                            case SpecialType.System_Int32: return (int)uint64Value;
                            case SpecialType.System_Int64: return (long)uint64Value;
                            case SpecialType.System_Single: return (double)(float)uint64Value;
                            case SpecialType.System_Double: return (double)uint64Value;
                            case SpecialType.System_Decimal: return (decimal)uint64Value;
                            default: throw ExceptionUtilities.UnexpectedValue(destinationType);
                        }
                    case ConstantValueTypeDiscriminator.SByte:
                        sbyte sbyteValue = value.SByteValue;
                        switch (destinationType)
                        {
                            case SpecialType.System_Byte: return (byte)sbyteValue;
                            case SpecialType.System_Char: return (char)sbyteValue;
                            case SpecialType.System_UInt16: return (ushort)sbyteValue;
                            case SpecialType.System_UInt32: return (uint)sbyteValue;
                            case SpecialType.System_UInt64: return (ulong)sbyteValue;
                            case SpecialType.System_SByte: return (sbyte)sbyteValue;
                            case SpecialType.System_Int16: return (short)sbyteValue;
                            case SpecialType.System_Int32: return (int)sbyteValue;
                            case SpecialType.System_Int64: return (long)sbyteValue;
                            case SpecialType.System_Single:
                            case SpecialType.System_Double: return (double)sbyteValue;
                            case SpecialType.System_Decimal: return (decimal)sbyteValue;
                            default: throw ExceptionUtilities.UnexpectedValue(destinationType);
                        }
                    case ConstantValueTypeDiscriminator.Int16:
                        short int16Value = value.Int16Value;
                        switch (destinationType)
                        {
                            case SpecialType.System_Byte: return (byte)int16Value;
                            case SpecialType.System_Char: return (char)int16Value;
                            case SpecialType.System_UInt16: return (ushort)int16Value;
                            case SpecialType.System_UInt32: return (uint)int16Value;
                            case SpecialType.System_UInt64: return (ulong)int16Value;
                            case SpecialType.System_SByte: return (sbyte)int16Value;
                            case SpecialType.System_Int16: return (short)int16Value;
                            case SpecialType.System_Int32: return (int)int16Value;
                            case SpecialType.System_Int64: return (long)int16Value;
                            case SpecialType.System_Single:
                            case SpecialType.System_Double: return (double)int16Value;
                            case SpecialType.System_Decimal: return (decimal)int16Value;
                            default: throw ExceptionUtilities.UnexpectedValue(destinationType);
                        }
                    case ConstantValueTypeDiscriminator.Int32:
                        int int32Value = value.Int32Value;
                        switch (destinationType)
                        {
                            case SpecialType.System_Byte: return (byte)int32Value;
                            case SpecialType.System_Char: return (char)int32Value;
                            case SpecialType.System_UInt16: return (ushort)int32Value;
                            case SpecialType.System_UInt32: return (uint)int32Value;
                            case SpecialType.System_UInt64: return (ulong)int32Value;
                            case SpecialType.System_SByte: return (sbyte)int32Value;
                            case SpecialType.System_Int16: return (short)int32Value;
                            case SpecialType.System_Int32: return (int)int32Value;
                            case SpecialType.System_Int64: return (long)int32Value;
                            case SpecialType.System_Single: return (double)(float)int32Value;
                            case SpecialType.System_Double: return (double)int32Value;
                            case SpecialType.System_Decimal: return (decimal)int32Value;
                            default: throw ExceptionUtilities.UnexpectedValue(destinationType);
                        }
                    case ConstantValueTypeDiscriminator.Int64:
                        long int64Value = value.Int64Value;
                        switch (destinationType)
                        {
                            case SpecialType.System_Byte: return (byte)int64Value;
                            case SpecialType.System_Char: return (char)int64Value;
                            case SpecialType.System_UInt16: return (ushort)int64Value;
                            case SpecialType.System_UInt32: return (uint)int64Value;
                            case SpecialType.System_UInt64: return (ulong)int64Value;
                            case SpecialType.System_SByte: return (sbyte)int64Value;
                            case SpecialType.System_Int16: return (short)int64Value;
                            case SpecialType.System_Int32: return (int)int64Value;
                            case SpecialType.System_Int64: return (long)int64Value;
                            case SpecialType.System_Single: return (double)(float)int64Value;
                            case SpecialType.System_Double: return (double)int64Value;
                            case SpecialType.System_Decimal: return (decimal)int64Value;
                            default: throw ExceptionUtilities.UnexpectedValue(destinationType);
                        }
                    case ConstantValueTypeDiscriminator.Single:
                    case ConstantValueTypeDiscriminator.Double:
                        // This code used to invoke CheckConstantBounds and return constant zero if the value is not within the target type.
                        // The C# spec says that in this case the result of the conversion is an unspecified value of the destination type.
                        // Zero is a perfectly valid unspecified value, so that behavior was formally correct.
                        // But it did not agree with the behavior of the native C# compiler, that apparently returned a value that
                        // would resulted from a runtime conversion with normal CLR overflow behavior.
                        // To avoid breaking programs that might accidentally rely on that unspecified behavior
                        // we now removed that check and just allow conversion to overflow.
                        double doubleValue = value.DoubleValue;
                        switch (destinationType)
                        {
                            case SpecialType.System_Byte: return (byte)doubleValue;
                            case SpecialType.System_Char: return (char)doubleValue;
                            case SpecialType.System_UInt16: return (ushort)doubleValue;
                            case SpecialType.System_UInt32: return (uint)doubleValue;
                            case SpecialType.System_UInt64: return (ulong)doubleValue;
                            case SpecialType.System_SByte: return (sbyte)doubleValue;
                            case SpecialType.System_Int16: return (short)doubleValue;
                            case SpecialType.System_Int32: return (int)doubleValue;
                            case SpecialType.System_Int64: return (long)doubleValue;
                            case SpecialType.System_Single: return (double)(float)doubleValue;
                            case SpecialType.System_Double: return (double)doubleValue;
                            case SpecialType.System_Decimal: return (value.Discriminator == ConstantValueTypeDiscriminator.Single) ? (decimal)(float)doubleValue : (decimal)doubleValue;
                            default: throw ExceptionUtilities.UnexpectedValue(destinationType);
                        }
                    case ConstantValueTypeDiscriminator.Decimal:
                        decimal decimalValue = CheckConstantBounds(destinationType, value.DecimalValue) ? value.DecimalValue : 0m;
                        switch (destinationType)
                        {
                            case SpecialType.System_Byte: return (byte)decimalValue;
                            case SpecialType.System_Char: return (char)decimalValue;
                            case SpecialType.System_UInt16: return (ushort)decimalValue;
                            case SpecialType.System_UInt32: return (uint)decimalValue;
                            case SpecialType.System_UInt64: return (ulong)decimalValue;
                            case SpecialType.System_SByte: return (sbyte)decimalValue;
                            case SpecialType.System_Int16: return (short)decimalValue;
                            case SpecialType.System_Int32: return (int)decimalValue;
                            case SpecialType.System_Int64: return (long)decimalValue;
                            case SpecialType.System_Single: return (double)(float)decimalValue;
                            case SpecialType.System_Double: return (double)decimalValue;
                            case SpecialType.System_Decimal: return (decimal)decimalValue;
                            default: throw ExceptionUtilities.UnexpectedValue(destinationType);
                        }
                    default:
                        throw ExceptionUtilities.UnexpectedValue(value.Discriminator);
                }
            }

            // all cases should have been handled in the switch above.
            // return value.Value;
        }

        // Returns null if the operator can't be evaluated at compile time.
        private static ConstantValue FoldConstantBinaryOperator(
            CSharpSyntaxNode syntax,
            BinaryOperatorKind kind,
            ConstantValue leftValue,
            ConstantValue rightValue,
            SpecialType resultType,
            DiagnosticBag diagnostics,
            ImmutableArray<Location> additionalDiagnosticLocations)
        {
            int compoundStringLength = 0;
            return FoldConstantBinaryOperator(syntax, kind, leftValue, rightValue, resultType, diagnostics, additionalDiagnosticLocations, ref compoundStringLength);
        }

        private static ConstantValue FoldConstantBinaryOperator(
            CSharpSyntaxNode syntax,
            BinaryOperatorKind kind,
            ConstantValue leftValue,
            ConstantValue rightValue,
            SpecialType resultType,
            DiagnosticBag diagnostics,
            ImmutableArray<Location> additionalDiagnosticLocations,
            ref int compoundStringLength)
        {
            Debug.Assert(leftValue != null && rightValue != null && !kind.IsEnum() && !kind.IsLifted());

            if (leftValue.IsBad || rightValue.IsBad)
            {
                return ConstantValue.Bad;
            }

            // Divisions by zero on integral types and decimal always fail even in an unchecked context.
            if (IsDivisionByZero(kind, rightValue))
            {
                Error(diagnostics, ErrorCode.ERR_IntDivByZero, syntax, additionalDiagnosticLocations);
                return ConstantValue.Bad;
            }

            object newValue = null;

            // Certain binary operations never fail; bool & bool, for example. If we are in one of those
            // cases, simply fold the operation and return.
            //
            // Although remainder and division always overflow at runtime with arguments int.MinValue/long.MinValue and -1 
            // (regardless of checked context) the constant folding behavior is different. 
            // Remainder never overflows at compile time while division does.
            newValue = FoldNeverOverflowBinaryOperators(kind, leftValue, rightValue);
            if (newValue != null)
            {
                return ConstantValue.Create(newValue, resultType);
            }

            ConstantValue concatResult = FoldStringConcatenation(kind, leftValue, rightValue, ref compoundStringLength);
            if (concatResult != null)
            {
                if (concatResult.IsBad)
                {
                    Error(diagnostics, ErrorCode.ERR_ConstantStringTooLong, syntax, additionalDiagnosticLocations);
                }

                return concatResult;
            }

            // Certain binary operations always fail if they overflow even when in an unchecked context;
            // decimal + decimal, for example. If we are in one of those cases, make the attempt. If it
            // succeeds, return the result. If not, give a compile-time error regardless of context.
            try
            {
                newValue = FoldDecimalBinaryOperators(kind, leftValue, rightValue);
            }
            catch (OverflowException)
            {
                Error(diagnostics, ErrorCode.ERR_DecConstError, syntax, additionalDiagnosticLocations);
                return ConstantValue.Bad;
            }

            if (newValue != null)
            {
                return ConstantValue.Create(newValue, resultType);
            }

            if (ShouldCheckOverflow(syntax))
            {
                try
                {
                    newValue = FoldCheckedIntegralBinaryOperator(kind, leftValue, rightValue);
                }
                catch (OverflowException)
                {
                    Error(diagnostics, ErrorCode.ERR_CheckedOverflow, syntax, additionalDiagnosticLocations);
                    return ConstantValue.Bad;
                }
            }
            else
            {
                newValue = FoldUncheckedIntegralBinaryOperator(kind, leftValue, rightValue);
            }

            if (newValue != null)
            {
                return ConstantValue.Create(newValue, resultType);
            }

            return null;
        }

        private static bool IsDivisionByZero(BinaryOperatorKind kind, ConstantValue valueRight)
        {
            Debug.Assert(valueRight != null);

            switch (kind)
            {
                case BinaryOperatorKind.DecimalDivision:
                case BinaryOperatorKind.DecimalRemainder:
                    return valueRight.DecimalValue == 0.0m;
                case BinaryOperatorKind.IntDivision:
                case BinaryOperatorKind.IntRemainder:
                    return valueRight.Int32Value == 0;
                case BinaryOperatorKind.LongDivision:
                case BinaryOperatorKind.LongRemainder:
                    return valueRight.Int64Value == 0;
                case BinaryOperatorKind.UIntDivision:
                case BinaryOperatorKind.UIntRemainder:
                    return valueRight.UInt32Value == 0;
                case BinaryOperatorKind.ULongDivision:
                case BinaryOperatorKind.ULongRemainder:
                    return valueRight.UInt64Value == 0;
            }

            return false;
        }

        // Some binary operators on constants never overflow, regardless of whether the context is checked or not.
        private static object FoldNeverOverflowBinaryOperators(BinaryOperatorKind kind, ConstantValue valueLeft, ConstantValue valueRight)
        {
            Debug.Assert(valueLeft != null);
            Debug.Assert(valueRight != null);

            // Note that we *cannot* do folding on single-precision floats as doubles to preserve precision,
            // as that would cause incorrect rounding that would be impossible to correct afterwards.
            switch (kind)
            {
                case BinaryOperatorKind.ObjectEqual:
                    if (valueLeft.IsNull) return valueRight.IsNull;
                    if (valueRight.IsNull) return false;
                    break;
                case BinaryOperatorKind.ObjectNotEqual:
                    if (valueLeft.IsNull) return !valueRight.IsNull;
                    if (valueRight.IsNull) return true;
                    break;
                case BinaryOperatorKind.DoubleAddition:
                    return valueLeft.DoubleValue + valueRight.DoubleValue;
                case BinaryOperatorKind.FloatAddition:
                    return valueLeft.SingleValue + valueRight.SingleValue;
                case BinaryOperatorKind.DoubleSubtraction:
                    return valueLeft.DoubleValue - valueRight.DoubleValue;
                case BinaryOperatorKind.FloatSubtraction:
                    return valueLeft.SingleValue - valueRight.SingleValue;
                case BinaryOperatorKind.DoubleMultiplication:
                    return valueLeft.DoubleValue * valueRight.DoubleValue;
                case BinaryOperatorKind.FloatMultiplication:
                    return valueLeft.SingleValue * valueRight.SingleValue;
                case BinaryOperatorKind.DoubleDivision:
                    return valueLeft.DoubleValue / valueRight.DoubleValue;
                case BinaryOperatorKind.FloatDivision:
                    return valueLeft.SingleValue / valueRight.SingleValue;
                case BinaryOperatorKind.DoubleRemainder:
                    return valueLeft.DoubleValue % valueRight.DoubleValue;
                case BinaryOperatorKind.FloatRemainder:
                    return valueLeft.SingleValue % valueRight.SingleValue;
                case BinaryOperatorKind.IntLeftShift:
                    return valueLeft.Int32Value << valueRight.Int32Value;
                case BinaryOperatorKind.LongLeftShift:
                    return valueLeft.Int64Value << valueRight.Int32Value;
                case BinaryOperatorKind.UIntLeftShift:
                    return valueLeft.UInt32Value << valueRight.Int32Value;
                case BinaryOperatorKind.ULongLeftShift:
                    return valueLeft.UInt64Value << valueRight.Int32Value;
                case BinaryOperatorKind.IntRightShift:
                    return valueLeft.Int32Value >> valueRight.Int32Value;
                case BinaryOperatorKind.LongRightShift:
                    return valueLeft.Int64Value >> valueRight.Int32Value;
                case BinaryOperatorKind.UIntRightShift:
                    return valueLeft.UInt32Value >> valueRight.Int32Value;
                case BinaryOperatorKind.ULongRightShift:
                    return valueLeft.UInt64Value >> valueRight.Int32Value;
                case BinaryOperatorKind.BoolAnd:
                    return valueLeft.BooleanValue & valueRight.BooleanValue;
                case BinaryOperatorKind.IntAnd:
                    return valueLeft.Int32Value & valueRight.Int32Value;
                case BinaryOperatorKind.LongAnd:
                    return valueLeft.Int64Value & valueRight.Int64Value;
                case BinaryOperatorKind.UIntAnd:
                    return valueLeft.UInt32Value & valueRight.UInt32Value;
                case BinaryOperatorKind.ULongAnd:
                    return valueLeft.UInt64Value & valueRight.UInt64Value;
                case BinaryOperatorKind.BoolOr:
                    return valueLeft.BooleanValue | valueRight.BooleanValue;
                case BinaryOperatorKind.IntOr:
                    return valueLeft.Int32Value | valueRight.Int32Value;
                case BinaryOperatorKind.LongOr:
                    return valueLeft.Int64Value | valueRight.Int64Value;
                case BinaryOperatorKind.UIntOr:
                    return valueLeft.UInt32Value | valueRight.UInt32Value;
                case BinaryOperatorKind.ULongOr:
                    return valueLeft.UInt64Value | valueRight.UInt64Value;
                case BinaryOperatorKind.BoolXor:
                    return valueLeft.BooleanValue ^ valueRight.BooleanValue;
                case BinaryOperatorKind.IntXor:
                    return valueLeft.Int32Value ^ valueRight.Int32Value;
                case BinaryOperatorKind.LongXor:
                    return valueLeft.Int64Value ^ valueRight.Int64Value;
                case BinaryOperatorKind.UIntXor:
                    return valueLeft.UInt32Value ^ valueRight.UInt32Value;
                case BinaryOperatorKind.ULongXor:
                    return valueLeft.UInt64Value ^ valueRight.UInt64Value;
                case BinaryOperatorKind.LogicalBoolAnd:
                    return valueLeft.BooleanValue && valueRight.BooleanValue;
                case BinaryOperatorKind.LogicalBoolOr:
                    return valueLeft.BooleanValue || valueRight.BooleanValue;
                case BinaryOperatorKind.BoolEqual:
                    return valueLeft.BooleanValue == valueRight.BooleanValue;
                case BinaryOperatorKind.StringEqual:
                    return valueLeft.StringValue == valueRight.StringValue;
                case BinaryOperatorKind.DecimalEqual:
                    return valueLeft.DecimalValue == valueRight.DecimalValue;
                case BinaryOperatorKind.FloatEqual:
                    return valueLeft.SingleValue == valueRight.SingleValue;
                case BinaryOperatorKind.DoubleEqual:
                    return valueLeft.DoubleValue == valueRight.DoubleValue;
                case BinaryOperatorKind.IntEqual:
                    return valueLeft.Int32Value == valueRight.Int32Value;
                case BinaryOperatorKind.LongEqual:
                    return valueLeft.Int64Value == valueRight.Int64Value;
                case BinaryOperatorKind.UIntEqual:
                    return valueLeft.UInt32Value == valueRight.UInt32Value;
                case BinaryOperatorKind.ULongEqual:
                    return valueLeft.UInt64Value == valueRight.UInt64Value;
                case BinaryOperatorKind.BoolNotEqual:
                    return valueLeft.BooleanValue != valueRight.BooleanValue;
                case BinaryOperatorKind.StringNotEqual:
                    return valueLeft.StringValue != valueRight.StringValue;
                case BinaryOperatorKind.DecimalNotEqual:
                    return valueLeft.DecimalValue != valueRight.DecimalValue;
                case BinaryOperatorKind.FloatNotEqual:
                    return valueLeft.SingleValue != valueRight.SingleValue;
                case BinaryOperatorKind.DoubleNotEqual:
                    return valueLeft.DoubleValue != valueRight.DoubleValue;
                case BinaryOperatorKind.IntNotEqual:
                    return valueLeft.Int32Value != valueRight.Int32Value;
                case BinaryOperatorKind.LongNotEqual:
                    return valueLeft.Int64Value != valueRight.Int64Value;
                case BinaryOperatorKind.UIntNotEqual:
                    return valueLeft.UInt32Value != valueRight.UInt32Value;
                case BinaryOperatorKind.ULongNotEqual:
                    return valueLeft.UInt64Value != valueRight.UInt64Value;
                case BinaryOperatorKind.DecimalLessThan:
                    return valueLeft.DecimalValue < valueRight.DecimalValue;
                case BinaryOperatorKind.FloatLessThan:
                    return valueLeft.SingleValue < valueRight.SingleValue;
                case BinaryOperatorKind.DoubleLessThan:
                    return valueLeft.DoubleValue < valueRight.DoubleValue;
                case BinaryOperatorKind.IntLessThan:
                    return valueLeft.Int32Value < valueRight.Int32Value;
                case BinaryOperatorKind.LongLessThan:
                    return valueLeft.Int64Value < valueRight.Int64Value;
                case BinaryOperatorKind.UIntLessThan:
                    return valueLeft.UInt32Value < valueRight.UInt32Value;
                case BinaryOperatorKind.ULongLessThan:
                    return valueLeft.UInt64Value < valueRight.UInt64Value;
                case BinaryOperatorKind.DecimalGreaterThan:
                    return valueLeft.DecimalValue > valueRight.DecimalValue;
                case BinaryOperatorKind.FloatGreaterThan:
                    return valueLeft.SingleValue > valueRight.SingleValue;
                case BinaryOperatorKind.DoubleGreaterThan:
                    return valueLeft.DoubleValue > valueRight.DoubleValue;
                case BinaryOperatorKind.IntGreaterThan:
                    return valueLeft.Int32Value > valueRight.Int32Value;
                case BinaryOperatorKind.LongGreaterThan:
                    return valueLeft.Int64Value > valueRight.Int64Value;
                case BinaryOperatorKind.UIntGreaterThan:
                    return valueLeft.UInt32Value > valueRight.UInt32Value;
                case BinaryOperatorKind.ULongGreaterThan:
                    return valueLeft.UInt64Value > valueRight.UInt64Value;
                case BinaryOperatorKind.DecimalLessThanOrEqual:
                    return valueLeft.DecimalValue <= valueRight.DecimalValue;
                case BinaryOperatorKind.FloatLessThanOrEqual:
                    return valueLeft.SingleValue <= valueRight.SingleValue;
                case BinaryOperatorKind.DoubleLessThanOrEqual:
                    return valueLeft.DoubleValue <= valueRight.DoubleValue;
                case BinaryOperatorKind.IntLessThanOrEqual:
                    return valueLeft.Int32Value <= valueRight.Int32Value;
                case BinaryOperatorKind.LongLessThanOrEqual:
                    return valueLeft.Int64Value <= valueRight.Int64Value;
                case BinaryOperatorKind.UIntLessThanOrEqual:
                    return valueLeft.UInt32Value <= valueRight.UInt32Value;
                case BinaryOperatorKind.ULongLessThanOrEqual:
                    return valueLeft.UInt64Value <= valueRight.UInt64Value;
                case BinaryOperatorKind.DecimalGreaterThanOrEqual:
                    return valueLeft.DecimalValue >= valueRight.DecimalValue;
                case BinaryOperatorKind.FloatGreaterThanOrEqual:
                    return valueLeft.SingleValue >= valueRight.SingleValue;
                case BinaryOperatorKind.DoubleGreaterThanOrEqual:
                    return valueLeft.DoubleValue >= valueRight.DoubleValue;
                case BinaryOperatorKind.IntGreaterThanOrEqual:
                    return valueLeft.Int32Value >= valueRight.Int32Value;
                case BinaryOperatorKind.LongGreaterThanOrEqual:
                    return valueLeft.Int64Value >= valueRight.Int64Value;
                case BinaryOperatorKind.UIntGreaterThanOrEqual:
                    return valueLeft.UInt32Value >= valueRight.UInt32Value;
                case BinaryOperatorKind.ULongGreaterThanOrEqual:
                    return valueLeft.UInt64Value >= valueRight.UInt64Value;
                case BinaryOperatorKind.UIntDivision:
                    return valueLeft.UInt32Value / valueRight.UInt32Value;
                case BinaryOperatorKind.ULongDivision:
                    return valueLeft.UInt64Value / valueRight.UInt64Value;

                // MinValue % -1 always overflows at runtime but never at compile time
                case BinaryOperatorKind.IntRemainder:
                    return (valueRight.Int32Value != -1) ? valueLeft.Int32Value % valueRight.Int32Value : 0;
                case BinaryOperatorKind.LongRemainder:
                    return (valueRight.Int64Value != -1) ? valueLeft.Int64Value % valueRight.Int64Value : 0;
                case BinaryOperatorKind.UIntRemainder:
                    return valueLeft.UInt32Value % valueRight.UInt32Value;
                case BinaryOperatorKind.ULongRemainder:
                    return valueLeft.UInt64Value % valueRight.UInt64Value;
            }

            return null;
        }

        /// <summary>
        /// Returns ConstantValue.Bad if, and only if, compound string length is out of supported limit.
        /// The <paramref name="compoundStringLength"/> parameter contains value corresponding to the 
        /// left node, or zero, which will trigger inference. Upon return, it will 
        /// be adjusted to correspond future result node.
        /// </summary>
        private static ConstantValue FoldStringConcatenation(BinaryOperatorKind kind, ConstantValue valueLeft, ConstantValue valueRight, ref int compoundStringLength)
        {
            Debug.Assert(valueLeft != null);
            Debug.Assert(valueRight != null);

            if (kind == BinaryOperatorKind.StringConcatenation)
            {
                string leftValue = valueLeft.StringValue ?? string.Empty;
                string rightValue = valueRight.StringValue ?? string.Empty;

                if (compoundStringLength == 0)
                {
                    // Infer. Keep it simple for now.
                    compoundStringLength = leftValue.Length;
                }

                Debug.Assert(compoundStringLength >= leftValue.Length);

                long newCompoundLength = (long)compoundStringLength + (long)leftValue.Length + (long)rightValue.Length;

                if (newCompoundLength > int.MaxValue)
                {
                    return ConstantValue.Bad;
                }

                ConstantValue result;

                try
                {
                    result = ConstantValue.Create(String.Concat(leftValue, rightValue));
                    compoundStringLength = (int)newCompoundLength;
                }
                catch (System.OutOfMemoryException)
                {
                    return ConstantValue.Bad;
                }

                return result;
            }

            return null;
        }

        private static object FoldDecimalBinaryOperators(BinaryOperatorKind kind, ConstantValue valueLeft, ConstantValue valueRight)
        {
            Debug.Assert(valueLeft != null);
            Debug.Assert(valueRight != null);

            // Roslyn uses Decimal.operator+, operator-, etc. for both constant expressions and
            // non-constant expressions. Dev11 uses Decimal.operator+ etc. for non-constant
            // expressions only. This leads to different results between the two compilers
            // for certain constant expressions involving +/-0. (See bug #529730.) For instance,
            // +0 + -0 == +0 in Roslyn and == -0 in Dev11. Similarly, -0 - -0 == -0 in Roslyn, +0 in Dev11.
            // This is a breaking change from the native compiler but seems acceptable since
            // constant and non-constant expressions behave consistently in Roslyn.
            // (In Dev11, (+0 + -0) != (x + y) when x = +0, y = -0.)

            switch (kind)
            {
                case BinaryOperatorKind.DecimalAddition:
                    return valueLeft.DecimalValue + valueRight.DecimalValue;
                case BinaryOperatorKind.DecimalSubtraction:
                    return valueLeft.DecimalValue - valueRight.DecimalValue;
                case BinaryOperatorKind.DecimalMultiplication:
                    return valueLeft.DecimalValue * valueRight.DecimalValue;
                case BinaryOperatorKind.DecimalDivision:
                    return valueLeft.DecimalValue / valueRight.DecimalValue;
                case BinaryOperatorKind.DecimalRemainder:
                    return valueLeft.DecimalValue % valueRight.DecimalValue;
            }

            return null;
        }

        private static object FoldCheckedIntegralBinaryOperator(BinaryOperatorKind kind, ConstantValue valueLeft, ConstantValue valueRight)
        {
            checked
            {
                Debug.Assert(valueLeft != null);
                Debug.Assert(valueRight != null);

                switch (kind)
                {
                    case BinaryOperatorKind.IntAddition:
                        return valueLeft.Int32Value + valueRight.Int32Value;
                    case BinaryOperatorKind.LongAddition:
                        return valueLeft.Int64Value + valueRight.Int64Value;
                    case BinaryOperatorKind.UIntAddition:
                        return valueLeft.UInt32Value + valueRight.UInt32Value;
                    case BinaryOperatorKind.ULongAddition:
                        return valueLeft.UInt64Value + valueRight.UInt64Value;
                    case BinaryOperatorKind.IntSubtraction:
                        return valueLeft.Int32Value - valueRight.Int32Value;
                    case BinaryOperatorKind.LongSubtraction:
                        return valueLeft.Int64Value - valueRight.Int64Value;
                    case BinaryOperatorKind.UIntSubtraction:
                        return valueLeft.UInt32Value - valueRight.UInt32Value;
                    case BinaryOperatorKind.ULongSubtraction:
                        return valueLeft.UInt64Value - valueRight.UInt64Value;
                    case BinaryOperatorKind.IntMultiplication:
                        return valueLeft.Int32Value * valueRight.Int32Value;
                    case BinaryOperatorKind.LongMultiplication:
                        return valueLeft.Int64Value * valueRight.Int64Value;
                    case BinaryOperatorKind.UIntMultiplication:
                        return valueLeft.UInt32Value * valueRight.UInt32Value;
                    case BinaryOperatorKind.ULongMultiplication:
                        return valueLeft.UInt64Value * valueRight.UInt64Value;
                    case BinaryOperatorKind.IntDivision:
                        return valueLeft.Int32Value / valueRight.Int32Value;
                    case BinaryOperatorKind.LongDivision:
                        return valueLeft.Int64Value / valueRight.Int64Value;
                }

                return null;
            }
        }

        private static object FoldUncheckedIntegralBinaryOperator(BinaryOperatorKind kind, ConstantValue valueLeft, ConstantValue valueRight)
        {
            unchecked
            {
                Debug.Assert(valueLeft != null);
                Debug.Assert(valueRight != null);

                switch (kind)
                {
                    case BinaryOperatorKind.IntAddition:
                        return valueLeft.Int32Value + valueRight.Int32Value;
                    case BinaryOperatorKind.LongAddition:
                        return valueLeft.Int64Value + valueRight.Int64Value;
                    case BinaryOperatorKind.UIntAddition:
                        return valueLeft.UInt32Value + valueRight.UInt32Value;
                    case BinaryOperatorKind.ULongAddition:
                        return valueLeft.UInt64Value + valueRight.UInt64Value;
                    case BinaryOperatorKind.IntSubtraction:
                        return valueLeft.Int32Value - valueRight.Int32Value;
                    case BinaryOperatorKind.LongSubtraction:
                        return valueLeft.Int64Value - valueRight.Int64Value;
                    case BinaryOperatorKind.UIntSubtraction:
                        return valueLeft.UInt32Value - valueRight.UInt32Value;
                    case BinaryOperatorKind.ULongSubtraction:
                        return valueLeft.UInt64Value - valueRight.UInt64Value;
                    case BinaryOperatorKind.IntMultiplication:
                        return valueLeft.Int32Value * valueRight.Int32Value;
                    case BinaryOperatorKind.LongMultiplication:
                        return valueLeft.Int64Value * valueRight.Int64Value;
                    case BinaryOperatorKind.UIntMultiplication:
                        return valueLeft.UInt32Value * valueRight.UInt32Value;
                    case BinaryOperatorKind.ULongMultiplication:
                        return valueLeft.UInt64Value * valueRight.UInt64Value;

                    // even in unchecked context division may overflow:
                    case BinaryOperatorKind.IntDivision:
                        if (valueLeft.Int32Value == int.MinValue && valueRight.Int32Value == -1)
                        {
                            return int.MinValue;
                        }

                        return valueLeft.Int32Value / valueRight.Int32Value;

                    case BinaryOperatorKind.LongDivision:
                        if (valueLeft.Int64Value == long.MinValue && valueRight.Int64Value == -1)
                        {
                            return long.MinValue;
                        }

                        return valueLeft.Int64Value / valueRight.Int64Value;
                }

                return null;
            }
        }
    }
}
