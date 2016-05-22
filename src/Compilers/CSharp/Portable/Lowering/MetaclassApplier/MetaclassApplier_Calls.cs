using Microsoft.CodeAnalysis.CSharp.Symbols;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    partial class MetaclassApplier
    {
        public override CompileTimeValue VisitCall(BoundCall node, object arg)
        {
            MethodSymbol method = node.Method;

            CompileTimeValue receiverValue = Visit(node.ReceiverOpt, arg);
            ImmutableArray<CompileTimeValue> argumentValues = VisitList(node.Arguments, arg);

            // Handle well-known method invocations with static binding time
            if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__GetMethods)
                || method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__GetMethods2))
            {
                return VisitGetMethods(node, receiverValue, argumentValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsAssignableFrom))
            {
                return VisitIsAssignableFromCall(node, receiverValue, argumentValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodBase__GetParameters))
            {
                return VisitGetParametersCall(node, receiverValue, argumentValues);
            }
            else if (method.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T)
                     || method.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T2))
            {
                return VisitGetCustomAttributesCall(node, argumentValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__AddTrait)
                     || method.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__AddTrait_T))
            {
                return VisitAddTraitCall(node, argumentValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__ApplyDecorator))
            {
                return VisitApplyDecoratorCall(node, argumentValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__ParameterType))
            {
                return VisitParameterTypeCall(node, argumentValues);
            }
            else
            {
                Debug.Assert(method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__ThisObjectType));
                return VisitThisObjectTypeCall(node, argumentValues);
            }
        }

        private CompileTimeValue VisitGetMethods(BoundCall node, CompileTimeValue receiverValue, ImmutableArray<CompileTimeValue> argumentValues)
        {
            Debug.Assert(receiverValue != null && argumentValues.Length <= 1);

            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.ReceiverOpt.Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            Debug.Assert(receiverValue is TypeValue && node.Type.IsArray());

            BindingFlags bindingFlags;
            if (argumentValues.IsEmpty)
            {
                bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public;
            }
            else
            {
                var bindingFlagsValue = argumentValues[0] as EnumValue;
                Debug.Assert(bindingFlagsValue != null && bindingFlagsValue.EnumType == _compilation.GetWellKnownType(WellKnownType.System_Reflection_BindingFlags));
                bindingFlags = (BindingFlags)bindingFlagsValue.UnderlyingValue.Int32Value;
            }

            return StaticValueUtils.LookupMethods(((TypeValue)receiverValue).Type, bindingFlags, (ArrayTypeSymbol)node.Type);
        }

        private CompileTimeValue VisitIsAssignableFromCall(BoundCall node, CompileTimeValue receiverValue, ImmutableArray<CompileTimeValue> argumentValues)
        {
            Debug.Assert(receiverValue != null && argumentValues.Length == 1);
            CompileTimeValue otherTypeValue = argumentValues[0];

            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.ReceiverOpt.Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            if (otherTypeValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)otherTypeValue).Value.IsNull);
                _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.Arguments[0].Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            Debug.Assert(receiverValue is TypeValue && otherTypeValue is TypeValue);
            return new ConstantStaticValue(
                ConstantValue.Create(
                    MetaUtils.CheckTypeIsAssignableFrom(((TypeValue)receiverValue).Type, ((TypeValue)otherTypeValue).Type)));
        }

        private CompileTimeValue VisitGetParametersCall(BoundCall node, CompileTimeValue receiverValue, ImmutableArray<CompileTimeValue> argumentValues)
        {
            Debug.Assert(receiverValue != null && argumentValues.IsEmpty);

            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.ReceiverOpt.Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            Debug.Assert(receiverValue is MethodInfoValue);
            MethodSymbol method = ((MethodInfoValue)receiverValue).Method;
            return new ArrayValue(
                _compilation.CreateArrayTypeSymbol(_compilation.GetWellKnownType(WellKnownType.System_Reflection_ParameterInfo)),
                method.Parameters.SelectAsArray(p => (CompileTimeValue)(new ParameterInfoValue(p))));
        }

        private CompileTimeValue VisitGetCustomAttributesCall(BoundCall node, ImmutableArray<CompileTimeValue> argumentValues)
        {
            Debug.Assert(argumentValues.Length == 1 && node.Method.TypeArguments.Length == 1);
            CompileTimeValue argumentValue = argumentValues[0];

            if (argumentValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)argumentValue).Value.IsNull);
                _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.ReceiverOpt.Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            MethodSymbol invokedMethod = node.Method;
            TypeSymbol requestedAttributeType = invokedMethod.TypeArguments[0];
            CSharpAttributeData candidateAttribute;
            if (argumentValue.Kind == CompileTimeValueKind.Simple)
            {
                Debug.Assert(argumentValue is TypeValue
                             && invokedMethod.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T));
                TypeSymbol type = ((TypeValue)argumentValue).Type;
                return StaticValueUtils.LookupCustomAttributeValue(node.Syntax, requestedAttributeType, type.GetAttributes(), _diagnostics, out candidateAttribute);
            }
            else
            {
                Debug.Assert(argumentValue.Kind == CompileTimeValueKind.Complex);
                if (argumentValue is MethodInfoValue)
                {
                    Debug.Assert(invokedMethod.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T));
                    MethodSymbol method = ((MethodInfoValue)argumentValue).Method;
                    return StaticValueUtils.LookupCustomAttributeValue(node.Syntax, requestedAttributeType, method.GetAttributes(), _diagnostics, out candidateAttribute);
                }
                else
                {
                    Debug.Assert(argumentValue is ParameterInfoValue
                                 && invokedMethod.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T2));
                    ParameterSymbol parameter = ((ParameterInfoValue)argumentValue).Parameter;
                    return StaticValueUtils.LookupCustomAttributeValue(node.Syntax, requestedAttributeType, parameter.GetAttributes(), _diagnostics, out candidateAttribute);
                }
            }
        }

        private CompileTimeValue VisitAddTraitCall(BoundCall node, ImmutableArray<CompileTimeValue> argumentValues)
        {
            Debug.Assert(argumentValues.Length >= 1);

            CompileTimeValue hostTypeValue = argumentValues[0];

            if (hostTypeValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)hostTypeValue).Value.IsNull);
                _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.Arguments[0].Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            Debug.Assert(hostTypeValue is TypeValue);
            TypeSymbol hostType = ((TypeValue)hostTypeValue).Type;

            if (hostType != _targetType)
            {
                _diagnostics.Add(ErrorCode.ERR_MetaclassModificationOnNonTargetType, node.Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            MethodSymbol method = node.Method;
            TypeSymbol traitType;
            if (method.Arity == 0)
            {
                Debug.Assert(argumentValues.Length == 2);
                CompileTimeValue traitTypeValue = argumentValues[1];

                if (traitTypeValue is ConstantStaticValue)
                {
                    Debug.Assert(((ConstantStaticValue)traitTypeValue).Value.IsNull);
                    _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.Arguments[1].Syntax.Location);
                    throw new ExecutionInterruptionException(InterruptionKind.Throw);
                }

                Debug.Assert(traitTypeValue is TypeValue);
                traitType = ((TypeValue)hostTypeValue).Type;
            }
            else
            {
                Debug.Assert(method.Arity == 1 && argumentValues.Length == 1 && !method.TypeArguments.IsDefaultOrEmpty);
                traitType = method.TypeArguments[0];
            }

            if (traitType.BaseType != _compilation.GetWellKnownType(WellKnownType.CSharp_Meta_Trait))
            {
                _diagnostics.Add(ErrorCode.ERR_NotATrait, node.Syntax.Location, traitType.Name);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            var hostSourceType = hostType as SourceMemberContainerTypeSymbol;
            Debug.Assert(hostSourceType != null);

            var traitSourceType = traitType as SourceMemberContainerTypeSymbol;
            if (traitSourceType == null)
            {
                _diagnostics.Add(ErrorCode.ERR_NonSourceTrait, node.Syntax.Location, traitType.Name);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }
            else if (traitSourceType.IsGenericType)
            {
                _diagnostics.Add(ErrorCode.ERR_GenericTraitTypesNotSupported, node.Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            hostSourceType.AddTrait(traitSourceType, _diagnostics, node.Syntax.Location, _cancellationToken);
            return new ConstantStaticValue(ConstantValue.Null);
        }

        private CompileTimeValue VisitApplyDecoratorCall(BoundCall node, ImmutableArray<CompileTimeValue> argumentValues)
        {
            Debug.Assert(argumentValues.Length == 2);
            CompileTimeValue methodInfoValue = argumentValues[0];
            var decoratorValue = argumentValues[1] as DecoratorValue;

            Debug.Assert(methodInfoValue is MethodInfoValue && decoratorValue != null);
            var method = ((MethodInfoValue)methodInfoValue).Method as SourceMethodSymbol;
            if (method == null || method.ContainingType != _targetType)
            {
                _diagnostics.Add(ErrorCode.ERR_MetaclassModificationOnNonTargetType, node.Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }
            else
            {
                var decoratorData = decoratorValue.CreateDecoratorData(new SimpleSyntaxReference(node.Syntax));
                method.ApplyDecorator(decoratorData);
                return new ConstantStaticValue(ConstantValue.Null);
            }
        }

        private CompileTimeValue VisitParameterTypeCall(BoundCall node, ImmutableArray<CompileTimeValue> argumentValues)
        {
            Debug.Assert(argumentValues.Length == 2);
            CompileTimeValue methodInfoValue = argumentValues[0];
            CompileTimeValue parameterIndexValue = argumentValues[1];

            Debug.Assert(methodInfoValue is MethodInfoValue && parameterIndexValue is ConstantStaticValue);
            MethodSymbol method = ((MethodInfoValue)methodInfoValue).Method;
            ConstantValue parameterIndexConstantValue = ((ConstantStaticValue)parameterIndexValue).Value;
            Debug.Assert(parameterIndexConstantValue.SpecialType == SpecialType.System_Int32);
            int parameterIndex = parameterIndexConstantValue.Int32Value;

            if (parameterIndex < 0 || parameterIndex >= method.ParameterCount)
            {
                _diagnostics.Add(ErrorCode.ERR_StaticIndexOutOfBounds, node.Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }
            else
            {
                return new TypeValue(method.ParameterTypes[parameterIndex]);
            }
        }

        private CompileTimeValue VisitThisObjectTypeCall(BoundCall node, ImmutableArray<CompileTimeValue> argumentValues)
        {
            Debug.Assert(argumentValues.Length == 1);
            CompileTimeValue methodInfoValue = argumentValues[0];

            Debug.Assert(methodInfoValue is MethodInfoValue);
            MethodSymbol method = ((MethodInfoValue)methodInfoValue).Method;

            TypeSymbol thisObjectType = method.IsStatic
                                            ? _compilation.GetSpecialType(SpecialType.System_Void)
                                            : method.ContainingType;
            return new TypeValue(thisObjectType);
        }
    }
}
