using Microsoft.CodeAnalysis.CSharp.Symbols;
using Roslyn.Utilities;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    partial class MetaclassApplier
    {
        public override CompileTimeValue VisitPropertyAccess(BoundPropertyAccess node, object arg)
        {
            BoundExpression receiverOpt = node.ReceiverOpt;
            PropertySymbol property = node.PropertySymbol;

            CompileTimeValue receiverValue = Visit(receiverOpt, arg);

            Debug.Assert(receiverOpt != null && receiverValue != null && receiverValue.Kind != CompileTimeValueKind.Dynamic);
            if (receiverOpt.Type.IsArray())
            {
                if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Array__Length))
                {
                    Debug.Assert(receiverValue is ArrayValue);
                    return new ConstantStaticValue(ConstantValue.Create(((ArrayValue)receiverValue).Array.Length));
                }
            }
            if (receiverOpt.Type == _compilation.GetWellKnownType(WellKnownType.System_Type))
            {
                return VisitStaticTypePropertyAccess(node, property, receiverValue);
            }
            else if (receiverOpt.Type == _compilation.GetWellKnownType(WellKnownType.System_Reflection_MethodInfo))
            {
                return VisitStaticMethodInfoPropertyAccess(node, property, receiverValue);
            }
            else
            {
                Debug.Assert(receiverOpt.Type == _compilation.GetWellKnownType(WellKnownType.System_Reflection_ParameterInfo));
                return VisitStaticParameterInfoPropertyAccess(node, property, receiverValue);
            }
        }

        private CompileTimeValue VisitStaticTypePropertyAccess(BoundPropertyAccess node, PropertySymbol property, CompileTimeValue receiverValue)
        {
            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__FullName)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsAbstract)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsArray)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsByRef)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsClass)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsConstructedGenericType)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsEnum)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsGenericParameter)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsGenericType)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsGenericTypeDefinition)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsInterface)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNested)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNestedAssembly)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNestedPrivate)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNestedPublic)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNotPublic)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsPublic)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsSealed)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsValueType)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsVisible)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__Name)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__Namespace))
                {
                    _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.Syntax.Location);
                    throw new ExecutionInterruptionException(InterruptionKind.Throw);
                }
                else
                {
                    throw ExceptionUtilities.Unreachable;
                }
            }
            else
            {
                Debug.Assert(receiverValue is TypeValue);
                TypeSymbol type = ((TypeValue)receiverValue).Type;
                if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__FullName))
                {
                    return new ConstantStaticValue(ConstantValue.Create(MetaUtils.GetTypeFullName(type)));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsAbstract))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.IsAbstract));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsArray))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.IsArray()));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsByRef))
                {
                    return new ConstantStaticValue(ConstantValue.Create(((TypeValue)receiverValue).IsByRef));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsClass))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.IsClassType()));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsConstructedGenericType))
                {
                    return new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsGeneric(type) && !type.IsUnboundGenericType()));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsEnum))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.IsEnumType()));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsGenericParameter))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.IsTypeParameter()));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsGenericType))
                {
                    return new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsGeneric(type)));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsGenericTypeDefinition))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.IsUnboundGenericType()));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsInterface))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.IsInterfaceType()));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNested))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.ContainingType != null));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNestedAssembly))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.ContainingType != null && type.DeclaredAccessibility == Accessibility.Internal));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNestedPrivate))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.ContainingType != null && type.DeclaredAccessibility == Accessibility.Private));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNestedPublic))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.ContainingType != null && type.DeclaredAccessibility == Accessibility.Public));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNotPublic))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.ContainingType == null && type.DeclaredAccessibility != Accessibility.Public));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsPublic))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.ContainingType == null && type.DeclaredAccessibility == Accessibility.Public));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsSealed))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.IsSealed));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsValueType))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.IsValueType));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsVisible))
                {
                    return new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsVisible(type)));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__Name))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.MetadataName));
                }
                else
                {
                    Debug.Assert(property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__Namespace));
                    NamespaceSymbol @namespace = type.ContainingNamespace;
                    if (@namespace == null)
                    {
                        return new ConstantStaticValue(ConstantValue.Null);
                    }
                    else
                    {
                        return new ConstantStaticValue(ConstantValue.Create(MetaUtils.GetNamespaceFullName(@namespace)));
                    }
                }
            }
        }

        private CompileTimeValue VisitStaticMethodInfoPropertyAccess(BoundPropertyAccess node, PropertySymbol property, CompileTimeValue receiverValue)
        {
            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MemberInfo__DeclaringType)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MemberInfo__Name)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodInfo__ReturnType))
                {
                    _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.Syntax.Location);
                    throw new ExecutionInterruptionException(InterruptionKind.Throw);
                }
                else
                {
                    throw ExceptionUtilities.Unreachable;
                }
            }
            else
            {
                Debug.Assert(receiverValue is MethodInfoValue);
                MethodSymbol method = ((MethodInfoValue)receiverValue).Method;
                if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MemberInfo__DeclaringType))
                {
                    TypeSymbol containingType = method.ContainingType;
                    if (containingType == null)
                    {
                        return new ConstantStaticValue(ConstantValue.Null);
                    }
                    else
                    {
                        return new TypeValue(containingType);
                    }
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MemberInfo__Name))
                {
                    return new ConstantStaticValue(ConstantValue.Create(method.Name));
                }
                else
                {
                    Debug.Assert(property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodInfo__ReturnType));
                    return new TypeValue(method.ReturnType);
                }
            }
        }

        private CompileTimeValue VisitStaticParameterInfoPropertyAccess(BoundPropertyAccess node, PropertySymbol property, CompileTimeValue receiverValue)
        {
            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__IsOut)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__Member)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__Name)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__ParameterType)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__Position))
                {
                    _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.Syntax.Location);
                    throw new ExecutionInterruptionException(InterruptionKind.Throw);
                }
                else
                {
                    throw ExceptionUtilities.Unreachable;
                }
            }
            else
            {
                Debug.Assert(receiverValue is ParameterInfoValue);
                ParameterSymbol parameter = ((ParameterInfoValue)receiverValue).Parameter;
                if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__IsOut))
                {
                    return new ConstantStaticValue(ConstantValue.Create(parameter.RefKind == RefKind.Out));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__Member))
                {
                    // For now, we only deal at compile time with parameters of methods
                    Debug.Assert(parameter.ContainingSymbol is MethodSymbol);
                    return new MethodInfoValue((MethodSymbol)parameter.ContainingSymbol);
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__Name))
                {
                    return new ConstantStaticValue(ConstantValue.Create(parameter.Name));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__ParameterType))
                {
                    return new TypeValue(parameter.Type, parameter.RefKind != RefKind.None);
                }
                else
                {
                    Debug.Assert(property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__Position));
                    return new ConstantStaticValue(ConstantValue.Create(parameter.Ordinal));
                }
            }
        }
    }
}
