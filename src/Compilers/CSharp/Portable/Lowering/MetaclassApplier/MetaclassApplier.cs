using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Symbols.Meta;
using Roslyn.Utilities;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal sealed partial class MetaclassApplier : BoundTreeVisitor<object, CompileTimeValue>
    {
        private readonly CSharpCompilation _compilation;
        private readonly SourceMemberContainerTypeSymbol _targetType;
        private readonly MetaclassBindingTimeAnalyzer _bindingTimeAnalyzer;
        private readonly SourceMemberMethodSymbol _applicationMethod;
        private readonly DiagnosticBag _diagnostics;
        private readonly Dictionary<Symbol, CompileTimeValue> _variableValues;

        public MetaclassApplier(
            CSharpCompilation compilation,
            SourceMemberContainerTypeSymbol targetType,
            MetaclassBindingTimeAnalyzer bindingTimeAnalyzer,
            SourceMemberMethodSymbol applicationMethod,
            DiagnosticBag diagnostics)
        {
            _compilation = compilation;
            _targetType = targetType;
            _bindingTimeAnalyzer = bindingTimeAnalyzer;
            _applicationMethod = applicationMethod;
            _diagnostics = diagnostics;
            _variableValues = new Dictionary<Symbol, CompileTimeValue>();
        }

        public static void ApplyMetaclass(
            CSharpCompilation compilation,
            SourceMemberContainerTypeSymbol targetType,
            MetaclassData metaclassData,
            TypeCompilationState compilationState,
            DiagnosticBag diagnostics,
            CancellationToken cancellationToken)
        {
            Debug.Assert(targetType != null);
            Debug.Assert(compilationState != null);
            Debug.Assert(metaclassData != null);

            SourceMemberMethodSymbol applicationMethod = GetApplicationMethod(compilation, metaclassData, compilationState, diagnostics);
            if (applicationMethod == null)
            {
                return;
            }
            cancellationToken.ThrowIfCancellationRequested();

            BoundBlock applicationMethodBody = applicationMethod.EarlyBoundBody;
            if (applicationMethodBody == null)
            {
                diagnostics.Add(ErrorCode.ERR_MetaclassApplicationMethodWithoutBody, metaclassData.ApplicationSyntaxReference.GetLocation(), applicationMethod.ContainingType);
                return;
            }
            else if (applicationMethodBody.HasAnyErrors)
            {
                return;
            }

            // Perform binding-time analysis on the decorator method's body in order to identify variables, expressions and statements which can be statically evaluated
            var bindingTimeAnalyzer = new MetaclassBindingTimeAnalyzer(compilation, diagnostics, metaclassData.ApplicationSyntaxReference.GetLocation(), applicationMethod);
            if (!bindingTimeAnalyzer.PerformAnalysis())
            {
                return;
            }
            cancellationToken.ThrowIfCancellationRequested();

            // Create a synthetic node factory and perform the rewrite
            var metaclassApplier = new MetaclassApplier(compilation, targetType, bindingTimeAnalyzer, applicationMethod, diagnostics);
            metaclassApplier.PerformApplication(applicationMethodBody);
        }

        public override CompileTimeValue DefaultVisit(BoundNode node, object arg)
        {
            // Any nodes which are not specially handled should already have been rejected by the MetaclassBindingTimeAnalyzer, or should not be traversed at all
            throw ExceptionUtilities.Unreachable;
        }

        public override CompileTimeValue VisitArrayAccess(BoundArrayAccess node, object arg)
        {
            CompileTimeValue expressionValue = Visit(node.Expression, arg);
            Debug.Assert(expressionValue != null && expressionValue is ArrayValue);

            ImmutableArray<CompileTimeValue> indicesValues = VisitList(node.Indices, arg);
            Debug.Assert(indicesValues.Length == 1 && indicesValues[0] is ConstantStaticValue);

            var arrayValue = (ArrayValue)expressionValue;
            ConstantValue indexConstant = ((ConstantStaticValue)indicesValues[0]).Value;
            Debug.Assert(indexConstant.IsIntegral);

            return arrayValue.Array[indexConstant.Int32Value];
        }

        public override CompileTimeValue VisitArrayCreation(BoundArrayCreation node, object arg)
        {
            // TODO: Consider supporting creation of static-valued arrays

            // Such nodes should already have been rejected by the MetaclassBindingTimeAnalyzer
            throw ExceptionUtilities.Unreachable;
        }

        public override CompileTimeValue VisitArrayInitialization(BoundArrayInitialization node, object arg)
        {
            // TODO: Consider supporting creation of static-valued arrays

            // Such nodes should already have been rejected by the MetaclassBindingTimeAnalyzer
            throw ExceptionUtilities.Unreachable;
        }

        public override CompileTimeValue VisitArrayLength(BoundArrayLength node, object arg)
        {
            CompileTimeValue expressionValue = Visit(node.Expression, arg);
            Debug.Assert(expressionValue != null && expressionValue is ArrayValue);

            int arrayLength = ((ArrayValue)expressionValue).Array.Length;
            return new ConstantStaticValue(ConstantValue.Create(arrayLength));
        }

        public override CompileTimeValue VisitAsOperator(BoundAsOperator node, object arg)
        {
            CompileTimeValue operandValue = Visit(node.Operand, arg);
            if (operandValue.Kind == CompileTimeValueKind.Simple && !node.Conversion.IsUserDefined)
            {
                return StaticValueUtils.FoldConversion(node.Syntax, operandValue, node.Conversion.Kind, node.Type, _diagnostics);
            }
            else
            {
                Debug.Assert(operandValue.Kind == CompileTimeValueKind.Complex
                             && (node.Conversion.IsBoxing || node.Conversion.Kind == ConversionKind.ImplicitReference));
                return operandValue;
            }
        }

        public override CompileTimeValue VisitAssignmentOperator(BoundAssignmentOperator node, object arg)
        {
            BoundExpression left = node.Left;
            Symbol symbol;
            ArrayValue arrayValue = null;
            int arrayIndex = 0;
            switch (left.Kind)
            {
                case BoundKind.Local:
                    symbol = ((BoundLocal)left).LocalSymbol;
                    break;

                case BoundKind.Parameter:
                    symbol = ((BoundParameter)left).ParameterSymbol;
                    break;

                case BoundKind.ArrayAccess:
                    var arrayAccess = (BoundArrayAccess)left;
                    BoundExpression arrayExpression = arrayAccess.Expression;
                    CompileTimeValue expressionValue = Visit(arrayExpression, arg);
                    Debug.Assert(expressionValue != null && expressionValue is ArrayValue);

                    ImmutableArray<CompileTimeValue> indicesValues = VisitList(arrayAccess.Indices, arg);
                    Debug.Assert(indicesValues.Length == 1 && indicesValues[0] is ConstantStaticValue);

                    arrayValue = (ArrayValue)expressionValue;
                    ConstantValue indexConstant = ((ConstantStaticValue)indicesValues[0]).Value;
                    Debug.Assert(indexConstant.IsIntegral);

                    arrayIndex = indexConstant.Int32Value;

                    BindingTimeAnalysisResult arrayExpressionBindingTimeResult = _bindingTimeAnalyzer.Visit(arrayExpression, BindingTimeAnalyzerFlags.None);
                    symbol = arrayExpressionBindingTimeResult.MainSymbol;
                    break;

                default:
                    throw ExceptionUtilities.Unreachable;
            }

            CompileTimeValue rightValue = Visit(node.Right, arg);
            Debug.Assert(rightValue.Kind != CompileTimeValueKind.Dynamic);

            if (symbol != null)
            {
                if (arrayValue != null)
                {
                    _variableValues[symbol] = arrayValue.SetItem(arrayIndex, rightValue);
                }
                else
                {
                    _variableValues[symbol] = rightValue;
                }
            }

            return rightValue;
        }

        public override CompileTimeValue VisitBinaryOperator(BoundBinaryOperator node, object arg)
        {
            CompileTimeValue leftValue = Visit(node.Left, arg);

            if (node.OperatorKind.IsLogical() && leftValue.Kind == CompileTimeValueKind.Simple)
            {
                // Handle early evaluation termination for logical && and || operations
                Debug.Assert(leftValue is ConstantStaticValue);
                ConstantValue leftConstantValue = ((ConstantStaticValue)leftValue).Value;
                Debug.Assert(leftConstantValue.IsBoolean);
                switch (node.OperatorKind.Operator())
                {
                    case BinaryOperatorKind.And:
                        if (!leftConstantValue.BooleanValue)
                        {
                            return leftValue;
                        }
                        break;

                    case BinaryOperatorKind.Or:
                        if (leftConstantValue.BooleanValue)
                        {
                            return leftValue;
                        }
                        break;
                }
            }

            CompileTimeValue rightValue = Visit(node.Right, arg);
            Debug.Assert(leftValue.Kind != CompileTimeValueKind.Dynamic && rightValue.Kind != CompileTimeValueKind.Dynamic);
            return StaticValueUtils.FoldBinaryOperator(node.Syntax, node.OperatorKind, leftValue, rightValue, node.Type.SpecialType, _compilation, _diagnostics);
        }

        public override CompileTimeValue VisitBlock(BoundBlock node, object arg)
        {
            VisitStatements(node.Statements, arg);
            return null;
        }

        public override CompileTimeValue VisitBreakStatement(BoundBreakStatement node, object arg)
        {
            throw new ExecutionInterruptionException(InterruptionKind.Break);
        }

        public override CompileTimeValue VisitCompoundAssignmentOperator(BoundCompoundAssignmentOperator node, object arg)
        {
            BoundExpression left = node.Left;
            CompileTimeValue leftValue;
            Symbol symbol;
            ArrayValue arrayValue = null;
            int arrayIndex = 0;
            switch (left.Kind)
            {
                case BoundKind.Local:
                    symbol = ((BoundLocal)left).LocalSymbol;
                    leftValue = _variableValues[symbol];
                    break;

                case BoundKind.Parameter:
                    symbol = ((BoundParameter)left).ParameterSymbol;
                    leftValue = _variableValues[symbol];
                    break;

                case BoundKind.ArrayAccess:
                    var arrayAccess = (BoundArrayAccess)left;
                    BoundExpression arrayExpression = arrayAccess.Expression;
                    CompileTimeValue expressionValue = Visit(arrayExpression, arg);
                    Debug.Assert(expressionValue != null && expressionValue is ArrayValue);

                    ImmutableArray<CompileTimeValue> indicesValues = VisitList(arrayAccess.Indices, arg);
                    Debug.Assert(indicesValues.Length == 1 && indicesValues[0] is ConstantStaticValue);

                    arrayValue = (ArrayValue)expressionValue;
                    ConstantValue indexConstant = ((ConstantStaticValue)indicesValues[0]).Value;
                    Debug.Assert(indexConstant.IsIntegral);

                    arrayIndex = indexConstant.Int32Value;

                    BindingTimeAnalysisResult arrayExpressionBindingTimeResult = _bindingTimeAnalyzer.Visit(arrayExpression, BindingTimeAnalyzerFlags.None);
                    symbol = arrayExpressionBindingTimeResult.MainSymbol;

                    leftValue = arrayValue.Array[arrayIndex];
                    break;

                default:
                    throw ExceptionUtilities.Unreachable;
            }

            CompileTimeValue rightValue = Visit(node.Right, arg);
            Debug.Assert((leftValue is ConstantStaticValue || leftValue is EnumValue)
                         && (rightValue is ConstantStaticValue || rightValue is EnumValue));

            CompileTimeValue value = StaticValueUtils.FoldBinaryOperator(node.Syntax, node.Operator.Kind, leftValue, rightValue, node.Type.SpecialType, _compilation, _diagnostics);

            if (symbol != null)
            {
                if (arrayValue != null)
                {
                    _variableValues[symbol] = arrayValue.SetItem(arrayIndex, rightValue);
                }
                else
                {
                    _variableValues[symbol] = rightValue;
                }
            }

            return value;
        }

        public override CompileTimeValue VisitConditionalAccess(BoundConditionalAccess node, object arg)
        {
            CompileTimeValue receiverValue = Visit(node.Receiver, arg);
            Debug.Assert(receiverValue.Kind != CompileTimeValueKind.Dynamic);
            if (CheckIsNullStaticValue(receiverValue))
            {
                return new ConstantStaticValue(ConstantValue.Null);
            }
            else
            {
                return Visit(node.AccessExpression, arg);
            }
        }

        public override CompileTimeValue VisitConditionalOperator(BoundConditionalOperator node, object arg)
        {
            CompileTimeValue conditionValue = Visit(node.Condition, arg);
            Debug.Assert(conditionValue is ConstantStaticValue);
            ConstantValue conditionConstantValue = ((ConstantStaticValue)conditionValue).Value;
            Debug.Assert(conditionConstantValue.IsBoolean);
            if (conditionConstantValue.BooleanValue)
            {
                return Visit(node.Consequence, arg);
            }
            else
            {
                return Visit(node.Alternative, arg);
            }
        }

        public override CompileTimeValue VisitContinueStatement(BoundContinueStatement node, object arg)
        {
            throw new ExecutionInterruptionException(InterruptionKind.Continue);
        }

        public override CompileTimeValue VisitConversion(BoundConversion node, object arg)
        {
            CompileTimeValue operandValue = Visit(node.Operand, arg);
            if (operandValue.Kind == CompileTimeValueKind.Simple && !node.ConversionKind.IsUserDefinedConversion())
            {
                return StaticValueUtils.FoldConversion(node.Syntax, operandValue, node.ConversionKind, node.Type, _diagnostics);
            }
            else
            {
                Debug.Assert(operandValue.Kind == CompileTimeValueKind.Complex
                             && (node.ConversionKind == ConversionKind.Boxing || node.ConversionKind == ConversionKind.ImplicitReference));
                return operandValue;
            }
        }

        public override CompileTimeValue VisitDefaultOperator(BoundDefaultOperator node, object arg)
        {
            TypeSymbol type = node.Type;
            if (type.IsClassType())
            {
                return new ConstantStaticValue(ConstantValue.Null);
            }
            else
            {
                Debug.Assert(MetaUtils.CheckIsSimpleStaticValueType(type, _compilation));
                if (type.SpecialType != SpecialType.None)
                {
                    return new ConstantStaticValue(ConstantValue.Default(type.SpecialType));
                }
                else
                {
                    Debug.Assert(type.IsEnumType());
                    return new EnumValue(type, ConstantValue.Default(type.GetEnumUnderlyingType().SpecialType));
                }
            }
        }

        public override CompileTimeValue VisitDoStatement(BoundDoStatement node, object arg)
        {
            bool performMoreIterations = true;
            do
            {
                try
                {
                    Visit(node.Body, arg);
                }
                catch (ExecutionInterruptionException e)
                {
                    switch (e.Interruption)
                    {
                        case InterruptionKind.Break:
                            performMoreIterations = false;
                            break;

                        case InterruptionKind.Continue:
                            break;

                        default:
                            throw;
                    }
                }

                if (performMoreIterations)
                {
                    CompileTimeValue conditionValue = Visit(node.Condition, arg);
                    Debug.Assert(conditionValue is ConstantStaticValue);
                    ConstantValue conditionConstantValue = ((ConstantStaticValue)conditionValue).Value;
                    Debug.Assert(conditionConstantValue.IsBoolean);
                    performMoreIterations = conditionConstantValue.BooleanValue;
                }
            }
            while (performMoreIterations);
            return null;
        }

        public override CompileTimeValue VisitExpressionStatement(BoundExpressionStatement node, object arg)
        {
            Visit(node.Expression, arg);
            return null;
        }

        public override CompileTimeValue VisitFieldAccess(BoundFieldAccess node, object arg)
        {
            // Verify that the field is an enum constant
            FieldSymbol fieldSymbol = node.FieldSymbol;
            TypeSymbol fieldType = fieldSymbol.Type;
            Debug.Assert(fieldSymbol.IsStatic && fieldSymbol.HasConstantValue && fieldType.IsEnumType());

            TypeSymbol underlyingType = fieldType.EnumUnderlyingType();
            Debug.Assert(underlyingType.SpecialType != SpecialType.None);

            return new EnumValue(fieldType, ConstantValue.Create(fieldSymbol.ConstantValue, underlyingType.SpecialType));
        }

        public override CompileTimeValue VisitForEachStatement(BoundForEachStatement node, object arg)
        {
            LocalSymbol iterationVariable = node.IterationVariable;
            CompileTimeValue expressionValue = Visit(node.Expression, arg);
            Debug.Assert(expressionValue.Kind != CompileTimeValueKind.Dynamic && expressionValue.Kind != CompileTimeValueKind.ArgumentArray);

            if (expressionValue.Kind == CompileTimeValueKind.Simple)
            {
                Debug.Assert(expressionValue is ConstantStaticValue && ((ConstantStaticValue)expressionValue).Value.IsNull);
                _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.Expression.Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            Debug.Assert(expressionValue is ArrayValue);
            var arrayValue = (ArrayValue)expressionValue;
            bool performMoreIterations = true;
            int iterationIndex = 0;
            do
            {
                if (iterationIndex < arrayValue.Array.Length)
                {
                    _variableValues[iterationVariable] = arrayValue.Array[iterationIndex];
                    try
                    {
                        Visit(node.Body, arg);
                    }
                    catch (ExecutionInterruptionException e)
                    {
                        switch (e.Interruption)
                        {
                            case InterruptionKind.Break:
                                performMoreIterations = false;
                                break;

                            case InterruptionKind.Continue:
                                break;

                            default:
                                throw;
                        }
                    }
                    iterationIndex++;
                }
                else
                {
                    performMoreIterations = false;
                }
            }
            while (performMoreIterations);
            return null;
        }

        public override CompileTimeValue VisitForStatement(BoundForStatement node, object arg)
        {
            Visit(node.Initializer, arg);

            bool performMoreIterations = true;
            do
            {
                CompileTimeValue conditionValue = Visit(node.Condition, arg);
                Debug.Assert(conditionValue is ConstantStaticValue);
                ConstantValue conditionConstantValue = ((ConstantStaticValue)conditionValue).Value;
                Debug.Assert(conditionConstantValue.IsBoolean);
                if (!conditionConstantValue.BooleanValue)
                {
                    break;
                }

                try
                {
                    Visit(node.Body, arg);
                }
                catch (ExecutionInterruptionException e)
                {
                    switch (e.Interruption)
                    {
                        case InterruptionKind.Break:
                            performMoreIterations = false;
                            break;

                        case InterruptionKind.Continue:
                            break;

                        default:
                            throw;
                    }
                }

                if (performMoreIterations)
                {
                    Visit(node.Increment, arg);
                }
            }
            while (performMoreIterations);
            return null;
        }

        public override CompileTimeValue VisitIfStatement(BoundIfStatement node, object arg)
        {
            CompileTimeValue conditionValue = Visit(node.Condition, arg);
            Debug.Assert(conditionValue is ConstantStaticValue);
            ConstantValue conditionConstantValue = ((ConstantStaticValue)conditionValue).Value;
            Debug.Assert(conditionConstantValue.IsBoolean);
            if (conditionConstantValue.BooleanValue)
            {
                Visit(node.Consequence, arg);
            }
            else
            {
                Visit(node.AlternativeOpt, arg);
            }
            return null;
        }

        public override CompileTimeValue VisitIncrementOperator(BoundIncrementOperator node, object arg)
        {
            BoundExpression operand = node.Operand;
            CompileTimeValue operandValue;
            Symbol symbol;
            ArrayValue arrayValue = null;
            int arrayIndex = 0;
            switch (operand.Kind)
            {
                case BoundKind.Local:
                    symbol = ((BoundLocal)operand).LocalSymbol;
                    operandValue = _variableValues[symbol];
                    break;

                case BoundKind.Parameter:
                    symbol = ((BoundParameter)operand).ParameterSymbol;
                    operandValue = _variableValues[symbol];
                    break;

                case BoundKind.ArrayAccess:
                    var arrayAccess = (BoundArrayAccess)operand;
                    BoundExpression arrayExpression = arrayAccess.Expression;
                    CompileTimeValue expressionValue = Visit(arrayExpression, arg);
                    Debug.Assert(expressionValue != null && expressionValue is ArrayValue);

                    ImmutableArray<CompileTimeValue> indicesValues = VisitList(arrayAccess.Indices, arg);
                    Debug.Assert(indicesValues.Length == 1 && indicesValues[0] is ConstantStaticValue);

                    arrayValue = (ArrayValue)expressionValue;
                    ConstantValue indexConstant = ((ConstantStaticValue)indicesValues[0]).Value;
                    Debug.Assert(indexConstant.IsIntegral);

                    arrayIndex = indexConstant.Int32Value;

                    BindingTimeAnalysisResult arrayExpressionBindingTimeResult = _bindingTimeAnalyzer.Visit(arrayExpression, BindingTimeAnalyzerFlags.None);
                    symbol = arrayExpressionBindingTimeResult.MainSymbol;

                    operandValue = arrayValue.Array[arrayIndex];
                    break;

                default:
                    throw ExceptionUtilities.Unreachable;
            }

            Debug.Assert(operandValue is ConstantStaticValue || operandValue is EnumValue);

            UnaryOperatorKind operatorKind = node.OperatorKind;
            CompileTimeValue newOperandValue = StaticValueUtils.FoldIncrementOperator(node.Syntax, operatorKind, operandValue, node.Type.SpecialType, _diagnostics);
            CompileTimeValue value;
            switch (operatorKind.Operator())
            {
                case UnaryOperatorKind.PrefixDecrement:
                case UnaryOperatorKind.PrefixIncrement:
                    value = operandValue;
                    break;

                case UnaryOperatorKind.PostfixDecrement:
                case UnaryOperatorKind.PostfixIncrement:
                    value = newOperandValue;
                    break;

                default:
                    throw ExceptionUtilities.Unreachable;
            }

            if (symbol != null)
            {
                if (arrayValue != null)
                {
                    _variableValues[symbol] = arrayValue.SetItem(arrayIndex, newOperandValue);
                }
                else
                {
                    _variableValues[symbol] = newOperandValue;
                }
            }
            return value;
        }

        public override CompileTimeValue VisitIsOperator(BoundIsOperator node, object arg)
        {
            CompileTimeValue operandValue = Visit(node.Operand, arg);
            TypeSymbol targetType = node.TargetType.Type;

            if (targetType.IsObjectType())
            {
                return new ConstantStaticValue(ConstantValue.Create(true));
            }
            else if (operandValue.Kind == CompileTimeValueKind.Simple)
            {
                if (operandValue is ConstantStaticValue)
                {
                    ConstantValue operandConstantValue = ((ConstantStaticValue)operandValue).Value;
                    if (operandConstantValue.IsNull)
                    {
                        bool acceptsNull = targetType.IsReferenceType || targetType.IsNullableTypeOrTypeParameter();
                        return new ConstantStaticValue(ConstantValue.Create(acceptsNull));
                    }
                    else
                    {
                        Debug.Assert(operandConstantValue.SpecialType != SpecialType.None);
                        TypeSymbol constantType = _compilation.GetSpecialType(operandConstantValue.SpecialType);
                        return new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsAssignableFrom(targetType, constantType)));
                    }
                }
                else if (operandValue is EnumValue)
                {
                    TypeSymbol enumType = ((EnumValue)operandValue).EnumType;
                    return new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsAssignableFrom(targetType, enumType)));
                }
                else
                {
                    Debug.Assert(operandValue is TypeValue);
                    TypeSymbol typeType = _compilation.GetWellKnownType(WellKnownType.System_Type);
                    return new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsAssignableFrom(targetType, typeType)));
                }
            }
            else
            {
                Debug.Assert(operandValue.Kind == CompileTimeValueKind.Complex);
                if (operandValue is ArrayValue)
                {
                    TypeSymbol arrayType = ((ArrayValue)operandValue).ArrayType;
                    return new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsAssignableFrom(targetType, arrayType)));
                }
                else if (operandValue is MethodInfoValue)
                {
                    TypeSymbol methodInfoType = _compilation.GetWellKnownType(WellKnownType.System_Reflection_MethodInfo);
                    return new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsAssignableFrom(targetType, methodInfoType)));
                }
                else if (operandValue is ParameterInfoValue)
                {
                    TypeSymbol parameterInfoType = _compilation.GetWellKnownType(WellKnownType.System_Reflection_ParameterInfo);
                    return new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsAssignableFrom(targetType, parameterInfoType)));
                }
                else
                {
                    Debug.Assert(operandValue is AttributeValue);
                    TypeSymbol attributeType = ((AttributeValue)operandValue).Attribute.AttributeClass;
                    return new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsAssignableFrom(targetType, attributeType)));
                }
            }
        }

        public override CompileTimeValue VisitLabelStatement(BoundLabelStatement node, object arg)
        {
            return null;
        }

        public override CompileTimeValue VisitLabeledStatement(BoundLabeledStatement node, object arg)
        {
            Visit(node.Body, arg);
            return null;
        }

        public override CompileTimeValue VisitLiteral(BoundLiteral node, object arg)
        {
            return new ConstantStaticValue(node.ConstantValue);
        }

        public override CompileTimeValue VisitLocal(BoundLocal node, object arg)
        {
            Debug.Assert(_variableValues.ContainsKey(node.LocalSymbol));
            return _variableValues[node.LocalSymbol];
        }

        public override CompileTimeValue VisitLocalDeclaration(BoundLocalDeclaration node, object arg)
        {
            BoundExpression initializerOpt = node.InitializerOpt;
            if (initializerOpt != null)
            {
                CompileTimeValue initializerValue = Visit(initializerOpt, arg);
                Debug.Assert(initializerValue.Kind != CompileTimeValueKind.Dynamic);
                _variableValues[node.LocalSymbol] = initializerValue;
            }
            return null;
        }

        public override CompileTimeValue VisitLoweredConditionalAccess(BoundLoweredConditionalAccess node, object arg)
        {
            CompileTimeValue receiverValue = Visit(node.Receiver, arg);
            Debug.Assert(receiverValue.Kind != CompileTimeValueKind.Dynamic);
            if (CheckIsNullStaticValue(receiverValue))
            {
                return Visit(node.WhenNullOpt, arg) ?? new ConstantStaticValue(ConstantValue.Null);
            }
            else
            {
                return Visit(node.WhenNotNull, arg);
            }
        }

        public override CompileTimeValue VisitMultipleLocalDeclarations(BoundMultipleLocalDeclarations node, object arg)
        {
            VisitStatements(node.LocalDeclarations, arg);
            return null;
        }

        public override CompileTimeValue VisitNameOfOperator(BoundNameOfOperator node, object arg)
        {
            ConstantValue nameConstant = node.ConstantValue;
            Debug.Assert(nameConstant != null && nameConstant.IsString);
            return new ConstantStaticValue(nameConstant);
        }

        public override CompileTimeValue VisitNoOpStatement(BoundNoOpStatement node, object arg)
        {
            return null;
        }

        public override CompileTimeValue VisitNullCoalescingOperator(BoundNullCoalescingOperator node, object arg)
        {
            CompileTimeValue leftOperandValue = Visit(node.LeftOperand, arg);
            Debug.Assert(leftOperandValue.Kind != CompileTimeValueKind.Dynamic);

            if (CheckIsNullStaticValue(leftOperandValue))
            {
                return Visit(node.RightOperand, arg);
            }
            else
            {
                return leftOperandValue;
            }
        }

        public override CompileTimeValue VisitObjectCreationExpression(BoundObjectCreationExpression node, object arg)
        {
            // Only decorator creation expressions using the default constructor and no property/field initializer can be allowed through by the binding time analysis
            TypeSymbol type = node.Type;
            HashSet<DiagnosticInfo> useSiteDiagnostics = null;
            Debug.Assert(type.IsDerivedFrom(_compilation.GetWellKnownType(WellKnownType.CSharp_Meta_Decorator), false, ref useSiteDiagnostics));
            Debug.Assert(type is NamedTypeSymbol && node.Arguments.IsEmpty && node.InitializerExpressionOpt == null);

            return new DecoratorValue((NamedTypeSymbol)type, node.Constructor);
        }

        public override CompileTimeValue VisitParameter(BoundParameter node, object arg)
        {
            Debug.Assert(_variableValues.ContainsKey(node.ParameterSymbol));
            return _variableValues[node.ParameterSymbol];
        }

        public override CompileTimeValue VisitReturnStatement(BoundReturnStatement node, object arg)
        {
            Debug.Assert(node.ExpressionOpt == null);
            throw new ExecutionInterruptionException(InterruptionKind.Return);
        }

        public override CompileTimeValue VisitSequencePointExpression(BoundSequencePointExpression node, object arg)
        {
            return Visit(node.Expression, arg);
        }

        public override CompileTimeValue VisitStatementList(BoundStatementList node, object arg)
        {
            VisitStatements(node.Statements, arg);
            return null;
        }

        public override CompileTimeValue VisitSwitchStatement(BoundSwitchStatement node, object arg)
        {
            CompileTimeValue expressionValue = Visit(node.BoundExpression, arg);
            Debug.Assert(expressionValue is ConstantStaticValue || expressionValue is EnumValue);

            BoundSwitchSection matchingSection = null;
            foreach (BoundSwitchSection switchSection in node.SwitchSections)
            {
                bool hasMatchingLabel = false;
                foreach (BoundSwitchLabel switchLabel in switchSection.BoundSwitchLabels)
                {
                    CompileTimeValue labelExpressionValue = Visit(switchLabel.ExpressionOpt, arg);
                    if (labelExpressionValue != null)
                    {
                        Debug.Assert(labelExpressionValue is ConstantStaticValue || labelExpressionValue is EnumValue);
                        if (Equals(expressionValue, labelExpressionValue))
                        {
                            hasMatchingLabel = true;
                            break;
                        }
                    }
                }

                if (hasMatchingLabel)
                {
                    matchingSection = switchSection;
                }
            }

            if (matchingSection == null)
            {
                foreach (BoundSwitchSection switchSection in node.SwitchSections)
                {
                    bool hasMatchingLabel = false;
                    foreach (BoundSwitchLabel switchLabel in switchSection.BoundSwitchLabels)
                    {
                        if (switchLabel.ExpressionOpt == null)
                        {
                            // If the switch label has no expression, it must be the default case
                            hasMatchingLabel = true;
                            break;
                        }
                    }

                    if (hasMatchingLabel)
                    {
                        matchingSection = switchSection;
                    }
                }
            }

            if (matchingSection != null)
            {
                // If we have found a matching section, execute it
                try
                {
                    VisitStatements(matchingSection.Statements, arg);
                }
                catch (ExecutionInterruptionException e) when (e.Interruption == InterruptionKind.Break)
                {
                    // Capture interruptions caused by break statements but not any of the others
                }
            }
            return null;
        }

        public override CompileTimeValue VisitThrowStatement(BoundThrowStatement node, object arg)
        {
            // TODO: Handle compile-time errors and their reporting
            throw new ExecutionInterruptionException(InterruptionKind.Throw);
        }

        public override CompileTimeValue VisitTypeExpression(BoundTypeExpression node, object arg)
        {
            return new TypeValue(node.Type);
        }

        public override CompileTimeValue VisitTypeOfOperator(BoundTypeOfOperator node, object arg)
        {
            return new TypeValue(node.SourceType.Type);
        }

        public override CompileTimeValue VisitUnaryOperator(BoundUnaryOperator node, object arg)
        {
            CompileTimeValue operandValue = Visit(node.Operand, arg);
            Debug.Assert(operandValue.Kind == CompileTimeValueKind.Simple);

            return StaticValueUtils.FoldUnaryOperator(node.Syntax, node.OperatorKind, operandValue, node.Type.SpecialType, _compilation, _diagnostics);
        }

        public override CompileTimeValue VisitWhileStatement(BoundWhileStatement node, object arg)
        {
            bool performMoreIterations = true;
            do
            {
                CompileTimeValue conditionValue = Visit(node.Condition, arg);
                Debug.Assert(conditionValue is ConstantStaticValue);
                ConstantValue conditionConstantValue = ((ConstantStaticValue)conditionValue).Value;
                Debug.Assert(conditionConstantValue.IsBoolean);
                if (!conditionConstantValue.BooleanValue)
                {
                    break;
                }

                try
                {
                    Visit(node.Body, arg);
                }
                catch (ExecutionInterruptionException e)
                {
                    switch (e.Interruption)
                    {
                        case InterruptionKind.Break:
                            performMoreIterations = false;
                            break;

                        case InterruptionKind.Continue:
                            break;

                        default:
                            throw;
                    }
                }
            }
            while (performMoreIterations);
            return null;
        }

        public void PerformApplication(BoundBlock applicationMethodBody)
        {
            _variableValues.Clear();
            _variableValues.Add(_applicationMethod.Parameters[0], new TypeValue(_targetType));
            try
            {
                Visit(applicationMethodBody, null);
            }
            catch (ExecutionInterruptionException)
            {
            }
        }

        public ImmutableArray<CompileTimeValue> VisitList<T>(ImmutableArray<T> list, object arg)
            where T : BoundNode
        {
            if (list.IsDefaultOrEmpty)
            {
                return ImmutableArray<CompileTimeValue>.Empty;
            }
            else
            {
                var itemValues = new CompileTimeValue[list.Length];
                for (int i = 0; i < list.Length; i++)
                {
                    itemValues[i] = Visit(list[i], arg);
                }

                return itemValues.ToImmutableArray();
            }
        }

        public void VisitStatements<T>(ImmutableArray<T> list, object arg)
            where T : BoundStatement
        {
            if (!list.IsDefaultOrEmpty)
            {
                foreach (T statement in list)
                {
                    Visit(statement, arg);
                }
            }
        }

        private static SourceMemberMethodSymbol GetApplicationMethod(
            CSharpCompilation compilation,
            MetaclassData metaclassData,
            TypeCompilationState compilationState,
            DiagnosticBag diagnostics)
        {
            var metaclassClass = metaclassData.MetaclassClass as SourceNamedTypeSymbol;
            if (metaclassClass == null)
            {
                diagnostics.Add(ErrorCode.ERR_NonSourceMetaclass, metaclassData.ApplicationSyntaxReference.GetLocation(), metaclassClass);
                return null;
            }

            SourceMemberMethodSymbol applicationMethod = metaclassClass.FindMetaclassApplicationMethod();
            while (applicationMethod == null)
            {
                metaclassClass = metaclassClass.BaseType as SourceNamedTypeSymbol;
                if (metaclassData == null)
                {
                    throw ExceptionUtilities.Unreachable;
                }
                applicationMethod = metaclassClass.FindMetaclassApplicationMethod();
            }
            return applicationMethod;
        }

        private static BoundExpression StripConversions(BoundExpression expression)
        {
            while (expression != null)
            {
                switch (expression.Kind)
                {
                    case BoundKind.Conversion:
                        expression = ((BoundConversion)expression).Operand;
                        break;

                    case BoundKind.AsOperator:
                        expression = ((BoundAsOperator)expression).Operand;
                        break;

                    default:
                        return expression;
                }
            }
            return null;
        }

        private static bool CheckIsNullStaticValue(CompileTimeValue value)
        {
            return value.Kind == CompileTimeValueKind.Simple
                   && value is ConstantStaticValue
                   && ((ConstantStaticValue)value).Value.IsNull;
        }
    }
}
