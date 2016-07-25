// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.Symbols;
using Roslyn.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal static class MetaUtils
    {
        internal delegate void AddDiagnosticCallback(ErrorCode code, Location location, params object[] args);

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

        public static bool CheckIsSpliceLocation(
            BoundCall call,
            CSharpCompilation compilation,
            DecoratedMemberKind targetMemberKind,
            SourceMemberMethodSymbol decoratorMethod,
            AddDiagnosticCallback addDiagnosticCallback)
        {
            MethodSymbol method = call.Method;
            switch (targetMemberKind)
            {
                case DecoratedMemberKind.Constructor:
                case DecoratedMemberKind.Method:
                    if (call.Method == compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodBase__Invoke))
                    {
                        // This is a call to MethodBase.Invoke(object obj, object[] parameters)
                        if (call.ReceiverOpt != null
                            && CheckIsSpecificParameter(call.ReceiverOpt, decoratorMethod.Parameters[0])
                            && CheckIsSpecificParameter(call.Arguments[0], decoratorMethod.Parameters[1])
                            && (call.Arguments[1].Kind == BoundKind.Parameter || call.Arguments[1].Kind == BoundKind.Local))
                        {
                            return true;
                        }
                        else
                        {
                            // Disallow calls to MethodBase.Invoke(object obj, object[] parameters) which are not obvious splices
                            // (as they might use a different thisObject, or they might refer to this method through a different local variable, leading to infinite recursion)
                            addDiagnosticCallback(ErrorCode.ERR_InvalidSpecialMethodCallInDecorator, call.Syntax.Location, method);
                        }
                    }
                    break;

                case DecoratedMemberKind.Destructor:
                    if (call.Method == compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodBase__Invoke))
                    {
                        // This is a call to MethodBase.Invoke(object obj, object[] parameters) with null as a second argument (destructors never have arguments)
                        if (call.ReceiverOpt != null
                            && CheckIsSpecificParameter(call.ReceiverOpt, decoratorMethod.Parameters[0])
                            && CheckIsSpecificParameter(call.Arguments[0], decoratorMethod.Parameters[1])
                            && call.Arguments[1].Kind == BoundKind.Literal
                            && ((BoundLiteral)call.Arguments[1]).ConstantValue.IsNull)
                        {
                            return true;
                        }
                        else
                        {
                            // Disallow calls to MethodBase.Invoke(object obj, object[] parameters) which are not obvious splices
                            // (as they might use a different thisObject, or they might refer to this method through a different local variable, leading to infinite recursion)
                            addDiagnosticCallback(ErrorCode.ERR_InvalidSpecialMethodCallInDecorator, call.Syntax.Location, method);
                        }
                    }
                    break;

                case DecoratedMemberKind.IndexerGet:
                    if (call.Method == compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_PropertyInfo__GetValue2))
                    {
                        // This is a call to PropertyInfo.GetValue(object obj, object[] index)
                        if (call.ReceiverOpt != null
                            && CheckIsSpecificParameter(call.ReceiverOpt, decoratorMethod.Parameters[0])
                            && CheckIsSpecificParameter(call.Arguments[0], decoratorMethod.Parameters[1])
                            && (call.Arguments[1].Kind == BoundKind.Parameter || call.Arguments[1].Kind == BoundKind.Local))
                        {
                            return true;
                        }
                        else
                        {
                            // Disallow calls to PropertyInfo.GetValue(object obj, object[] parameters) which are not obvious splices
                            // (as they might use a different thisObject, or they might refer to this indexer through a different local variable, leading to infinite recursion)
                            addDiagnosticCallback(ErrorCode.ERR_InvalidSpecialMethodCallInDecorator, call.Syntax.Location, method);
                        }
                    }
                    break;

                case DecoratedMemberKind.IndexerSet:
                    if (call.Method == compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_PropertyInfo__SetValue2))
                    {
                        // This is a call to PropertyInfo.SetValue(object obj, object value, object[] index)
                        if (call.ReceiverOpt != null
                            && CheckIsSpecificParameter(call.ReceiverOpt, decoratorMethod.Parameters[0])
                            && CheckIsSpecificParameter(call.Arguments[0], decoratorMethod.Parameters[1])
                            && (call.Arguments[2].Kind == BoundKind.Parameter || call.Arguments[2].Kind == BoundKind.Local))
                        {
                            return true;
                        }
                        else
                        {
                            // Disallow calls to PropertyInfo.SetValue(object obj, object value, object[] index) which are not obvious splices
                            // (as they might use a different thisObject, or they might refer to this indexer through a different local variable, leading to infinite recursion)
                            addDiagnosticCallback(ErrorCode.ERR_InvalidSpecialMethodCallInDecorator, call.Syntax.Location, method);
                        }
                    }
                    break;

                case DecoratedMemberKind.PropertyGet:
                    if (call.Method == compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_PropertyInfo__GetValue))
                    {
                        // This is a call to PropertyInfo.GetValue(object obj)
                        if (call.ReceiverOpt != null
                            && CheckIsSpecificParameter(call.ReceiverOpt, decoratorMethod.Parameters[0])
                            && CheckIsSpecificParameter(call.Arguments[0], decoratorMethod.Parameters[1]))
                        {
                            return true;
                        }
                        else
                        {
                            // Disallow calls to PropertyInfo.GetValue(object obj) which are not obvious splices
                            // (as they might use a different thisObject, or they might refer to this property through a different local variable, leading to infinite recursion)
                            addDiagnosticCallback(ErrorCode.ERR_InvalidSpecialMethodCallInDecorator, call.Syntax.Location, method);
                        }
                    }
                    else if (call.Method == compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_PropertyInfo__GetValue2))
                    {
                        // This is a call to PropertyInfo.GetValue(object obj, object[] index)
                        if (call.ReceiverOpt != null
                            && CheckIsSpecificParameter(call.ReceiverOpt, decoratorMethod.Parameters[0])
                            && CheckIsSpecificParameter(call.Arguments[0], decoratorMethod.Parameters[1])
                            && CheckIsNullLiteral(call.Arguments[1]))
                        {
                            return true;
                        }
                        else
                        {
                            // Disallow calls to PropertyInfo.GetValue(object obj, object[] parameters) which are not obvious splices
                            // (as they might use a different thisObject, or they might refer to this indexer through a different local variable, leading to infinite recursion)
                            addDiagnosticCallback(ErrorCode.ERR_InvalidSpecialMethodCallInDecorator, call.Syntax.Location, method);
                        }
                    }
                    break;

                case DecoratedMemberKind.PropertySet:
                    if (call.Method == compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_PropertyInfo__SetValue))
                    {
                        // This is a call to PropertyInfo.SetValue(object obj, object value)
                        if (call.ReceiverOpt != null
                            && CheckIsSpecificParameter(call.ReceiverOpt, decoratorMethod.Parameters[0])
                            && CheckIsSpecificParameter(call.Arguments[0], decoratorMethod.Parameters[1]))
                        {
                            return true;
                        }
                        else
                        {
                            // Disallow calls to PropertyInfo.SetValue(object obj, object value) which are not obvious splices
                            // (as they might use a different thisObject, or they might refer to this property through a different local variable, leading to infinite recursion)
                            addDiagnosticCallback(ErrorCode.ERR_InvalidSpecialMethodCallInDecorator, call.Syntax.Location, method);
                        }
                    }
                    else if (call.Method == compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_PropertyInfo__SetValue2))
                    {
                        // This is a call to PropertyInfo.SetValue(object obj, object value, object[] index)
                        if (call.ReceiverOpt != null
                            && CheckIsSpecificParameter(call.ReceiverOpt, decoratorMethod.Parameters[0])
                            && CheckIsSpecificParameter(call.Arguments[0], decoratorMethod.Parameters[1])
                            && CheckIsNullLiteral(call.Arguments[2]))
                        {
                            return true;
                        }
                        else
                        {
                            // Disallow calls to PropertyInfo.SetValue(object obj, object value, object[] index) which are not obvious splices
                            // (as they might use a different thisObject, or they might refer to this indexer through a different local variable, leading to infinite recursion)
                            addDiagnosticCallback(ErrorCode.ERR_InvalidSpecialMethodCallInDecorator, call.Syntax.Location, method);
                        }
                    }
                    break;

                default:
                    throw ExceptionUtilities.Unreachable;
            }
            return false;
        }

        public static bool CheckIsBaseMethodCall(BoundCall call, SourceMemberMethodSymbol currentMethod)
        {
            MethodSymbol method = call.Method;
            if (method.Name != currentMethod.Name
                || method.ParameterCount != currentMethod.ParameterCount)
            {
                return false;
            }

            for (int i = 0; i < method.ParameterCount; i++)
            {
                ParameterSymbol methodParameter = method.Parameters[i];
                ParameterSymbol decoratorMethodParameter = currentMethod.Parameters[i];
                if (methodParameter.Type != decoratorMethodParameter.Type
                    || methodParameter.RefKind != decoratorMethodParameter.RefKind)
                {
                    return false;
                }
            }

            BoundExpression receiverOpt = call.ReceiverOpt;
            return receiverOpt != null && receiverOpt.Kind == BoundKind.BaseReference;
        }

        public static bool CheckIsSpecificParameter(BoundExpression node, ParameterSymbol parameter)
        {
            while (node.Kind == BoundKind.Conversion)
            {
                node = ((BoundConversion)node).Operand;
            }
            return node.Kind == BoundKind.Parameter && ((BoundParameter)node).ParameterSymbol == parameter;
        }

        public static bool CheckIsNullLiteral(BoundExpression node)
        {
            while (node.Kind == BoundKind.Conversion)
            {
                node = ((BoundConversion)node).Operand;
            }
            return node.Kind == BoundKind.Literal && ((BoundLiteral)node).ConstantValue.IsNull;
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

        public static BoundExpression StripConversions(BoundExpression expression, bool implicitOnly = false)
        {
            while (expression != null)
            {
                switch (expression.Kind)
                {
                    case BoundKind.Conversion:
                        var boundConversion = (BoundConversion)expression;
                        if (boundConversion.Conversion.IsImplicit)
                        {
                            expression = ((BoundConversion)expression).Operand;
                        }
                        else
                        {
                            return boundConversion;
                        }
                        break;

                    case BoundKind.AsOperator:
                        var boundAsOperator = (BoundAsOperator)expression;
                        if (boundAsOperator.Conversion.IsImplicit)
                        {
                            expression = ((BoundAsOperator)expression).Operand;
                        }
                        else
                        {
                            return boundAsOperator;
                        }
                        break;

                    default:
                        return expression;
                }
            }
            return null;
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
