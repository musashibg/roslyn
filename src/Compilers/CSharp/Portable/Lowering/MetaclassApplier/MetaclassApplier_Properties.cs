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
            else if (property.ContainingType == _compilation.GetWellKnownType(WellKnownType.System_Reflection_MemberInfo))
            {
                return VisitStaticMemberInfoPropertyAccess(node, property, receiverValue);
            }
            else if (receiverOpt.Type == _compilation.GetWellKnownType(WellKnownType.System_Reflection_MethodInfo))
            {
                return VisitStaticMethodInfoPropertyAccess(node, property, receiverValue);
            }
            else if (property.ContainingType == _compilation.GetWellKnownType(WellKnownType.System_Reflection_PropertyInfo))
            {
                return VisitStaticPropertyInfoPropertyAccess(node, property, receiverValue);
            }
            else
            {
                Debug.Assert(receiverOpt.Type == _compilation.GetWellKnownType(WellKnownType.System_Reflection_ParameterInfo));
                return VisitStaticParameterInfoPropertyAccess(node, property, receiverValue);
            }
        }

        private CompileTimeValue VisitStaticTypePropertyAccess(BoundPropertyAccess node, PropertySymbol accessedProperty, CompileTimeValue receiverValue)
        {
            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__FullName)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsAbstract)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsArray)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsByRef)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsClass)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsConstructedGenericType)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsEnum)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsGenericParameter)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsGenericType)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsGenericTypeDefinition)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsInterface)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNested)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNestedAssembly)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNestedPrivate)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNestedPublic)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNotPublic)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsPublic)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsSealed)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsValueType)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsVisible)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__Namespace))
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
                if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__FullName))
                {
                    return new ConstantStaticValue(ConstantValue.Create(MetaUtils.GetTypeFullName(type)));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsAbstract))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.IsAbstract));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsArray))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.IsArray()));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsByRef))
                {
                    return new ConstantStaticValue(ConstantValue.Create(((TypeValue)receiverValue).IsByRef));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsClass))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.IsClassType()));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsConstructedGenericType))
                {
                    return new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsGeneric(type) && !type.IsUnboundGenericType()));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsEnum))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.IsEnumType()));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsGenericParameter))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.IsTypeParameter()));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsGenericType))
                {
                    return new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsGeneric(type)));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsGenericTypeDefinition))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.IsUnboundGenericType()));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsInterface))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.IsInterfaceType()));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNested))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.ContainingType != null));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNestedAssembly))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.ContainingType != null && type.DeclaredAccessibility == Accessibility.Internal));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNestedPrivate))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.ContainingType != null && type.DeclaredAccessibility == Accessibility.Private));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNestedPublic))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.ContainingType != null && type.DeclaredAccessibility == Accessibility.Public));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNotPublic))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.ContainingType == null && type.DeclaredAccessibility != Accessibility.Public));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsPublic))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.ContainingType == null && type.DeclaredAccessibility == Accessibility.Public));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsSealed))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.IsSealed));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsValueType))
                {
                    return new ConstantStaticValue(ConstantValue.Create(type.IsValueType));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsVisible))
                {
                    return new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsVisible(type)));
                }
                else
                {
                    Debug.Assert(accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__Namespace));
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

        private CompileTimeValue VisitStaticMemberInfoPropertyAccess(BoundPropertyAccess node, PropertySymbol accessedProperty, CompileTimeValue receiverValue)
        {
            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MemberInfo__DeclaringType)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MemberInfo__Name))
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
                Symbol member;
                if (receiverValue is TypeValue)
                {
                    member = ((TypeValue)receiverValue).Type;
                }
                if (receiverValue is MethodInfoValue)
                {
                    member = ((MethodInfoValue)receiverValue).Method;
                }
                else if (receiverValue is ConstructorInfoValue)
                {
                    member = ((ConstructorInfoValue)receiverValue).Constructor;
                }
                else
                {
                    Debug.Assert(receiverValue is PropertyInfoValue);
                    member = ((PropertyInfoValue)receiverValue).Property;
                }

                if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MemberInfo__DeclaringType))
                {
                    TypeSymbol containingType = member.ContainingType;
                    if (containingType == null)
                    {
                        return new ConstantStaticValue(ConstantValue.Null);
                    }
                    else
                    {
                        return new TypeValue(containingType);
                    }
                }
                else
                {
                    Debug.Assert(accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MemberInfo__Name));
                    return new ConstantStaticValue(ConstantValue.Create(member.MetadataName));
                }
            }
        }

        private CompileTimeValue VisitStaticMethodInfoPropertyAccess(BoundPropertyAccess node, PropertySymbol accessedProperty, CompileTimeValue receiverValue)
        {
            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodInfo__ReturnType))
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

                Debug.Assert(accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodInfo__ReturnType));
                return new TypeValue(method.ReturnType);
            }
        }

        private CompileTimeValue VisitStaticPropertyInfoPropertyAccess(BoundPropertyAccess node, PropertySymbol accessedProperty, CompileTimeValue receiverValue)
        {
            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodInfo__ReturnType))
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
                Debug.Assert(receiverValue is PropertyInfoValue);
                PropertySymbol property = ((PropertyInfoValue)receiverValue).Property;

                Debug.Assert(accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_PropertyInfo__PropertyType));
                return new TypeValue(property.Type);
            }
        }

        private CompileTimeValue VisitStaticParameterInfoPropertyAccess(BoundPropertyAccess node, PropertySymbol accessedProperty, CompileTimeValue receiverValue)
        {
            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__IsOut)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__Member)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__Name)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__ParameterType)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__Position))
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
                if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__IsOut))
                {
                    return new ConstantStaticValue(ConstantValue.Create(parameter.RefKind == RefKind.Out));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__Member))
                {
                    // For now, we only deal at compile time with parameters of methods
                    Debug.Assert(parameter.ContainingSymbol is MethodSymbol);
                    return new MethodInfoValue((MethodSymbol)parameter.ContainingSymbol);
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__Name))
                {
                    return new ConstantStaticValue(ConstantValue.Create(parameter.Name));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__ParameterType))
                {
                    return new TypeValue(parameter.Type, parameter.RefKind != RefKind.None);
                }
                else
                {
                    Debug.Assert(accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__Position));
                    return new ConstantStaticValue(ConstantValue.Create(parameter.Ordinal));
                }
            }
        }
    }
}
