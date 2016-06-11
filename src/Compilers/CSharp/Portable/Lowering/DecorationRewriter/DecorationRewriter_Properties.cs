using Microsoft.CodeAnalysis.CSharp.Symbols;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    partial class DecorationRewriter
    {
        public override DecorationRewriteResult VisitPropertyAccess(BoundPropertyAccess node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            BoundExpression receiverOpt = node.ReceiverOpt;
            PropertySymbol property = node.PropertySymbol;
            if (!_flags.HasFlag(DecorationRewriterFlags.InDecoratorArgument))
            {
                BoundExpression strippedReceiver = MetaUtils.StripConversions(receiverOpt);
                if (strippedReceiver != null && (strippedReceiver.Kind == BoundKind.BaseReference || strippedReceiver.Kind == BoundKind.ThisReference))
                {
                    return VisitDecoratorArgument(node, property, variableValues);
                }
            }

            DecorationRewriteResult receiverResult = Visit(receiverOpt, variableValues);
            if (receiverResult != null)
            {
                variableValues = receiverResult.UpdatedVariableValues;
            }

            BoundExpression rewrittenNode = null;
            CompileTimeValue value = null;

            // Handle well-known property accesses with static binding time
            if (receiverOpt != null)
            {
                Debug.Assert(receiverResult != null);
                CompileTimeValue receiverValue = receiverResult.Value;
                if (receiverValue.Kind != CompileTimeValueKind.Dynamic)
                {
                    if (receiverOpt.Type.IsArray())
                    {
                        if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Array__Length))
                        {
                            Debug.Assert(receiverValue.Kind != CompileTimeValueKind.Simple);
                            switch (receiverValue.Kind)
                            {
                                case CompileTimeValueKind.Complex:
                                    Debug.Assert(receiverValue is ArrayValue);
                                    value = new ConstantStaticValue(ConstantValue.Create(((ArrayValue)receiverValue).Array.Length));
                                    rewrittenNode = MakeSimpleStaticValueExpression(value, node.Type, node.Syntax);
                                    break;

                                case CompileTimeValueKind.ArgumentArray:
                                    value = new ConstantStaticValue(ConstantValue.Create(((ArgumentArrayValue)receiverValue).ArgumentSymbols.Length));
                                    rewrittenNode = MakeSimpleStaticValueExpression(value, node.Type, node.Syntax);
                                    break;
                            }
                        }
                    }
                    if (property.ContainingType == _compilation.GetWellKnownType(WellKnownType.System_Type))
                    {
                        VisitStaticTypePropertyAccess(node, property, receiverValue, out rewrittenNode, out value);
                    }
                    else if (property.ContainingType == _compilation.GetWellKnownType(WellKnownType.System_Reflection_MemberInfo))
                    {
                        VisitStaticMemberInfoPropertyAccess(node, property, receiverValue, out rewrittenNode, out value);
                    }
                    else if (property.ContainingType == _compilation.GetWellKnownType(WellKnownType.System_Reflection_MethodInfo))
                    {
                        VisitStaticMethodInfoPropertyAccess(node, property, receiverValue, out rewrittenNode, out value);
                    }
                    else if (property.ContainingType == _compilation.GetWellKnownType(WellKnownType.System_Reflection_PropertyInfo))
                    {
                        VisitStaticPropertyInfoPropertyAccess(node, property, receiverValue, out rewrittenNode, out value);
                    }
                    else if (property.ContainingType == _compilation.GetWellKnownType(WellKnownType.System_Reflection_ParameterInfo))
                    {
                        VisitStaticParameterInfoPropertyAccess(node, property, receiverValue, out rewrittenNode, out value);
                    }
                }
            }

            if (rewrittenNode == null)
            {
                rewrittenNode = node.Update((BoundExpression)receiverResult?.Node, node.PropertySymbol, node.ResultKind, node.Type);
                if (value == null)
                {
                    value = CompileTimeValue.Dynamic;
                }
            }

            // A lone property access should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(rewrittenNode, variableValues, false, value);
        }

        public override DecorationRewriteResult VisitPropertyGroup(BoundPropertyGroup node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult receiverResult = Visit(node.ReceiverOpt, variableValues);
            if (receiverResult != null)
            {
                variableValues = receiverResult.UpdatedVariableValues;
            }

            // A lone property group expression should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(
                node.Update(node.Properties, (BoundExpression)receiverResult?.Node, node.ResultKind),
                variableValues,
                false,
                CompileTimeValue.Dynamic);
        }

        private void VisitStaticTypePropertyAccess(
            BoundPropertyAccess node,
            PropertySymbol accessedProperty,
            CompileTimeValue receiverValue,
            out BoundExpression rewrittenNode,
            out CompileTimeValue value)
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
                    rewrittenNode = MakeBadExpression(node.Syntax, node.Type);
                    value = CompileTimeValue.Dynamic;
                }
                else
                {
                    rewrittenNode = null;
                    value = null;
                }
            }
            else
            {
                Debug.Assert(receiverValue is TypeValue);
                TypeSymbol type = ((TypeValue)receiverValue).Type;
                if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__FullName))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(MetaUtils.GetTypeFullName(type)));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsAbstract))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.IsAbstract));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsArray))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.IsArray()));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsByRef))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(((TypeValue)receiverValue).IsByRef));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsClass))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.IsClassType()));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsConstructedGenericType))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsGeneric(type) && !type.IsUnboundGenericType()));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsEnum))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.IsEnumType()));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsGenericParameter))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.IsTypeParameter()));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsGenericType))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsGeneric(type)));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsGenericTypeDefinition))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.IsUnboundGenericType()));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsInterface))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.IsInterfaceType()));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNested))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.ContainingType != null));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNestedAssembly))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.ContainingType != null && type.DeclaredAccessibility == Accessibility.Internal));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNestedPrivate))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.ContainingType != null && type.DeclaredAccessibility == Accessibility.Private));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNestedPublic))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.ContainingType != null && type.DeclaredAccessibility == Accessibility.Public));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNotPublic))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.ContainingType == null && type.DeclaredAccessibility != Accessibility.Public));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsPublic))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.ContainingType == null && type.DeclaredAccessibility == Accessibility.Public));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsSealed))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.IsSealed));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsValueType))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.IsValueType));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsVisible))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsVisible(type)));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__Namespace))
                {
                    NamespaceSymbol @namespace = type.ContainingNamespace;
                    if (@namespace == null)
                    {
                        value = new ConstantStaticValue(ConstantValue.Null);
                    }
                    else
                    {
                        value = new ConstantStaticValue(ConstantValue.Create(MetaUtils.GetNamespaceFullName(@namespace)));
                    }
                }
                else
                {
                    value = null;
                }

                if (value == null)
                {
                    rewrittenNode = null;
                }
                else
                {
                    rewrittenNode = MakeSimpleStaticValueExpression(value, node.Type, node.Syntax);
                }
            }
        }

        private void VisitStaticMemberInfoPropertyAccess(
            BoundPropertyAccess node,
            PropertySymbol accessedProperty,
            CompileTimeValue receiverValue,
            out BoundExpression rewrittenNode,
            out CompileTimeValue value)
        {
            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MemberInfo__DeclaringType)
                    || accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MemberInfo__Name))
                {
                    _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.Syntax.Location);
                    rewrittenNode = MakeBadExpression(node.Syntax, node.Type);
                    value = CompileTimeValue.Dynamic;
                }
                else
                {
                    rewrittenNode = null;
                    value = null;
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
                        value = new ConstantStaticValue(ConstantValue.Null);
                    }
                    else
                    {
                        value = new TypeValue(containingType);
                    }
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MemberInfo__Name))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(member.MetadataName));
                }
                else
                {
                    value = null;
                }

                if (value == null)
                {
                    rewrittenNode = null;
                }
                else
                {
                    rewrittenNode = MakeSimpleStaticValueExpression(value, node.Type, node.Syntax);
                }
            }
        }

        private void VisitStaticMethodInfoPropertyAccess(
            BoundPropertyAccess node,
            PropertySymbol accessedProperty,
            CompileTimeValue receiverValue,
            out BoundExpression rewrittenNode,
            out CompileTimeValue value)
        {
            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodInfo__ReturnType))
                {
                    _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.Syntax.Location);
                    rewrittenNode = MakeBadExpression(node.Syntax, node.Type);
                    value = CompileTimeValue.Dynamic;
                }
                else
                {
                    rewrittenNode = null;
                    value = null;
                }
            }
            else
            {
                Debug.Assert(receiverValue is MethodInfoValue);
                MethodSymbol method = ((MethodInfoValue)receiverValue).Method;
                if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodInfo__ReturnType))
                {
                    value = new TypeValue(method.ReturnType);
                }
                else
                {
                    value = null;
                }

                if (value == null)
                {
                    rewrittenNode = null;
                }
                else
                {
                    rewrittenNode = MakeSimpleStaticValueExpression(value, node.Type, node.Syntax);
                }
            }
        }

        private void VisitStaticPropertyInfoPropertyAccess(
            BoundPropertyAccess node,
            PropertySymbol accessedProperty,
            CompileTimeValue receiverValue,
            out BoundExpression rewrittenNode,
            out CompileTimeValue value)
        {
            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodInfo__ReturnType))
                {
                    _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.Syntax.Location);
                    rewrittenNode = MakeBadExpression(node.Syntax, node.Type);
                    value = CompileTimeValue.Dynamic;
                }
                else
                {
                    rewrittenNode = null;
                    value = null;
                }
            }
            else
            {
                Debug.Assert(receiverValue is PropertyInfoValue);
                PropertySymbol property = ((PropertyInfoValue)receiverValue).Property;
                if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodInfo__ReturnType))
                {
                    value = new TypeValue(property.Type);
                }
                else
                {
                    value = null;
                }

                if (value == null)
                {
                    rewrittenNode = null;
                }
                else
                {
                    rewrittenNode = MakeSimpleStaticValueExpression(value, node.Type, node.Syntax);
                }
            }
        }

        private void VisitStaticParameterInfoPropertyAccess(
            BoundPropertyAccess node,
            PropertySymbol accessedProperty,
            CompileTimeValue receiverValue,
            out BoundExpression rewrittenNode,
            out CompileTimeValue value)
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
                    rewrittenNode = MakeBadExpression(node.Syntax, node.Type);
                    value = CompileTimeValue.Dynamic;
                }
                else
                {
                    rewrittenNode = null;
                    value = null;
                }
            }
            else
            {
                Debug.Assert(receiverValue is ParameterInfoValue);
                ParameterSymbol parameter = ((ParameterInfoValue)receiverValue).Parameter;
                if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__IsOut))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(parameter.RefKind == RefKind.Out));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__Member))
                {
                    // For now, we only deal at compile time with parameters of methods
                    Debug.Assert(parameter.ContainingSymbol is MethodSymbol);
                    value = new MethodInfoValue((MethodSymbol)parameter.ContainingSymbol);
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__Name))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(parameter.Name));
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__ParameterType))
                {
                    value = new TypeValue(parameter.Type, parameter.RefKind != RefKind.None);
                }
                else if (accessedProperty == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__Position))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(parameter.Ordinal));
                }
                else
                {
                    value = null;
                }

                if (value == null || value.Kind != CompileTimeValueKind.Simple)
                {
                    rewrittenNode = null;
                }
                else
                {
                    rewrittenNode = MakeSimpleStaticValueExpression(value, node.Type, node.Syntax);
                }
            }
        }
    }
}
