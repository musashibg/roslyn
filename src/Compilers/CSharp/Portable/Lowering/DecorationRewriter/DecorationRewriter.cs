using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Symbols.Meta;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Roslyn.Utilities;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal sealed partial class DecorationRewriter : BoundTreeVisitor<ImmutableDictionary<Symbol, CompileTimeValue>, DecorationRewriteResult>
    {
        private readonly CSharpCompilation _compilation;
        private readonly MethodSymbol _targetMethod;
        private readonly BoundBlock _targetBody;
        private readonly DecoratedMemberKind _targetMemberKind;
        private readonly int _argumentCount;
        private readonly SourceMemberMethodSymbol _decoratorMethod;
        private readonly int _decoratorOrdinal;
        private readonly ImmutableDictionary<Symbol, BoundExpression> _decoratorArguments;
        private readonly SyntheticBoundNodeFactory _factory;
        private readonly DecorationBindingTimeAnalyzer _bindingTimeAnalyzer;
        private readonly DiagnosticBag _diagnostics;
        private readonly List<EncapsulatingStatementKind> _encapsulatingStatements;
        private readonly VariableNameGenerator _variableNameGenerator;

        private DecorationRewriterFlags _flags;
        private ImmutableDictionary<Symbol, LocalSymbol> _replacementSymbols;
        private ImmutableArray<BoundStatement>.Builder _splicedStatementsBuilder;
        private ImmutableArray<LocalSymbol>.Builder _blockLocalsBuilder;
        private LabelSymbol _staticSwitchEndLabel;
        private int _spliceOrdinal;

        public DecorationRewriter(
            CSharpCompilation compilation,
            MethodSymbol targetMethod,
            BoundBlock targetBody,
            DecoratedMemberKind targetMemberKind,
            SourceMemberMethodSymbol decoratorMethod,
            int decoratorOrdinal,
            ImmutableDictionary<Symbol, BoundExpression> decoratorArguments,
            SyntheticBoundNodeFactory factory,
            DecorationBindingTimeAnalyzer bindingTimeAnalyzer,
            DiagnosticBag diagnostics)
        {
            _compilation = compilation;
            _targetMethod = targetMethod;
            _targetBody = targetBody;
            _targetMemberKind = targetMemberKind;
            switch (_targetMemberKind)
            {
                case DecoratedMemberKind.Constructor:
                case DecoratedMemberKind.IndexerGet:
                case DecoratedMemberKind.Method:
                    _argumentCount = _targetMethod.ParameterCount;
                    break;

                case DecoratedMemberKind.IndexerSet:
                    _argumentCount = _targetMethod.ParameterCount - 1;
                    break;

                case DecoratedMemberKind.Destructor:
                case DecoratedMemberKind.PropertyGet:
                case DecoratedMemberKind.PropertySet:
                    _argumentCount = 0;
                    break;

                default:
                    throw ExceptionUtilities.Unreachable;
            }

            _decoratorMethod = decoratorMethod;
            _decoratorOrdinal = decoratorOrdinal;
            _decoratorArguments = decoratorArguments;
            _factory = factory;
            _factory.CurrentMethod = targetMethod;
            Debug.Assert(factory.CurrentType == targetMethod.ContainingType);
            _bindingTimeAnalyzer = bindingTimeAnalyzer;
            _diagnostics = diagnostics;

            _encapsulatingStatements = new List<EncapsulatingStatementKind>();
            _variableNameGenerator = new VariableNameGenerator(_targetMethod.Parameters.Select(p => p.Name));
            _flags = DecorationRewriterFlags.None;
            _replacementSymbols = ImmutableDictionary<Symbol, LocalSymbol>.Empty;
            _spliceOrdinal = 0;
        }

        public static BoundBlock Rewrite(
            CSharpCompilation compilation,
            MethodSymbol targetMethod,
            BoundBlock targetMethodBody,
            DecoratorData decoratorData,
            int decoratorOrdinal,
            TypeCompilationState compilationState,
            DiagnosticBag diagnostics,
            CancellationToken cancellationToken)
        {
            Debug.Assert(targetMethodBody != null);
            Debug.Assert(compilationState != null);
            Debug.Assert(decoratorData != null);

            DecoratedMemberKind targetMemberKind = GetTargetMemberKind(targetMethod);
            cancellationToken.ThrowIfCancellationRequested();
            SourceMemberMethodSymbol decoratorMethod = GetDecoratorMethod(compilation, targetMethod, targetMemberKind, decoratorData, compilationState, diagnostics);
            if (decoratorMethod == null)
            {
                return targetMethodBody;
            }
            cancellationToken.ThrowIfCancellationRequested();

            BoundBlock decoratorBody = decoratorMethod.EarlyBoundBody;
            if (decoratorBody == null)
            {
                diagnostics.Add(ErrorCode.ERR_DecoratorMethodWithoutBody, decoratorData.ApplicationSyntaxReference.GetLocation(), decoratorMethod.ContainingType);
                return targetMethodBody;
            }
            else if (decoratorBody.HasAnyErrors)
            {
                return targetMethodBody;
            }

            ImmutableDictionary<Symbol, BoundExpression> decoratorArguments = BuildDecoratorArguments(decoratorData);
            cancellationToken.ThrowIfCancellationRequested();

            // Perform binding-time analysis on the decorator method's body in order to identify variables, expressions and statements which can be statically evaluated
            var bindingTimeAnalyzer = new DecorationBindingTimeAnalyzer(
                compilation,
                diagnostics,
                decoratorData.ApplicationSyntaxReference.GetLocation(),
                targetMemberKind,
                decoratorMethod,
                decoratorArguments);
            if (!bindingTimeAnalyzer.PerformAnalysis())
            {
                return targetMethodBody;
            }
            cancellationToken.ThrowIfCancellationRequested();

            // Create a synthetic node factory and perform the rewrite
            CSharpSyntaxNode methodSyntax = targetMethodBody.Syntax;
            var factory = new SyntheticBoundNodeFactory(targetMethod, methodSyntax, compilationState, diagnostics);
            var decorationRewriter = new DecorationRewriter(
                compilation,
                targetMethod,
                targetMethodBody,
                targetMemberKind,
                decoratorMethod,
                decoratorOrdinal,
                decoratorArguments,
                factory,
                bindingTimeAnalyzer,
                diagnostics);
            return decorationRewriter.Rewrite(decoratorBody);
        }

        public override DecorationRewriteResult DefaultVisit(BoundNode node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            // Any nodes which are not specially handled should already have been rejected by the DecorationBindingTimeAnalyzer, or should not be traversed at all
            throw ExceptionUtilities.Unreachable;
        }

        public override DecorationRewriteResult Visit(BoundNode node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            if (node == null)
            {
                return null;
            }

            if (node is BoundStatement)
            {
                ImmutableArray<BoundStatement>.Builder outerSplicedStatements = _splicedStatementsBuilder;

                _splicedStatementsBuilder = ImmutableArray.CreateBuilder<BoundStatement>();

                DecorationRewriteResult result = base.Visit(node, variableValues);

                if (_splicedStatementsBuilder.Count > 0)
                {
                    if (result.MustEmit)
                    {
                        if (result.Node is BoundStatement)
                        {
                            _splicedStatementsBuilder.Add((BoundStatement)result.Node);
                        }
                        else if (result.Node is BoundStatementList)
                        {
                            _splicedStatementsBuilder.AddRange(((BoundStatementList)result.Node).Statements);
                        }
                    }
                    ImmutableArray<BoundStatement> statements = _splicedStatementsBuilder.ToImmutable();

                    result = new DecorationRewriteResult(
                        new BoundStatementList(node.Syntax, statements) { WasCompilerGenerated = true },
                        result.UpdatedVariableValues,
                        true,
                        result.PossibleContinuations);
                }

                _splicedStatementsBuilder = outerSplicedStatements;

                return result;
            }
            else
            {
                return base.Visit(node, variableValues);
            }
        }

        public override DecorationRewriteResult VisitAnonymousObjectCreationExpression(BoundAnonymousObjectCreationExpression node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            ImmutableArray<DecorationRewriteResult> argumentResults = VisitSequentialList(node.Arguments, ref variableValues);

            ImmutableArray<DecorationRewriteResult> declarationResults = VisitSequentialList(node.Declarations, ref variableValues);

            return new DecorationRewriteResult(
                node.Update(
                    node.Constructor,
                    argumentResults.SelectAsArray(r => (BoundExpression)r.Node),
                    declarationResults.SelectAsArray(r => (BoundAnonymousPropertyDeclaration)r.Node),
                    node.Type),
                variableValues,
                true,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitAnonymousPropertyDeclaration(BoundAnonymousPropertyDeclaration node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            // A lone anonymous property declaration should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(node, variableValues, false, CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitArrayAccess(BoundArrayAccess node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult expressionResult = Visit(node.Expression, variableValues);
            variableValues = expressionResult.UpdatedVariableValues;
            ImmutableArray<DecorationRewriteResult> indicesResults = VisitSequentialList(node.Indices, ref variableValues);

            BoundExpression rewrittenNode = null;
            CompileTimeValue value;
            ConstantValue indexConstant;
            Debug.Assert(expressionResult.Value.Kind != CompileTimeValueKind.Simple);
            switch (expressionResult.Value.Kind)
            {
                case CompileTimeValueKind.Complex:
                    Debug.Assert(expressionResult.Value is ArrayValue && indicesResults.Length == 1 && indicesResults[0].Value is ConstantStaticValue);
                    var arrayValue = (ArrayValue)expressionResult.Value;
                    indexConstant = ((ConstantStaticValue)indicesResults[0].Value).Value;
                    Debug.Assert(indexConstant.IsIntegral);
                    value = arrayValue.Array[indexConstant.Int32Value];
                    if (value.Kind == CompileTimeValueKind.Simple)
                    {
                        rewrittenNode = MakeSimpleStaticValueExpression(value, node.Type, node.Syntax);
                    }
                    break;

                case CompileTimeValueKind.ArgumentArray:
                    Debug.Assert(expressionResult.Value is ArgumentArrayValue && indicesResults.Length == 1 && indicesResults[0].Value is ConstantStaticValue);
                    value = CompileTimeValue.Dynamic;
                    var argumentArrayValue = (ArgumentArrayValue)expressionResult.Value;
                    indexConstant = ((ConstantStaticValue)indicesResults[0].Value).Value;
                    Debug.Assert(indexConstant.IsIntegral);
                    Symbol argumentSymbol = argumentArrayValue.ArgumentSymbols[indexConstant.Int32Value];
                    if (argumentSymbol.Kind == SymbolKind.Parameter)
                    {
                        rewrittenNode = MetaUtils.ConvertIfNeeded(
                            node.Type,
                            new BoundParameter(node.Syntax, (ParameterSymbol)argumentSymbol) { WasCompilerGenerated = true },
                            _compilation);
                    }
                    else
                    {
                        Debug.Assert(argumentSymbol.Kind == SymbolKind.Local);
                        rewrittenNode = MetaUtils.ConvertIfNeeded(
                            node.Type,
                            new BoundLocal(node.Syntax, (LocalSymbol)argumentSymbol, null, ((LocalSymbol)argumentSymbol).Type) { WasCompilerGenerated = true },
                            _compilation);
                    }
                    break;

                default:
                    value = CompileTimeValue.Dynamic;
                    break;
            }
            if (rewrittenNode == null)
            {
                rewrittenNode = node.Update(
                    (BoundExpression)expressionResult.Node,
                    indicesResults.SelectAsArray(r => (BoundExpression)r.Node),
                    node.Type);
            }

            // A lone array access should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(
                rewrittenNode,
                variableValues,
                false,
                value);
        }

        public override DecorationRewriteResult VisitArrayCreation(BoundArrayCreation node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            // TODO: Consider supporting creation of static-valued arrays
            ImmutableArray<DecorationRewriteResult> boundsResults = VisitSequentialList(node.Bounds, ref variableValues);

            DecorationRewriteResult initializerResult = Visit(node.InitializerOpt, variableValues);
            if (initializerResult != null)
            {
                variableValues = initializerResult.UpdatedVariableValues;
            }

            // A lone array creation should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(
                node.Update(boundsResults.SelectAsArray(r => (BoundExpression)r.Node), (BoundArrayInitialization)initializerResult?.Node, node.Type),
                variableValues,
                false,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitArrayInitialization(BoundArrayInitialization node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            // TODO: Consider supporting creation of static-valued arrays
            ImmutableArray<DecorationRewriteResult> initializersResults = VisitSequentialList(node.Initializers, ref variableValues);

            // A lone array initialization should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(
                node.Update(initializersResults.SelectAsArray(r => (BoundExpression)r.Node)),
                variableValues,
                false,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitArrayLength(BoundArrayLength node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult expressionResult = Visit(node.Expression, variableValues);
            CompileTimeValue expressionValue = expressionResult.Value;

            ConstantValue constantLength = null;
            Debug.Assert(expressionValue.Kind != CompileTimeValueKind.Simple);
            switch (expressionValue.Kind)
            {
                case CompileTimeValueKind.Complex:
                    Debug.Assert(expressionValue is ArrayValue);
                    constantLength = ConstantValue.Create(((ArrayValue)expressionValue).Array.Length);
                    break;

                case CompileTimeValueKind.ArgumentArray:
                    constantLength = ConstantValue.Create(((ArgumentArrayValue)expressionValue).ArgumentSymbols.Length);
                    break;
            }

            if (constantLength == null)
            {
                // A lone array length access should never be a stand-alone statement, so we return MustEmit = false
                return new DecorationRewriteResult(
                    node.Update((BoundExpression)expressionResult.Node, node.Type),
                    expressionResult.UpdatedVariableValues,
                    false,
                    CompileTimeValue.Dynamic);
            }
            else
            {
                return new DecorationRewriteResult(
                    new BoundLiteral(node.Syntax, constantLength, node.Type) { WasCompilerGenerated = true },
                    expressionResult.UpdatedVariableValues,
                    false,
                    new ConstantStaticValue(constantLength));
            }
        }

        public override DecorationRewriteResult VisitAsOperator(BoundAsOperator node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult operandResult = Visit(node.Operand, variableValues);
            CompileTimeValue operandValue = operandResult.Value;

            BoundExpression rewrittenNode = null;
            CompileTimeValue value;
            if (operandValue.Kind == CompileTimeValueKind.Simple && !node.Conversion.IsUserDefined)
            {
                value = StaticValueUtils.FoldConversion(node.Syntax, operandValue, node.Conversion.Kind, node.Type, _diagnostics);
                rewrittenNode = MakeSimpleStaticValueExpression(value, node.Type, node.Syntax);
            }
            else if (operandValue.Kind == CompileTimeValueKind.Complex
                     && (node.Conversion.IsBoxing || node.Conversion.Kind == ConversionKind.ImplicitReference))
            {
                value = operandValue;
            }
            else
            {
                value = CompileTimeValue.Dynamic;
            }

            if (rewrittenNode == null)
            {
                rewrittenNode = node.Update((BoundExpression)operandResult.Node, node.TargetType, node.Conversion, node.Type);
            }

            // A lone as operator should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(rewrittenNode, variableValues, false, value);
        }

        public override DecorationRewriteResult VisitAssignmentOperator(BoundAssignmentOperator node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            BoundExpression left = node.Left;
            DecorationRewriteResult leftResult = Visit(left, variableValues);
            // If the left side is a variable whose type has been altered, a conversion has been introduced for it and we want to strip it
            if (leftResult.Node.Kind == BoundKind.Conversion)
            {
                leftResult = new DecorationRewriteResult(MetaUtils.StripConversions((BoundExpression)leftResult.Node), leftResult.UpdatedVariableValues, leftResult.MustEmit, leftResult.Value);
            }

            DecorationRewriteResult rightResult;

            BindingTimeAnalysisResult leftBindingTimeResult = _bindingTimeAnalyzer.Visit(node.Left, BindingTimeAnalyzerFlags.None);
            if (leftBindingTimeResult.BindingTime == BindingTime.Dynamic && leftBindingTimeResult.MainSymbol != null
                && _decoratorMethod.DecoratorMethodVariableTypes[leftBindingTimeResult.MainSymbol].Kind == ExtendedTypeKind.ArgumentArray)
            {
                // If we are assigning a value to an argument array variable with dynamic binding time, we need to make sure that the right-side expression evaluates to a dynamic argument array
                rightResult = VisitWithExtraFlags(DecorationRewriterFlags.ExpectedDynamicArgumentArray, node.Right, leftResult.UpdatedVariableValues);
            }
            else
            {
                rightResult = Visit(node.Right, leftResult.UpdatedVariableValues);
            }
            CompileTimeValue rightValue = rightResult.Value;

            BoundExpression rewrittenNode = null;
            CompileTimeValue value = CompileTimeValue.Dynamic;
            BindingTime bindingTime;
            bool mustEmit = true;
            switch (left.Kind)
            {
                case BoundKind.Local:
                    LocalSymbol localSymbol = ((BoundLocal)left).LocalSymbol;
                    bindingTime = _bindingTimeAnalyzer.VariableBindingTimes[localSymbol];
                    if (bindingTime != BindingTime.Dynamic)
                    {
                        Debug.Assert(rightValue.Kind != CompileTimeValueKind.Dynamic);
                        variableValues = rightResult.UpdatedVariableValues.SetItem(localSymbol, rightValue);
                        rewrittenNode = MetaUtils.ConvertIfNeeded(node.Type, (BoundExpression)rightResult.Node, _compilation);
                        value = rightValue;
                        mustEmit = rightResult.MustEmit;
                    }
                    break;

                case BoundKind.Parameter:
                    ParameterSymbol parameterSymbol = ((BoundParameter)left).ParameterSymbol;
                    Debug.Assert(!_decoratorMethod.Parameters.Contains(parameterSymbol));
                    bindingTime = _bindingTimeAnalyzer.VariableBindingTimes[parameterSymbol];
                    if (bindingTime != BindingTime.Dynamic)
                    {
                        Debug.Assert(rightValue.Kind != CompileTimeValueKind.Dynamic);
                        variableValues = rightResult.UpdatedVariableValues.SetItem(parameterSymbol, rightValue);
                        rewrittenNode = MetaUtils.ConvertIfNeeded(node.Type, (BoundExpression)rightResult.Node, _compilation);
                        value = rightValue;
                        mustEmit = rightResult.MustEmit;
                    }
                    break;

                case BoundKind.ArrayAccess:
                    BoundExpression arrayExpression = ((BoundArrayAccess)left).Expression;
                    DecorationRewriteResult arrayExpressionResult = Visit(arrayExpression, variableValues);
                    CompileTimeValue arrayExpressionValue = arrayExpressionResult.Value;
                    Debug.Assert(arrayExpressionValue.Kind != CompileTimeValueKind.Simple);
                    if (arrayExpressionValue.Kind == CompileTimeValueKind.Complex)
                    {
                        Debug.Assert(arrayExpressionValue is ArrayValue && rightValue.Kind != CompileTimeValueKind.Dynamic);
                        variableValues = arrayExpressionResult.UpdatedVariableValues;
                        ImmutableArray<DecorationRewriteResult> indicesResults = VisitSequentialList(((BoundArrayAccess)left).Indices, ref variableValues);
                        Debug.Assert(indicesResults.Length == 1 && indicesResults[0].Value is ConstantStaticValue);
                        ConstantValue indexConstant = ((ConstantStaticValue)indicesResults[0].Value).Value;
                        Debug.Assert(indexConstant.IsIntegral);

                        BindingTimeAnalysisResult arrayExpressionBindingTimeResult = _bindingTimeAnalyzer.Visit(arrayExpression, BindingTimeAnalyzerFlags.None);
                        if (arrayExpressionBindingTimeResult.MainSymbol != null)
                        {
                            ArrayValue newArrayValue = ((ArrayValue)arrayExpressionValue).SetItem(indexConstant.Int32Value, rightValue);
                            variableValues = rightResult.UpdatedVariableValues.SetItem(arrayExpressionBindingTimeResult.MainSymbol, newArrayValue);
                        }

                        rewrittenNode = MetaUtils.ConvertIfNeeded(node.Type, (BoundExpression)rightResult.Node, _compilation);
                        value = rightValue;
                        mustEmit = rightResult.MustEmit;
                    }
                    break;
            }

            if (rewrittenNode == null)
            {
                // The left side might be a local variable containing a result value and thus might have had its type changed
                var rewrittenLeft = (BoundExpression)leftResult.Node;
                TypeSymbol leftType = rewrittenLeft.Type;
                rewrittenNode = node.Update(
                    rewrittenLeft,
                    MetaUtils.ConvertIfNeeded(leftType, (BoundExpression)rightResult.Node, _compilation),
                    node.RefKind,
                    leftType);
                variableValues = rightResult.UpdatedVariableValues;
            }
            return new DecorationRewriteResult(rewrittenNode, variableValues, mustEmit, value);
        }

        public override DecorationRewriteResult VisitAwaitExpression(BoundAwaitExpression node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult expressionResult = Visit(node.Expression, variableValues);
            Debug.Assert(expressionResult.Value.Kind == CompileTimeValueKind.Dynamic);

            return new DecorationRewriteResult(
                node.Update((BoundExpression)expressionResult.Node, node.GetAwaiter, node.IsCompleted, node.GetResult, node.Type),
                expressionResult.UpdatedVariableValues,
                true,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitBaseReference(BoundBaseReference node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            if (_flags.HasFlag(DecorationRewriterFlags.InDecoratorArgument))
            {
                return new DecorationRewriteResult(node, variableValues, false, CompileTimeValue.Dynamic);
            }
            else
            {
                throw ExceptionUtilities.Unreachable;
            }
        }

        public override DecorationRewriteResult VisitBinaryOperator(BoundBinaryOperator node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult leftResult = Visit(node.Left, variableValues);

            BoundExpression rewrittenNode = null;
            CompileTimeValue value = null;
            if (node.OperatorKind.IsLogical() && leftResult.Value.Kind == CompileTimeValueKind.Simple)
            {
                // Handle early evaluation termination for logical && and || operations
                Debug.Assert(leftResult.Value is ConstantStaticValue);
                ConstantValue leftConstantValue = ((ConstantStaticValue)leftResult.Value).Value;
                Debug.Assert(leftConstantValue.IsBoolean);
                switch (node.OperatorKind.Operator())
                {
                    case BinaryOperatorKind.And:
                        if (!leftConstantValue.BooleanValue)
                        {
                            value = leftResult.Value;
                            rewrittenNode = MakeSimpleStaticValueExpression(value, node.Type, node.Syntax);
                            variableValues = leftResult.UpdatedVariableValues;
                        }
                        break;

                    case BinaryOperatorKind.Or:
                        if (leftConstantValue.BooleanValue)
                        {
                            value = leftResult.Value;
                            rewrittenNode = MakeSimpleStaticValueExpression(value, node.Type, node.Syntax);
                            variableValues = leftResult.UpdatedVariableValues;
                        }
                        break;
                }
            }

            if (rewrittenNode == null)
            {
                DecorationRewriteResult rightResult = Visit(node.Right, leftResult.UpdatedVariableValues);
                variableValues = rightResult.UpdatedVariableValues;

                if (leftResult.Value.Kind != CompileTimeValueKind.Dynamic && rightResult.Value.Kind != CompileTimeValueKind.Dynamic)
                {
                    value = StaticValueUtils.FoldBinaryOperator(node.Syntax, node.OperatorKind, leftResult.Value, rightResult.Value, node.Type.SpecialType, _compilation, _diagnostics);
                    rewrittenNode = MakeSimpleStaticValueExpression(value, node.Type, node.Syntax);
                }
                else
                {
                    rewrittenNode = node.Update(
                        node.OperatorKind,
                        (BoundExpression)leftResult.Node,
                        (BoundExpression)rightResult.Node,
                        node.ConstantValueOpt,
                        node.MethodOpt,
                        node.ResultKind,
                        node.Type);
                    value = CompileTimeValue.Dynamic;
                }
            }

            // A lone binary operator should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(rewrittenNode, variableValues, false, value);
        }

        public override DecorationRewriteResult VisitBlock(BoundBlock node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            ImmutableArray<LocalSymbol>.Builder outerBlockLocalsBuilder = _blockLocalsBuilder;
            _blockLocalsBuilder = ImmutableArray.CreateBuilder<LocalSymbol>();

            ImmutableHashSet<ExecutionContinuation> possibleContinuations;
            ImmutableArray<DecorationRewriteResult> statementsResults = VisitAndTrimStatements(node.Statements, ref variableValues, out possibleContinuations);

            ImmutableArray<LocalSymbol> locals = _blockLocalsBuilder.ToImmutable();
            _blockLocalsBuilder = outerBlockLocalsBuilder;

            ImmutableArray<BoundStatement> rewrittenStatements = FlattenStatementList(statementsResults);

            if (locals.IsEmpty)
            {
                if (rewrittenStatements.IsEmpty)
                {
                    return MakeNoOpResult(node.Syntax, variableValues, possibleContinuations);
                }
                else if (rewrittenStatements.Length == 1)
                {
                    return new DecorationRewriteResult(rewrittenStatements[0], variableValues, true, possibleContinuations);
                }
            }

            return new DecorationRewriteResult(
                node.Update(locals, rewrittenStatements),
                variableValues,
                !statementsResults.IsEmpty,
                possibleContinuations);
        }

        public override DecorationRewriteResult VisitBreakStatement(BoundBreakStatement node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            Debug.Assert(_encapsulatingStatements.Any(es => es.Statement() == EncapsulatingStatementKind.Switch || es.Statement() == EncapsulatingStatementKind.Loop));
            bool mustEmit = true;
            bool isInSwitch = false;
            // isInDynamicBranch = true, if and only if the encapsulating loop or switch statement is static, but the break statement occurs in a dynamically-accessible branch in its body
            bool isInDynamicBranch = false;
            for (int i = _encapsulatingStatements.Count - 1; i >= 0; i--)
            {
                EncapsulatingStatementKind encapsulatingStatementKind = _encapsulatingStatements[i];
                if (encapsulatingStatementKind.Statement() == EncapsulatingStatementKind.Switch)
                {
                    isInSwitch = true;
                    mustEmit = encapsulatingStatementKind.IsDynamic() || isInDynamicBranch;
                    isInDynamicBranch = !encapsulatingStatementKind.IsDynamic();
                    break;
                }
                else if (encapsulatingStatementKind.Statement() == EncapsulatingStatementKind.Loop)
                {
                    mustEmit = encapsulatingStatementKind.IsDynamic() || isInDynamicBranch;
                    isInDynamicBranch = !encapsulatingStatementKind.IsDynamic();
                    break;
                }
                else if (encapsulatingStatementKind.IsDynamic())
                {
                    isInDynamicBranch = true;
                }
            }
            if (isInSwitch && isInDynamicBranch)
            {
                // If the break statement belongs to a switch statement with a static expression, but occurs in a dynamicaly controlled branch in the appropriate switch section,
                // we replace the break statement with an unconditional jump to a label at the end of the switch statement's residual code; the switch statement itself does not exist
                // in the residual code, which is why we cannot simply leave the break statement in place
                Debug.Assert(mustEmit && _staticSwitchEndLabel != null);
                return new DecorationRewriteResult(new BoundGotoStatement(node.Syntax, _staticSwitchEndLabel), variableValues, mustEmit, ExecutionContinuation.Break);
            }
            else
            {
                return new DecorationRewriteResult(node, variableValues, mustEmit, ExecutionContinuation.Break);
            }
        }

        public override DecorationRewriteResult VisitCatchBlock(BoundCatchBlock node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            int encapsulatingStatementIndex = _encapsulatingStatements.Count;
            _encapsulatingStatements.Add(EncapsulatingStatementKind.CatchBlock | EncapsulatingStatementKind.DynamicallyControlled);

            LocalSymbol localOpt = node.LocalOpt == null ? null : GetReplacementSymbol(node.LocalOpt);

            DecorationRewriteResult exceptionSourceResult = Visit(node.ExceptionSourceOpt, variableValues);
            if (exceptionSourceResult != null)
            {
                variableValues = exceptionSourceResult.UpdatedVariableValues;
            }

            DecorationRewriteResult exceptionFilterResult = Visit(node.ExceptionFilterOpt, variableValues);
            if (exceptionFilterResult != null)
            {
                variableValues = exceptionFilterResult.UpdatedVariableValues;
            }

            DecorationRewriteResult bodyResult = Visit(node.Body, variableValues);

            _encapsulatingStatements.RemoveAt(encapsulatingStatementIndex);
            Debug.Assert(_encapsulatingStatements.Count == encapsulatingStatementIndex);

            return new DecorationRewriteResult(
                node.Update(
                    localOpt,
                    (BoundExpression)exceptionSourceResult?.Node,
                    node.ExceptionTypeOpt,
                    (BoundExpression)exceptionFilterResult?.Node,
                    GetBlock(bodyResult.Node, node.Body.Syntax),
                    node.IsSynthesizedAsyncCatchAll),
                bodyResult.UpdatedVariableValues,
                true,
                bodyResult.PossibleContinuations);
        }

        public override DecorationRewriteResult VisitCollectionElementInitializer(BoundCollectionElementInitializer node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            ImmutableArray<DecorationRewriteResult> argumentsResults = VisitSequentialList(node.Arguments, ref variableValues);

            // A lone collection element initializer should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(
                node.Update(
                    node.AddMethod,
                    argumentsResults.SelectAsArray(r => (BoundExpression)r.Node),
                    node.Expanded,
                    node.ArgsToParamsOpt,
                    node.InvokedAsExtensionMethod,
                    node.ResultKind,
                    node.Type),
                variableValues,
                false,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitCollectionInitializerExpression(BoundCollectionInitializerExpression node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            ImmutableArray<DecorationRewriteResult> initializersResults = VisitSequentialList(node.Initializers, ref variableValues);

            // A lone collection initializer should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(
                node.Update(initializersResults.SelectAsArray(r => (BoundExpression)r.Node), node.Type),
                variableValues,
                false,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitComplexConditionalReceiver(BoundComplexConditionalReceiver node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult valueTypeReceiverResult = Visit(node.ValueTypeReceiver, variableValues);

            DecorationRewriteResult referenceTypeReceiverResult = Visit(node.ReferenceTypeReceiver, variableValues);

            // A lone complex conditional receiver should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(
                node.Update((BoundExpression)valueTypeReceiverResult.Node, (BoundExpression)referenceTypeReceiverResult.Node, node.Type),
                UnifyVariableValues(valueTypeReceiverResult.UpdatedVariableValues, referenceTypeReceiverResult.UpdatedVariableValues),
                false,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitCompoundAssignmentOperator(BoundCompoundAssignmentOperator node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            BoundExpression left = node.Left;
            DecorationRewriteResult leftResult = Visit(left, variableValues);
            // If the left side is a variable, its type should not have been altered, and thus no conversion should have been introduced
            Debug.Assert(leftResult.Node.Kind != BoundKind.Conversion);
            CompileTimeValue leftValue = leftResult.Value;

            DecorationRewriteResult rightResult = Visit(node.Right, leftResult.UpdatedVariableValues);
            CompileTimeValue rightValue = rightResult.Value;

            BoundExpression rewrittenNode = null;
            CompileTimeValue value = CompileTimeValue.Dynamic;
            bool mustEmit = true;
            if (leftValue.Kind == CompileTimeValueKind.Simple)
            {
                Debug.Assert((leftValue is ConstantStaticValue || leftValue is EnumValue)
                             && rightValue.Kind == CompileTimeValueKind.Simple
                             && (rightValue is ConstantStaticValue || rightValue is EnumValue));

                value = StaticValueUtils.FoldBinaryOperator(node.Syntax, node.Operator.Kind, leftValue, rightValue, node.Type.SpecialType, _compilation, _diagnostics);
                rewrittenNode = MakeSimpleStaticValueExpression(value, node.Type, node.Syntax);
                mustEmit = false;

                switch (left.Kind)
                {
                    case BoundKind.Local:
                        LocalSymbol localSymbol = ((BoundLocal)left).LocalSymbol;
                        Debug.Assert(_bindingTimeAnalyzer.VariableBindingTimes[localSymbol] == BindingTime.StaticSimpleValue);
                        variableValues = rightResult.UpdatedVariableValues.SetItem(localSymbol, value);
                        break;

                    case BoundKind.Parameter:
                        ParameterSymbol parameterSymbol = ((BoundParameter)left).ParameterSymbol;
                        Debug.Assert(!_decoratorMethod.Parameters.Contains(parameterSymbol)
                                     && _bindingTimeAnalyzer.VariableBindingTimes[parameterSymbol] == BindingTime.StaticSimpleValue);
                        variableValues = rightResult.UpdatedVariableValues.SetItem(parameterSymbol, value);
                        break;

                    case BoundKind.ArrayAccess:
                        BoundExpression arrayExpression = ((BoundArrayAccess)left).Expression;
                        DecorationRewriteResult arrayExpressionResult = Visit(arrayExpression, variableValues);
                        CompileTimeValue arrayExpressionValue = arrayExpressionResult.Value;
                        Debug.Assert(arrayExpressionValue.Kind == CompileTimeValueKind.Complex && arrayExpressionValue is ArrayValue);
                        variableValues = arrayExpressionResult.UpdatedVariableValues;
                        ImmutableArray<DecorationRewriteResult> indicesResults = VisitSequentialList(((BoundArrayAccess)left).Indices, ref variableValues);
                        Debug.Assert(indicesResults.Length == 1 && indicesResults[0].Value is ConstantStaticValue);
                        ConstantValue indexConstant = ((ConstantStaticValue)indicesResults[0].Value).Value;
                        Debug.Assert(indexConstant.IsIntegral);

                        BindingTimeAnalysisResult arrayExpressionBindingTimeResult = _bindingTimeAnalyzer.Visit(arrayExpression, BindingTimeAnalyzerFlags.None);
                        if (arrayExpressionBindingTimeResult.MainSymbol != null)
                        {
                            ArrayValue newArrayValue = ((ArrayValue)arrayExpressionValue).SetItem(indexConstant.Int32Value, value);
                            variableValues = rightResult.UpdatedVariableValues.SetItem(arrayExpressionBindingTimeResult.MainSymbol, newArrayValue);
                        }
                        break;

                    default:
                        throw ExceptionUtilities.Unreachable;
                }
            }
            else
            {
                Debug.Assert(leftValue.Kind == CompileTimeValueKind.Dynamic);

                rewrittenNode = node.Update(
                    node.Operator,
                    (BoundExpression)leftResult.Node,
                    (BoundExpression)rightResult.Node,
                    node.LeftConversion,
                    node.FinalConversion,
                    node.ResultKind,
                    node.Type);
                variableValues = rightResult.UpdatedVariableValues;
                value = CompileTimeValue.Dynamic;
                mustEmit = true;
            }
            return new DecorationRewriteResult(rewrittenNode, variableValues, mustEmit, value);
        }

        public override DecorationRewriteResult VisitConditionalAccess(BoundConditionalAccess node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult receiverResult = Visit(node.Receiver, variableValues);
            CompileTimeValue receiverValue = receiverResult.Value;
            if (receiverValue.Kind != CompileTimeValueKind.Dynamic && CheckIsNullStaticValue(receiverValue))
            {
                var nullValue = new ConstantStaticValue(ConstantValue.Null);

                return new DecorationRewriteResult(
                    MakeSimpleStaticValueExpression(nullValue, node.Type, node.Syntax),
                    receiverResult.UpdatedVariableValues,
                    false,
                    nullValue);
            }
            else
            {
                DecorationRewriteResult accessExpressionResult = Visit(node.AccessExpression, receiverResult.UpdatedVariableValues);

                return new DecorationRewriteResult(
                    node.Update((BoundExpression)receiverResult.Node, (BoundExpression)accessExpressionResult.Node, node.Type),
                    accessExpressionResult.UpdatedVariableValues,
                    receiverResult.MustEmit || accessExpressionResult.MustEmit,
                    accessExpressionResult.Value);
            }
        }

        public override DecorationRewriteResult VisitConditionalGoto(BoundConditionalGoto node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            LabelSymbol label = node.Label;
            DecorationRewriteResult conditionResult = Visit(node.Condition, variableValues);
            CompileTimeValue conditionValue = conditionResult.Value;

            BoundStatement rewrittenNode;
            bool mustEmit;
            ImmutableHashSet<ExecutionContinuation> possibleContinuations;
            if (conditionValue.Kind == CompileTimeValueKind.Simple)
            {
                Debug.Assert(conditionValue is ConstantStaticValue);
                ConstantValue conditionConstantValue = ((ConstantStaticValue)conditionValue).Value;
                Debug.Assert(conditionConstantValue.IsBoolean);
                if (node.JumpIfTrue == conditionConstantValue.BooleanValue)
                {
                    rewrittenNode = new BoundGotoStatement(node.Syntax, label) { WasCompilerGenerated = true };
                    mustEmit = true;
                    possibleContinuations = ImmutableHashSet.Create<ExecutionContinuation>(new JumpContinuation(label));
                }
                else
                {
                    rewrittenNode = MakeNoOpStatement(node.Syntax);
                    mustEmit = false;
                    possibleContinuations = ImmutableHashSet.Create(ExecutionContinuation.NextStatement);
                }
            }
            else
            {
                Debug.Assert(conditionValue.Kind == CompileTimeValueKind.Dynamic);
                rewrittenNode = node.Update((BoundExpression)conditionResult.Node, node.JumpIfTrue, label);
                mustEmit = true;
                possibleContinuations = ImmutableHashSet.Create(ExecutionContinuation.NextStatement, new JumpContinuation(label));
            }

            return new DecorationRewriteResult(rewrittenNode, conditionResult.UpdatedVariableValues, mustEmit, possibleContinuations);
        }

        public override DecorationRewriteResult VisitConditionalOperator(BoundConditionalOperator node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult conditionResult = Visit(node.Condition, variableValues);
            CompileTimeValue conditionValue = conditionResult.Value;
            if (conditionValue.Kind == CompileTimeValueKind.Simple)
            {
                Debug.Assert(conditionResult.Value is ConstantStaticValue);
                ConstantValue conditionConstantValue = ((ConstantStaticValue)conditionValue).Value;
                Debug.Assert(conditionConstantValue.IsBoolean);
                if (conditionConstantValue.BooleanValue)
                {
                    return Visit(node.Consequence, conditionResult.UpdatedVariableValues);
                }
                else
                {
                    return Visit(node.Alternative, conditionResult.UpdatedVariableValues);
                }
            }
            else
            {
                Debug.Assert(conditionResult.Value.Kind == CompileTimeValueKind.Dynamic);

                DecorationRewriteResult consequenceResult = Visit(node.Consequence, conditionResult.UpdatedVariableValues);
                DecorationRewriteResult alternativeResult = Visit(node.Alternative, conditionResult.UpdatedVariableValues);

                // A lone conditional operator should never be a stand-alone statement, so we return MustEmit = false
                return new DecorationRewriteResult(
                    node.Update((BoundExpression)conditionResult.Node, (BoundExpression)consequenceResult.Node, (BoundExpression)alternativeResult.Node, node.ConstantValueOpt, node.Type),
                    UnifyVariableValues(consequenceResult.UpdatedVariableValues, alternativeResult.UpdatedVariableValues),
                    false,
                    CompileTimeValue.Dynamic);
            }
        }

        public override DecorationRewriteResult VisitConditionalReceiver(BoundConditionalReceiver node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            // A lone conditional receiver should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(node, variableValues, false, CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitContinueStatement(BoundContinueStatement node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            Debug.Assert(_encapsulatingStatements.Any(es => es.Statement() == EncapsulatingStatementKind.Loop));
            bool mustEmit = true;
            for (int i = _encapsulatingStatements.Count - 1; i >= 0; i--)
            {
                EncapsulatingStatementKind encapsulatingStatementKind = _encapsulatingStatements[i];
                if (encapsulatingStatementKind.Statement() == EncapsulatingStatementKind.Loop)
                {
                    mustEmit = encapsulatingStatementKind.IsDynamic();
                    break;
                }
            }
            return new DecorationRewriteResult(node, variableValues, mustEmit, ExecutionContinuation.Continue);
        }

        public override DecorationRewriteResult VisitConversion(BoundConversion node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult operandResult = Visit(node.Operand, variableValues);
            CompileTimeValue operandValue = operandResult.Value;

            BoundExpression rewrittenNode = null;
            CompileTimeValue value;
            if (operandValue.Kind == CompileTimeValueKind.Simple && !node.ConversionKind.IsUserDefinedConversion())
            {
                value = StaticValueUtils.FoldConversion(node.Syntax, operandValue, node.ConversionKind, node.Type, _diagnostics);
                rewrittenNode = MakeSimpleStaticValueExpression(value, node.Type, node.Syntax);
            }
            else if (operandValue.Kind == CompileTimeValueKind.Complex
                     && (node.ConversionKind == ConversionKind.Boxing || node.ConversionKind == ConversionKind.ImplicitReference))
            {
                value = operandValue;
            }
            else
            {
                value = CompileTimeValue.Dynamic;
            }

            if (rewrittenNode == null)
            {
                rewrittenNode = node.Update(
                    (BoundExpression)operandResult.Node,
                    node.ConversionKind,
                    node.ResultKind,
                    node.IsBaseConversion,
                    node.SymbolOpt,
                    node.Checked,
                    node.ExplicitCastInCode,
                    node.IsExtensionMethod,
                    node.IsArrayIndex,
                    node.ConstantValueOpt,
                    node.Type);
            }

            // A lone conversion should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(rewrittenNode, variableValues, false, value);
        }

        public override DecorationRewriteResult VisitDefaultOperator(BoundDefaultOperator node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            TypeSymbol type = node.Type;
            BoundExpression rewrittenNode;
            CompileTimeValue value;
            if (type.IsClassType())
            {
                value = new ConstantStaticValue(ConstantValue.Null);
                rewrittenNode = MakeSimpleStaticValueExpression(value, type, node.Syntax);
            }
            else if (MetaUtils.CheckIsSimpleStaticValueType(type, _compilation))
            {
                if (type.SpecialType != SpecialType.None)
                {
                    value = new ConstantStaticValue(ConstantValue.Default(type.SpecialType));
                }
                else
                {
                    Debug.Assert(type.IsEnumType());
                    value = new EnumValue(type, ConstantValue.Default(type.GetEnumUnderlyingType().SpecialType));
                }
                rewrittenNode = MakeSimpleStaticValueExpression(value, type, node.Syntax);
            }
            else
            {
                rewrittenNode = node;
                value = CompileTimeValue.Dynamic;
            }

            // A lone default expression should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(rewrittenNode, variableValues, false, value);
        }

        public override DecorationRewriteResult VisitDelegateCreationExpression(BoundDelegateCreationExpression node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult argumentResult = Visit(node.Argument, variableValues);

            return new DecorationRewriteResult(
                node.Update((BoundExpression)argumentResult.Node, node.MethodOpt, node.IsExtensionMethod, node.Type),
                argumentResult.UpdatedVariableValues,
                true,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitDoStatement(BoundDoStatement node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            ImmutableDictionary<Symbol, CompileTimeValue> originalVariableValues = variableValues;

            // At first, we assume that the loop is statically controlled
            bool isDynamicLoop = false;
            int encapsulatingStatementIndex = _encapsulatingStatements.Count;
            _encapsulatingStatements.Add(EncapsulatingStatementKind.Loop);

            ImmutableArray<BoundStatement>.Builder statementsBuilder = ImmutableArray.CreateBuilder<BoundStatement>();
            ImmutableHashSet<ExecutionContinuation> possibleContinuations = ImmutableHashSet.Create<ExecutionContinuation>();
            bool performMoreIterations;
            do
            {
                if (isDynamicLoop)
                {
                    statementsBuilder.Clear();
                    DecorationRewriteResult bodyResult = Visit(node.Body, originalVariableValues);
                    DecorationRewriteResult conditionResult = VisitWithExtraFlags(DecorationRewriterFlags.ProhibitSpliceLocation, node.Condition, bodyResult.UpdatedVariableValues);
                    statementsBuilder.Add(node.Update((BoundExpression)conditionResult.Node, (BoundStatement)bodyResult.Node, node.BreakLabel, node.ContinueLabel));
                    variableValues = conditionResult.UpdatedVariableValues;

                    performMoreIterations = false;
                    possibleContinuations = bodyResult.PossibleContinuations.Remove(ExecutionContinuation.Break).Remove(ExecutionContinuation.Continue);
                    if (bodyResult.HasBreakContinuation || bodyResult.HasContinueContinuation)
                    {
                        // If the loop may be interrupted by a break or continue statement, execution may proceed with the next statement after the loop
                        possibleContinuations = possibleContinuations.Add(ExecutionContinuation.NextStatement);
                    }
                }
                else
                {
                    DecorationRewriteResult bodyResult = Visit(node.Body, variableValues);
                    variableValues = bodyResult.UpdatedVariableValues;

                    if (bodyResult.HasNextStatementContinuation || bodyResult.HasContinueContinuation)
                    {
                        performMoreIterations = true;
                        if (bodyResult.PossibleContinuations.Count(ec => ec.AffectsLoopControlFlow()) > 1)
                        {
                            // The loop body contains multiple dynamically-reachable fragments that affect its flow, and it will perform more than one iteration.
                            // Therefore, we convert it into a dynamically-controlled loop and process it again
                            isDynamicLoop = true;
                            _encapsulatingStatements[encapsulatingStatementIndex] = EncapsulatingStatementKind.Loop | EncapsulatingStatementKind.DynamicallyControlled;
                            continue;
                        }
                    }
                    else
                    {
                        // All of the body's possible continuations terminate the loop
                        performMoreIterations = false;
                    }

                    if (bodyResult.MustEmit)
                    {
                        statementsBuilder.Add((BoundStatement)bodyResult.Node);
                    }

                    possibleContinuations = possibleContinuations.Union(bodyResult.PossibleContinuations.Remove(ExecutionContinuation.Break).Remove(ExecutionContinuation.Continue));
                    if (bodyResult.HasBreakContinuation || bodyResult.HasContinueContinuation)
                    {
                        // If the loop may be interrupted by a break or continue statement, execution may proceed with the next statement after the loop
                        possibleContinuations = possibleContinuations.Add(ExecutionContinuation.NextStatement);
                    }
                }

                if (performMoreIterations)
                {
                    DecorationRewriteResult conditionResult = VisitWithExtraFlags(DecorationRewriterFlags.ProhibitSpliceLocation, node.Condition, variableValues);
                    CompileTimeValue conditionValue = conditionResult.Value;
                    if (conditionValue.Kind == CompileTimeValueKind.Dynamic)
                    {
                        isDynamicLoop = true;
                        _encapsulatingStatements[encapsulatingStatementIndex] = EncapsulatingStatementKind.Loop | EncapsulatingStatementKind.DynamicallyControlled;
                    }
                    else
                    {
                        Debug.Assert(conditionValue.Kind == CompileTimeValueKind.Simple && conditionValue is ConstantStaticValue);
                        ConstantValue conditionConstantValue = ((ConstantStaticValue)conditionValue).Value;
                        Debug.Assert(conditionConstantValue.IsBoolean);
                        if (!conditionConstantValue.BooleanValue)
                        {
                            performMoreIterations = false;
                        }
                        variableValues = conditionResult.UpdatedVariableValues;
                    }
                }
            }
            while (performMoreIterations);
            Debug.Assert(!possibleContinuations.IsEmpty);

            _encapsulatingStatements.RemoveAt(encapsulatingStatementIndex);
            Debug.Assert(_encapsulatingStatements.Count == encapsulatingStatementIndex);

            switch (statementsBuilder.Count)
            {
                case 0:
                    return MakeNoOpResult(node.Syntax, variableValues, possibleContinuations);

                case 1:
                    return new DecorationRewriteResult(statementsBuilder[0], variableValues, true, possibleContinuations);

                default:
                    return new DecorationRewriteResult(
                        new BoundStatementList(node.Syntax, statementsBuilder.ToImmutable()) { WasCompilerGenerated = true },
                        variableValues,
                        true,
                        possibleContinuations);
            }
        }

        public override DecorationRewriteResult VisitDynamicCollectionElementInitializer(
            BoundDynamicCollectionElementInitializer node,
            ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            ImmutableArray<DecorationRewriteResult> argumentsResults = VisitSequentialList(node.Arguments, ref variableValues);

            return new DecorationRewriteResult(
                node.Update(argumentsResults.SelectAsArray(r => (BoundExpression)r.Node), node.ApplicableMethods, node.Type),
                variableValues,
                true,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitDynamicIndexerAccess(BoundDynamicIndexerAccess node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult receiverResult = Visit(node.ReceiverOpt, variableValues);
            if (receiverResult != null)
            {
                Debug.Assert(receiverResult.Value.Kind != CompileTimeValueKind.ArgumentArray);
                variableValues = receiverResult.UpdatedVariableValues;
            }

            ImmutableArray<DecorationRewriteResult> argumentsResults = VisitSequentialList(node.Arguments, ref variableValues);

            // A lone dynamic indexer access should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(
                node.Update(
                    (BoundExpression)receiverResult?.Node,
                    argumentsResults.SelectAsArray(r => (BoundExpression)r.Node),
                    node.ArgumentNamesOpt,
                    node.ArgumentRefKindsOpt,
                    node.ApplicableIndexers,
                    node.Type),
                variableValues,
                false,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitDynamicInvocation(BoundDynamicInvocation node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult expressionResult = Visit(node.Expression, variableValues);
            Debug.Assert(expressionResult.Value.Kind != CompileTimeValueKind.ArgumentArray);
            variableValues = expressionResult.UpdatedVariableValues;

            ImmutableArray<DecorationRewriteResult> argumentsResults = VisitSequentialList(node.Arguments, ref variableValues);

            return new DecorationRewriteResult(
                node.Update(
                    (BoundExpression)expressionResult.Node,
                    argumentsResults.SelectAsArray(r => (BoundExpression)r.Node),
                    node.ArgumentNamesOpt,
                    node.ArgumentRefKindsOpt,
                    node.ApplicableMethods,
                    node.Type),
                variableValues,
                true,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitDynamicMemberAccess(BoundDynamicMemberAccess node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult receiverResult = Visit(node.Receiver, variableValues);
            Debug.Assert(receiverResult.Value.Kind != CompileTimeValueKind.ArgumentArray);

            // A lone dynamic member access should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(
                node.Update((BoundExpression)receiverResult.Node, node.TypeArgumentsOpt, node.Name, node.Invoked, node.Indexed, node.Type),
                receiverResult.UpdatedVariableValues,
                false,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitDynamicObjectCreationExpression(BoundDynamicObjectCreationExpression node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            ImmutableArray<DecorationRewriteResult> argumentsResults = VisitSequentialList(node.Arguments, ref variableValues);

            DecorationRewriteResult initializerExpressionResult = Visit(node.InitializerExpressionOpt, variableValues);
            if (initializerExpressionResult != null)
            {
                variableValues = initializerExpressionResult.UpdatedVariableValues;
            }

            return new DecorationRewriteResult(
                node.Update(
                    argumentsResults.SelectAsArray(r => (BoundExpression)r.Node),
                    node.ArgumentNamesOpt,
                    node.ArgumentRefKindsOpt,
                    (BoundExpression)initializerExpressionResult?.Node,
                    node.ApplicableMethods,
                    node.Type),
                variableValues,
                true,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitDynamicObjectInitializerMember(BoundDynamicObjectInitializerMember node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            // A lone dynamic object initializer member should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(node, variableValues, false, CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitEventAccess(BoundEventAccess node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult receiverResult = Visit(node.ReceiverOpt, variableValues);
            if (receiverResult != null)
            {
                Debug.Assert(receiverResult.Value.Kind != CompileTimeValueKind.ArgumentArray);
                variableValues = receiverResult.UpdatedVariableValues;
            }

            // A lone event access should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(
                node.Update((BoundExpression)receiverResult?.Node, node.EventSymbol, node.IsUsableAsField, node.ResultKind, node.Type),
                variableValues,
                false,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitEventAssignmentOperator(BoundEventAssignmentOperator node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult receiverResult = Visit(node.ReceiverOpt, variableValues);
            if (receiverResult != null)
            {
                Debug.Assert(receiverResult.Value.Kind != CompileTimeValueKind.ArgumentArray);
                variableValues = receiverResult.UpdatedVariableValues;
            }

            DecorationRewriteResult argumentResult = Visit(node.Argument, variableValues);
            Debug.Assert(argumentResult.Value.Kind == CompileTimeValueKind.Dynamic);

            return new DecorationRewriteResult(
                node.Update(node.Event, node.IsAddition, node.IsDynamic, (BoundExpression)receiverResult?.Node, (BoundExpression)argumentResult.Node, node.Type),
                argumentResult.UpdatedVariableValues,
                true,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitExpressionStatement(BoundExpressionStatement node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult expressionResult = Visit(node.Expression, variableValues);

            if (expressionResult.MustEmit)
            {
                return new DecorationRewriteResult(
                    node.Update((BoundExpression)expressionResult.Node),
                    expressionResult.UpdatedVariableValues,
                    true,
                    ExecutionContinuation.NextStatement);
            }
            else
            {
                return MakeNoOpResult(node.Syntax, expressionResult.UpdatedVariableValues, ExecutionContinuation.NextStatement);
            }
        }

        public override DecorationRewriteResult VisitFieldAccess(BoundFieldAccess node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            if (!_flags.HasFlag(DecorationRewriterFlags.InDecoratorArgument))
            {
                BoundExpression strippedReceiver = MetaUtils.StripConversions(node.ReceiverOpt);
                if (strippedReceiver != null && (strippedReceiver.Kind == BoundKind.BaseReference || strippedReceiver.Kind == BoundKind.ThisReference))
                {
                    return VisitDecoratorArgument(node, node.FieldSymbol, variableValues);
                }
            }

            DecorationRewriteResult receiverResult = Visit(node.ReceiverOpt, variableValues);
            if (receiverResult != null)
            {
                Debug.Assert(receiverResult.Value.Kind != CompileTimeValueKind.ArgumentArray);
                variableValues = receiverResult.UpdatedVariableValues;
            }

            // Check if the field is an enum constant
            FieldSymbol fieldSymbol = node.FieldSymbol;
            TypeSymbol fieldType = fieldSymbol.Type;
            if (fieldSymbol.IsStatic && fieldSymbol.HasConstantValue && fieldType.IsEnumType())
            {
                TypeSymbol underlyingType = fieldType.EnumUnderlyingType();
                Debug.Assert(underlyingType.SpecialType != SpecialType.None);
                var value = new EnumValue(fieldType, ConstantValue.Create(fieldSymbol.ConstantValue, underlyingType.SpecialType));

                // A lone field access should never be a stand-alone statement, so we return MustEmit = false
                return new DecorationRewriteResult(
                    MakeSimpleStaticValueExpression(value, fieldType, node.Syntax),
                    variableValues,
                    false,
                    value);
            }

            // A lone field access should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(
                node.Update((BoundExpression)receiverResult?.Node, node.FieldSymbol, node.ConstantValue, node.ResultKind, node.Type),
                variableValues,
                false,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitFieldEqualsValue(BoundFieldEqualsValue node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult valueResult = Visit(node.Value, variableValues);

            // A lone field equals value expression should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(
                node.Update(node.Field, (BoundExpression)valueResult.Node),
                valueResult.UpdatedVariableValues,
                false,
                valueResult.Value);
        }

        public override DecorationRewriteResult VisitFixedLocalCollectionInitializer(BoundFixedLocalCollectionInitializer node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult expressionResult = Visit(node.Expression, variableValues);

            // A lone field local collection initializer should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(
                node.Update(node.ElementPointerType, node.ElementPointerTypeConversion, (BoundExpression)expressionResult.Node, node.Type),
                expressionResult.UpdatedVariableValues,
                false,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitFixedStatement(BoundFixedStatement node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult declarationsResult = Visit(node.Declarations, variableValues);

            DecorationRewriteResult bodyResult = Visit(node.Body, declarationsResult.UpdatedVariableValues);

            return new DecorationRewriteResult(
                node.Update(node.Locals, (BoundMultipleLocalDeclarations)declarationsResult.Node, (BoundStatement)bodyResult.Node),
                bodyResult.UpdatedVariableValues,
                declarationsResult.MustEmit || bodyResult.MustEmit,
                bodyResult.PossibleContinuations);
        }

        public override DecorationRewriteResult VisitForEachStatement(BoundForEachStatement node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            LocalSymbol iterationVariable = node.IterationVariable;
            DecorationRewriteResult expressionResult = Visit(node.Expression, variableValues);
            CompileTimeValue expressionValue = expressionResult.Value;
            Debug.Assert(expressionValue.Kind != CompileTimeValueKind.ArgumentArray);
            variableValues = expressionResult.UpdatedVariableValues;

            ImmutableDictionary<Symbol, CompileTimeValue> originalVariableValues = variableValues;

            // At first, we assume that the loop is statically controlled
            bool isDynamicLoop = false;
            int encapsulatingStatementIndex = _encapsulatingStatements.Count;
            if (expressionValue.Kind == CompileTimeValueKind.Dynamic)
            {
                isDynamicLoop = true;
                _encapsulatingStatements.Add(EncapsulatingStatementKind.Loop | EncapsulatingStatementKind.DynamicallyControlled);
            }
            else if (expressionValue.Kind == CompileTimeValueKind.Simple)
            {
                Debug.Assert(expressionValue is ConstantStaticValue && ((ConstantStaticValue)expressionValue).Value.IsNull);
                _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.Expression.Syntax.Location);
                return new DecorationRewriteResult(
                    new BoundBadStatement(node.Syntax, ImmutableArray<BoundNode>.Empty, true) { WasCompilerGenerated = true },
                    variableValues,
                    true,
                    ExecutionContinuation.NextStatement);
            }
            else
            {
                _encapsulatingStatements.Add(EncapsulatingStatementKind.Loop);
            }

            ImmutableArray<BoundStatement>.Builder statementsBuilder = ImmutableArray.CreateBuilder<BoundStatement>();
            ImmutableHashSet<ExecutionContinuation> possibleContinuations = ImmutableHashSet.Create<ExecutionContinuation>();
            bool performMoreIterations;
            int iterationIndex = 0;
            do
            {
                if (isDynamicLoop)
                {
                    DecorationRewriteResult bodyResult = Visit(node.Body, expressionResult.UpdatedVariableValues);
                    statementsBuilder.Add(node.Update(
                        node.EnumeratorInfoOpt,
                        node.ElementConversion,
                        node.IterationVariableType,
                        GetReplacementSymbol(iterationVariable),
                        (BoundExpression)expressionResult.Node,
                        (BoundStatement)bodyResult.Node,
                        node.Checked,
                        node.BreakLabel,
                        node.ContinueLabel));
                    variableValues = bodyResult.UpdatedVariableValues;

                    performMoreIterations = false;
                    possibleContinuations = bodyResult.PossibleContinuations.Remove(ExecutionContinuation.Break).Remove(ExecutionContinuation.Continue);
                    if (bodyResult.HasBreakContinuation || bodyResult.HasContinueContinuation)
                    {
                        // If the loop may be interrupted by a break or continue statement, execution may proceed with the next statement after the loop
                        possibleContinuations = possibleContinuations.Add(ExecutionContinuation.NextStatement);
                    }
                }
                else
                {
                    Debug.Assert(expressionValue is ArrayValue);
                    var arrayValue = (ArrayValue)expressionValue;
                    if (iterationIndex < arrayValue.Array.Length)
                    {
                        DecorationRewriteResult bodyResult = Visit(node.Body, variableValues.SetItem(iterationVariable, arrayValue.Array[iterationIndex]));

                        if (bodyResult.HasNextStatementContinuation || bodyResult.HasContinueContinuation)
                        {
                            performMoreIterations = true;
                            if (bodyResult.PossibleContinuations.Count(ec => ec.AffectsLoopControlFlow()) > 1)
                            {
                                // The loop body contains multiple dynamically-reachable fragments that affect its flow, and it will perform more than one iteration.
                                // Therefore, we convert it into a dynamically-controlled loop and process it again
                                isDynamicLoop = true;
                                _encapsulatingStatements[encapsulatingStatementIndex] = EncapsulatingStatementKind.Loop | EncapsulatingStatementKind.DynamicallyControlled;
                                continue;
                            }
                        }
                        else
                        {
                            // All of the body's possible continuations terminate the loop
                            performMoreIterations = false;
                        }
                        variableValues = bodyResult.UpdatedVariableValues;

                        if (bodyResult.MustEmit)
                        {
                            statementsBuilder.Add((BoundStatement)bodyResult.Node);
                        }

                        possibleContinuations = possibleContinuations.Union(bodyResult.PossibleContinuations.Remove(ExecutionContinuation.Break).Remove(ExecutionContinuation.Continue));
                        if (bodyResult.HasBreakContinuation || bodyResult.HasContinueContinuation)
                        {
                            // If the loop may be interrupted by a break or continue statement, execution may proceed with the next statement after the loop
                            possibleContinuations = possibleContinuations.Add(ExecutionContinuation.NextStatement);
                        }

                        iterationIndex++;
                    }
                    else
                    {
                        performMoreIterations = false;
                        // If we leave the loop before the first iteration is unrolled, we want to proceed with the next statement
                        possibleContinuations = possibleContinuations.Add(ExecutionContinuation.NextStatement);
                    }
                }
            }
            while (performMoreIterations);
            Debug.Assert(!possibleContinuations.IsEmpty);

            _encapsulatingStatements.RemoveAt(encapsulatingStatementIndex);
            Debug.Assert(_encapsulatingStatements.Count == encapsulatingStatementIndex);

            switch (statementsBuilder.Count)
            {
                case 0:
                    return MakeNoOpResult(node.Syntax, variableValues, possibleContinuations);

                case 1:
                    return new DecorationRewriteResult(statementsBuilder[0], variableValues, true, possibleContinuations);

                default:
                    return new DecorationRewriteResult(
                        new BoundStatementList(node.Syntax, statementsBuilder.ToImmutable()) { WasCompilerGenerated = true },
                        variableValues,
                        true,
                        possibleContinuations);
            }
        }

        public override DecorationRewriteResult VisitForStatement(BoundForStatement node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            ImmutableArray<LocalSymbol>.Builder outerBlockLocalsBuilder = _blockLocalsBuilder;
            _blockLocalsBuilder = ImmutableArray.CreateBuilder<LocalSymbol>();

            DecorationRewriteResult initializerResult = Visit(node.Initializer, variableValues);
            if (initializerResult != null)
            {
                Debug.Assert(initializerResult.HasNextStatementContinuation && !initializerResult.HasAmbiguousContinuation);
                variableValues = initializerResult.UpdatedVariableValues;
            }

            ImmutableDictionary<Symbol, CompileTimeValue> originalVariableValues = variableValues;

            // At first, we assume that the loop is statically controlled
            bool isDynamicLoop = false;
            int encapsulatingStatementIndex = _encapsulatingStatements.Count;
            _encapsulatingStatements.Add(EncapsulatingStatementKind.Loop);

            BoundStatement rewrittenNode = null;
            ImmutableArray<BoundStatement>.Builder statementsBuilder = ImmutableArray.CreateBuilder<BoundStatement>();
            ImmutableHashSet<ExecutionContinuation> possibleContinuations = ImmutableHashSet.Create<ExecutionContinuation>();
            bool performMoreIterations;
            do
            {
                if (isDynamicLoop)
                {
                    DecorationRewriteResult conditionResult = VisitWithExtraFlags(DecorationRewriterFlags.ProhibitSpliceLocation, node.Condition, originalVariableValues);

                    DecorationRewriteResult bodyResult = Visit(node.Body, conditionResult.UpdatedVariableValues);
                    variableValues = bodyResult.UpdatedVariableValues;

                    DecorationRewriteResult incrementResult = Visit(node.Increment, variableValues);
                    if (incrementResult != null)
                    {
                        Debug.Assert(incrementResult.HasNextStatementContinuation && !incrementResult.HasAmbiguousContinuation);
                        variableValues = incrementResult.UpdatedVariableValues;
                    }

                    rewrittenNode = node.Update(
                        _blockLocalsBuilder.ToImmutable(),
                        (BoundStatement)initializerResult?.Node,
                        (BoundExpression)conditionResult.Node,
                        (BoundStatement)incrementResult?.Node,
                        (BoundStatement)bodyResult.Node,
                        node.BreakLabel,
                        node.ContinueLabel);

                    performMoreIterations = false;
                    possibleContinuations = bodyResult.PossibleContinuations.Remove(ExecutionContinuation.Break).Remove(ExecutionContinuation.Continue);
                    if (bodyResult.HasBreakContinuation || bodyResult.HasContinueContinuation)
                    {
                        // If the loop may be interrupted by a break or continue statement, execution may proceed with the next statement after the loop
                        possibleContinuations = possibleContinuations.Add(ExecutionContinuation.NextStatement);
                    }
                }
                else
                {
                    // If this is the first iteration of a static loop, we want to add any residual initialization statement before unrolling the actual loop
                    if (statementsBuilder.Count == 0 && initializerResult != null && initializerResult.MustEmit)
                    {
                        statementsBuilder.Add((BoundStatement)initializerResult.Node);
                    }

                    DecorationRewriteResult conditionResult = VisitWithExtraFlags(DecorationRewriterFlags.ProhibitSpliceLocation, node.Condition, variableValues);
                    CompileTimeValue conditionValue = conditionResult.Value;
                    if (conditionValue.Kind == CompileTimeValueKind.Dynamic)
                    {
                        isDynamicLoop = true;
                        _encapsulatingStatements[encapsulatingStatementIndex] = EncapsulatingStatementKind.Loop | EncapsulatingStatementKind.DynamicallyControlled;
                        performMoreIterations = true;
                        continue;
                    }
                    variableValues = conditionResult.UpdatedVariableValues;

                    Debug.Assert(conditionValue.Kind == CompileTimeValueKind.Simple && conditionValue is ConstantStaticValue);
                    ConstantValue conditionConstantValue = ((ConstantStaticValue)conditionValue).Value;
                    Debug.Assert(conditionConstantValue.IsBoolean);
                    if (conditionConstantValue.BooleanValue)
                    {
                        DecorationRewriteResult bodyResult = Visit(node.Body, variableValues);

                        if (bodyResult.HasNextStatementContinuation || bodyResult.HasContinueContinuation)
                        {
                            performMoreIterations = true;
                            if (bodyResult.PossibleContinuations.Count(ec => ec.AffectsLoopControlFlow()) > 1)
                            {
                                // The loop body contains multiple dynamically-reachable fragments that affect its flow, and it will perform more than one iteration.
                                // Therefore, we convert it into a dynamically-controlled loop and process it again
                                isDynamicLoop = true;
                                _encapsulatingStatements[encapsulatingStatementIndex] = EncapsulatingStatementKind.Loop | EncapsulatingStatementKind.DynamicallyControlled;
                                continue;
                            }
                        }
                        else
                        {
                            // All of the body's possible continuations terminate the loop
                            performMoreIterations = false;
                        }
                        variableValues = bodyResult.UpdatedVariableValues;

                        if (bodyResult.MustEmit)
                        {
                            statementsBuilder.Add((BoundStatement)bodyResult.Node);
                        }

                        possibleContinuations = possibleContinuations.Union(bodyResult.PossibleContinuations.Remove(ExecutionContinuation.Break).Remove(ExecutionContinuation.Continue));
                        if (bodyResult.HasBreakContinuation || bodyResult.HasContinueContinuation)
                        {
                            // If the loop may be interrupted by a break or continue statement, execution may proceed with the next statement after the loop
                            possibleContinuations = possibleContinuations.Add(ExecutionContinuation.NextStatement);
                        }

                        if (performMoreIterations)
                        {
                            DecorationRewriteResult incrementResult = Visit(node.Increment, variableValues);
                            if (incrementResult != null)
                            {
                                Debug.Assert(incrementResult.HasNextStatementContinuation && !incrementResult.HasAmbiguousContinuation);
                                variableValues = incrementResult.UpdatedVariableValues;
                                if (incrementResult.MustEmit)
                                {
                                    statementsBuilder.Add((BoundStatement)incrementResult.Node);
                                }
                            }
                        }
                    }
                    else
                    {
                        performMoreIterations = false;
                        // If we leave the loop before the first iteration is unrolled, we want to proceed with the next statement
                        possibleContinuations = possibleContinuations.Add(ExecutionContinuation.NextStatement);
                    }
                }
            }
            while (performMoreIterations);
            Debug.Assert(!possibleContinuations.IsEmpty);

            _encapsulatingStatements.RemoveAt(encapsulatingStatementIndex);
            Debug.Assert(_encapsulatingStatements.Count == encapsulatingStatementIndex);

            bool mustEmit;
            if (isDynamicLoop)
            {
                mustEmit = true;
            }
            else if (statementsBuilder.Count == 0)
            {
                rewrittenNode = MakeNoOpStatement(node.Syntax);
                mustEmit = false;
            }
            else if (statementsBuilder.Count == 1 && _blockLocalsBuilder.Count == 0)
            {
                rewrittenNode = statementsBuilder[0];
                mustEmit = true;
            }
            else
            {
                rewrittenNode = new BoundBlock(node.Syntax, _blockLocalsBuilder.ToImmutable(), statementsBuilder.ToImmutable()) { WasCompilerGenerated = true };
                mustEmit = true;
            }

            _blockLocalsBuilder = outerBlockLocalsBuilder;

            return new DecorationRewriteResult(rewrittenNode, variableValues, mustEmit, possibleContinuations);
        }

        public override DecorationRewriteResult VisitGotoStatement(BoundGotoStatement node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            return new DecorationRewriteResult(node, variableValues, true, new JumpContinuation(node.Label));
        }

        public override DecorationRewriteResult VisitIfStatement(BoundIfStatement node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            int encapsulatingStatementIndex = _encapsulatingStatements.Count;
            DecorationRewriteResult conditionResult = Visit(node.Condition, variableValues);
            CompileTimeValue conditionValue = conditionResult.Value;
            if (conditionValue.Kind == CompileTimeValueKind.Simple)
            {
                Debug.Assert(conditionResult.Value is ConstantStaticValue);
                ConstantValue conditionConstantValue = ((ConstantStaticValue)conditionValue).Value;
                Debug.Assert(conditionConstantValue.IsBoolean);

                _encapsulatingStatements.Add(EncapsulatingStatementKind.Conditional);

                DecorationRewriteResult result;
                if (conditionConstantValue.BooleanValue)
                {
                    result = Visit(node.Consequence, conditionResult.UpdatedVariableValues);
                }
                else
                {
                    BoundStatement alternativeOpt = node.AlternativeOpt;
                    if (alternativeOpt == null)
                    {
                        result = MakeNoOpResult(node.Syntax, conditionResult.UpdatedVariableValues, ExecutionContinuation.NextStatement);
                    }
                    else
                    {
                        result = Visit(alternativeOpt, conditionResult.UpdatedVariableValues);
                    }
                }

                _encapsulatingStatements.RemoveAt(encapsulatingStatementIndex);
                Debug.Assert(_encapsulatingStatements.Count == encapsulatingStatementIndex);

                return result;
            }
            else
            {
                Debug.Assert(conditionResult.Value.Kind == CompileTimeValueKind.Dynamic);

                _encapsulatingStatements.Add(EncapsulatingStatementKind.Conditional | EncapsulatingStatementKind.DynamicallyControlled);

                DecorationRewriteResult consequenceResult = Visit(node.Consequence, conditionResult.UpdatedVariableValues);
                DecorationRewriteResult alternativeResult = Visit(node.AlternativeOpt, conditionResult.UpdatedVariableValues);

                ImmutableHashSet<ExecutionContinuation> possibleContinuations;
                if (alternativeResult == null)
                {
                    variableValues = UnifyVariableValues(conditionResult.UpdatedVariableValues, consequenceResult.UpdatedVariableValues);
                    possibleContinuations = consequenceResult.PossibleContinuations.Add(ExecutionContinuation.NextStatement);
                }
                else
                {
                    variableValues = UnifyVariableValues(consequenceResult.UpdatedVariableValues, alternativeResult.UpdatedVariableValues);
                    possibleContinuations = consequenceResult.PossibleContinuations.Union(alternativeResult.PossibleContinuations);
                }

                _encapsulatingStatements.RemoveAt(encapsulatingStatementIndex);
                Debug.Assert(_encapsulatingStatements.Count == encapsulatingStatementIndex);

                return new DecorationRewriteResult(
                    node.Update((BoundExpression)conditionResult.Node, (BoundStatement)consequenceResult.Node, (BoundStatement)alternativeResult?.Node),
                    variableValues,
                    true,
                    possibleContinuations);
            }
        }

        public override DecorationRewriteResult VisitImplicitReceiver(BoundImplicitReceiver node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            // A lone implicit receiver should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(node, variableValues, false, CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitIncrementOperator(BoundIncrementOperator node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            BoundExpression operand = node.Operand;
            DecorationRewriteResult operandResult = Visit(operand, variableValues);
            CompileTimeValue operandValue = operandResult.Value;

            BoundExpression rewrittenNode;
            CompileTimeValue value;
            bool mustEmit;
            if (operandValue.Kind == CompileTimeValueKind.Simple)
            {
                Debug.Assert(operandValue is ConstantStaticValue || operandValue is EnumValue);

                UnaryOperatorKind operatorKind = node.OperatorKind;
                CompileTimeValue newOperandValue = StaticValueUtils.FoldIncrementOperator(node.Syntax, operatorKind, operandValue, node.Type.SpecialType, _diagnostics);
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
                rewrittenNode = MakeSimpleStaticValueExpression(value, node.Type, node.Syntax);
                mustEmit = false;

                switch (operand.Kind)
                {
                    case BoundKind.Local:
                        LocalSymbol localSymbol = ((BoundLocal)operand).LocalSymbol;
                        Debug.Assert(_bindingTimeAnalyzer.VariableBindingTimes[localSymbol] == BindingTime.StaticSimpleValue);
                        variableValues = operandResult.UpdatedVariableValues.SetItem(localSymbol, newOperandValue);
                        break;

                    case BoundKind.Parameter:
                        ParameterSymbol parameterSymbol = ((BoundParameter)operand).ParameterSymbol;
                        Debug.Assert(!_decoratorMethod.Parameters.Contains(parameterSymbol)
                                     && _bindingTimeAnalyzer.VariableBindingTimes[parameterSymbol] == BindingTime.StaticSimpleValue);
                        variableValues = operandResult.UpdatedVariableValues.SetItem(parameterSymbol, newOperandValue);
                        break;

                    case BoundKind.ArrayAccess:
                        BoundExpression arrayExpression = ((BoundArrayAccess)operand).Expression;
                        DecorationRewriteResult arrayExpressionResult = Visit(arrayExpression, variableValues);
                        CompileTimeValue arrayExpressionValue = arrayExpressionResult.Value;
                        Debug.Assert(arrayExpressionValue.Kind == CompileTimeValueKind.Complex && arrayExpressionValue is ArrayValue);
                        variableValues = arrayExpressionResult.UpdatedVariableValues;
                        ImmutableArray<DecorationRewriteResult> indicesResults = VisitSequentialList(((BoundArrayAccess)operand).Indices, ref variableValues);
                        Debug.Assert(indicesResults.Length == 1 && indicesResults[0].Value is ConstantStaticValue);
                        ConstantValue indexConstant = ((ConstantStaticValue)indicesResults[0].Value).Value;
                        Debug.Assert(indexConstant.IsIntegral);

                        BindingTimeAnalysisResult arrayExpressionBindingTimeResult = _bindingTimeAnalyzer.Visit(arrayExpression, BindingTimeAnalyzerFlags.None);
                        if (arrayExpressionBindingTimeResult.MainSymbol != null)
                        {
                            variableValues = variableValues.SetItem(arrayExpressionBindingTimeResult.MainSymbol, newOperandValue);
                        }
                        break;

                    default:
                        throw ExceptionUtilities.Unreachable;
                }
            }
            else
            {
                Debug.Assert(operandValue.Kind == CompileTimeValueKind.Dynamic);
                rewrittenNode = node.Update(
                    node.OperatorKind,
                    (BoundExpression)operandResult.Node,
                    node.MethodOpt,
                    node.OperandConversion,
                    node.ResultConversion,
                    node.ResultKind,
                    node.Type);
                variableValues = operandResult.UpdatedVariableValues;
                value = CompileTimeValue.Dynamic;
                mustEmit = true;
            }
            return new DecorationRewriteResult(rewrittenNode, variableValues, mustEmit, value);
        }

        public override DecorationRewriteResult VisitIndexerAccess(BoundIndexerAccess node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            // TODO: Handle well-know indexer accesses with static binding time
            DecorationRewriteResult receiverResult = Visit(node.ReceiverOpt, variableValues);
            if (receiverResult != null)
            {
                variableValues = receiverResult.UpdatedVariableValues;
            }

            ImmutableArray<DecorationRewriteResult> argumentsResults = VisitSequentialList(node.Arguments, ref variableValues);

            // A lone indexer access should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(
                node.Update(
                    (BoundExpression)receiverResult?.Node,
                    node.Indexer,
                    argumentsResults.SelectAsArray(r => (BoundExpression)r.Node),
                    node.ArgumentNamesOpt,
                    node.ArgumentRefKindsOpt,
                    node.Expanded,
                    node.ArgsToParamsOpt,
                    node.Type),
                variableValues,
                false,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitInterpolatedString(BoundInterpolatedString node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            ImmutableArray<DecorationRewriteResult> partsResults = VisitSequentialList(node.Parts, ref variableValues);

            // A lone interpolated string should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(
                node.Update(partsResults.SelectAsArray(r => (BoundExpression)r.Node), node.Type),
                variableValues,
                false,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitIsOperator(BoundIsOperator node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult operandResult = Visit(node.Operand, variableValues);
            CompileTimeValue operandValue = operandResult.Value;
            TypeSymbol targetType = node.TargetType.Type;

            CompileTimeValue value;
            if (targetType.IsObjectType())
            {
                value = new ConstantStaticValue(ConstantValue.Create(true));
            }
            else
            {
                switch (operandValue.Kind)
                {
                    case CompileTimeValueKind.Simple:
                        if (operandValue is ConstantStaticValue)
                        {
                            ConstantValue operandConstantValue = ((ConstantStaticValue)operandValue).Value;
                            if (operandConstantValue.IsNull)
                            {
                                bool acceptsNull = targetType.IsReferenceType || targetType.IsNullableTypeOrTypeParameter();
                                value = new ConstantStaticValue(ConstantValue.Create(acceptsNull));
                            }
                            else
                            {
                                Debug.Assert(operandConstantValue.SpecialType != SpecialType.None);
                                TypeSymbol constantType = _compilation.GetSpecialType(operandConstantValue.SpecialType);
                                value = new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsAssignableFrom(targetType, constantType)));
                            }
                        }
                        else if (operandValue is EnumValue)
                        {
                            TypeSymbol enumType = ((EnumValue)operandValue).EnumType;
                            value = new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsAssignableFrom(targetType, enumType)));
                        }
                        else
                        {
                            Debug.Assert(operandValue is TypeValue);
                            TypeSymbol typeType = _compilation.GetWellKnownType(WellKnownType.System_Type);
                            value = new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsAssignableFrom(targetType, typeType)));
                        }
                        break;

                    case CompileTimeValueKind.Complex:
                        if (operandValue is ArrayValue)
                        {
                            TypeSymbol arrayType = ((ArrayValue)operandValue).ArrayType;
                            value = new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsAssignableFrom(targetType, arrayType)));
                        }
                        else if (operandValue is MethodInfoValue)
                        {
                            TypeSymbol methodInfoType = _compilation.GetWellKnownType(WellKnownType.System_Reflection_MethodInfo);
                            value = new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsAssignableFrom(targetType, methodInfoType)));
                        }
                        else if (operandValue is ConstructorInfoValue)
                        {
                            TypeSymbol constructorInfoType = _compilation.GetWellKnownType(WellKnownType.System_Reflection_ConstructorInfo);
                            value = new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsAssignableFrom(targetType, constructorInfoType)));
                        }
                        else if (operandValue is PropertyInfoValue)
                        {
                            TypeSymbol propertyInfoType = _compilation.GetWellKnownType(WellKnownType.System_Reflection_PropertyInfo);
                            value = new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsAssignableFrom(targetType, propertyInfoType)));
                        }
                        else if (operandValue is ParameterInfoValue)
                        {
                            TypeSymbol parameterInfoType = _compilation.GetWellKnownType(WellKnownType.System_Reflection_ParameterInfo);
                            value = new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsAssignableFrom(targetType, parameterInfoType)));
                        }
                        else
                        {
                            Debug.Assert(operandValue is AttributeValue);
                            TypeSymbol attributeType = ((AttributeValue)operandValue).Attribute.AttributeClass;
                            value = new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsAssignableFrom(targetType, attributeType)));
                        }
                        break;

                    case CompileTimeValueKind.ArgumentArray:
                        value = new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsAssignableFrom(targetType, _compilation.CreateArrayTypeSymbol(_compilation.ObjectType))));
                        break;

                    default:
                        Debug.Assert(operandValue.Kind == CompileTimeValueKind.Dynamic);
                        value = CompileTimeValue.Dynamic;
                        break;
                }
            }

            // A lone is operator should never be a stand-alone statement, so we return MustEmit = false
            if (value.Kind == CompileTimeValueKind.Dynamic)
            {
                return new DecorationRewriteResult(
                    node.Update((BoundExpression)operandResult.Node, node.TargetType, node.Conversion, node.Type),
                    operandResult.UpdatedVariableValues,
                    false,
                    value);
            }
            else
            {
                return new DecorationRewriteResult(
                    MakeSimpleStaticValueExpression(value, node.Type, node.Syntax),
                    operandResult.UpdatedVariableValues,
                    false,
                    value);
            }
        }

        public override DecorationRewriteResult VisitLabel(BoundLabel node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            return new DecorationRewriteResult(node, variableValues, true, ExecutionContinuation.NextStatement);
        }

        public override DecorationRewriteResult VisitLabelStatement(BoundLabelStatement node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            return new DecorationRewriteResult(node, variableValues, true, ExecutionContinuation.NextStatement);
        }

        public override DecorationRewriteResult VisitLabeledStatement(BoundLabeledStatement node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult bodyResult = Visit(node.Body, variableValues);

            return new DecorationRewriteResult(
                node.Update(node.Label, (BoundStatement)bodyResult.Node),
                bodyResult.UpdatedVariableValues,
                true,
                bodyResult.PossibleContinuations);
        }

        public override DecorationRewriteResult VisitLambda(BoundLambda node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult bodyResult = VisitWithExtraFlags(DecorationRewriterFlags.InNestedLambdaBody, node.Body, variableValues);

            return new DecorationRewriteResult(
                node.Update(node.Symbol, GetBlock(bodyResult.Node, node.Body.Syntax), node.Diagnostics, node.Binder, node.Type),
                bodyResult.UpdatedVariableValues,
                true,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitLiteral(BoundLiteral node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            // A lone literal should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(node, variableValues, false, new ConstantStaticValue(node.ConstantValue));
        }

        public override DecorationRewriteResult VisitLocal(BoundLocal node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            Debug.Assert(!_flags.HasFlag(DecorationRewriterFlags.InDecoratorArgument));

            LocalSymbol localSymbol = node.LocalSymbol;
            CompileTimeValue value;
            if (!variableValues.TryGetValue(localSymbol, out value))
            {
                value = CompileTimeValue.Dynamic;
            }

            BindingTime bindingTime = _bindingTimeAnalyzer.VariableBindingTimes[localSymbol];
            BoundExpression rewrittenNode = null;
            switch (value.Kind)
            {
                case CompileTimeValueKind.Simple:
                    Debug.Assert(bindingTime == BindingTime.StaticSimpleValue);
                    rewrittenNode = MetaUtils.ConvertIfNeeded(node.Type, MakeSimpleStaticValueExpression(value, node.Type, node.Syntax), _compilation);
                    break;

                case CompileTimeValueKind.ArgumentArray:
                    Debug.Assert(bindingTime == BindingTime.StaticArgumentArray && !_flags.HasFlag(DecorationRewriterFlags.ExpectedDynamicArgumentArray));
                    // If the compile-time value is an argument array, the expression part of the rewrite result should never be used in the rewritten outer expression;
                    // we therefore use a BoundBadExpression to represent it (as we have no variable corresponding to this argument array in the rewritten code)
                    rewrittenNode = MakeBadExpression(node.Syntax, node.Type);
                    break;

                case CompileTimeValueKind.Complex:
                    Debug.Assert(bindingTime == BindingTime.StaticComplexValue);
                    // If the compile-time value is a complex static value, the expression part of the rewrite result should never be used in the rewritten outer expression;
                    // we therefore use a BoundBadExpression to represent it (as we have no variable corresponding to this complex value in the rewritten code)
                    rewrittenNode = MakeBadExpression(node.Syntax, node.Type);
                    break;

                case CompileTimeValueKind.Dynamic:
                    Debug.Assert(bindingTime == BindingTime.Dynamic);
                    LocalSymbol replacementSymbol = GetReplacementSymbol(localSymbol);
                    // We need a conversion here, in case the original local variable was a result-holding variable and its type was changed from object to something else
                    rewrittenNode = MetaUtils.ConvertIfNeeded(
                        node.Type,
                        node.Update(replacementSymbol, node.ConstantValue, replacementSymbol.Type),
                        _compilation);
                    break;
            }

            // A lone variable should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(rewrittenNode, variableValues, false, value);
        }

        public override DecorationRewriteResult VisitLocalDeclaration(BoundLocalDeclaration node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            // All local variable declarations in the decorator method's body should be inside a block
            Debug.Assert(_blockLocalsBuilder != null);

            LocalSymbol localSymbol = node.LocalSymbol;
            BindingTime localBindingTime = _bindingTimeAnalyzer.VariableBindingTimes[localSymbol];

            BoundStatement rewrittenNode = null;
            CompileTimeValue value = CompileTimeValue.Dynamic;
            bool mustEmit = true;

            DecorationRewriteResult initializerResult = Visit(node.InitializerOpt, variableValues);
            if (initializerResult != null)
            {
                variableValues = initializerResult.UpdatedVariableValues;
            }

            ImmutableArray<DecorationRewriteResult> argumentsResults = VisitSequentialList(node.ArgumentsOpt, ref variableValues);

            if (initializerResult != null)
            {
                CompileTimeValue initializerValue = initializerResult.Value;
                if (localBindingTime == BindingTime.Dynamic)
                {
                    variableValues = variableValues.SetItem(localSymbol, CompileTimeValue.Dynamic);
                }
                else
                {
                    Debug.Assert(initializerValue.Kind != CompileTimeValueKind.Dynamic);
                    variableValues = variableValues.SetItem(localSymbol, initializerValue);
                    rewrittenNode = new BoundExpressionStatement(node.Syntax, (BoundExpression)initializerResult.Node) { WasCompilerGenerated = true };
                    value = initializerValue;
                    mustEmit = initializerResult.MustEmit;
                }
            }

            if (rewrittenNode == null)
            {
                LocalSymbol replacementSymbol = GetReplacementSymbol(localSymbol);
                _blockLocalsBuilder.Add(replacementSymbol);
                BoundTypeExpression rewrittenDeclaredType;
                if (replacementSymbol.Type == node.DeclaredType.Type)
                {
                    rewrittenDeclaredType = node.DeclaredType;
                }
                else
                {
                    rewrittenDeclaredType = new BoundTypeExpression(node.DeclaredType.Syntax, null, replacementSymbol.Type);
                }

                if (initializerResult == null)
                {
                    rewrittenNode = node.Update(
                        replacementSymbol,
                        rewrittenDeclaredType,
                        null,
                        argumentsResults.SelectAsArray(r => (BoundExpression)r.Node));
                }
                else
                {
                    rewrittenNode = node.Update(
                        replacementSymbol,
                        rewrittenDeclaredType,
                        MetaUtils.ConvertIfNeeded(replacementSymbol.Type, (BoundExpression)initializerResult.Node, _compilation),
                        argumentsResults.SelectAsArray(r => (BoundExpression)r.Node));
                }
            }
            return new DecorationRewriteResult(rewrittenNode, variableValues, mustEmit, ExecutionContinuation.NextStatement);
        }

        public override DecorationRewriteResult VisitLockStatement(BoundLockStatement node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult argumentResult = Visit(node.Argument, variableValues);
            Debug.Assert(argumentResult.Value.Kind == CompileTimeValueKind.Dynamic);

            DecorationRewriteResult bodyResult = Visit(node.Body, argumentResult.UpdatedVariableValues);

            if (bodyResult.MustEmit)
            {
                return new DecorationRewriteResult(
                    node.Update((BoundExpression)argumentResult.Node, (BoundStatement)bodyResult.Node),
                    bodyResult.UpdatedVariableValues,
                    true,
                    bodyResult.PossibleContinuations);
            }
            else
            {
                return MakeNoOpResult(node.Syntax, bodyResult.UpdatedVariableValues, bodyResult.PossibleContinuations);
            }
        }

        public override DecorationRewriteResult VisitLoweredConditionalAccess(BoundLoweredConditionalAccess node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult receiverResult = Visit(node.Receiver, variableValues);
            CompileTimeValue receiverValue = receiverResult.Value;
            if (receiverValue.Kind != CompileTimeValueKind.Dynamic)
            {
                if (CheckIsNullStaticValue(receiverValue))
                {
                    BoundExpression whenNullOpt = node.WhenNullOpt;
                    if (whenNullOpt != null)
                    {
                        return Visit(whenNullOpt, receiverResult.UpdatedVariableValues);
                    }
                    else
                    {
                        var nullValue = new ConstantStaticValue(ConstantValue.Null);

                        return new DecorationRewriteResult(
                            MakeSimpleStaticValueExpression(nullValue, node.Type, node.Syntax),
                            receiverResult.UpdatedVariableValues,
                            false,
                            nullValue);
                    }
                }
                else
                {
                    return Visit(node.WhenNotNull, receiverResult.UpdatedVariableValues);
                }
            }
            else
            {
                DecorationRewriteResult whenNotNullResult = Visit(node.WhenNotNull, receiverResult.UpdatedVariableValues);
                DecorationRewriteResult whenNullResult = Visit(node.WhenNullOpt, receiverResult.UpdatedVariableValues);
                if (whenNullResult == null)
                {
                    variableValues = whenNotNullResult.UpdatedVariableValues;
                }
                else
                {
                    variableValues = UnifyVariableValues(whenNotNullResult.UpdatedVariableValues, whenNullResult.UpdatedVariableValues);
                }

                return new DecorationRewriteResult(
                    node.Update(
                        (BoundExpression)receiverResult.Node,
                        node.HasValueMethodOpt,
                        (BoundExpression)whenNotNullResult.Node,
                        (BoundExpression)whenNullResult?.Node,
                        node.Id,
                        node.Type),
                    variableValues,
                    receiverResult.MustEmit || whenNotNullResult.MustEmit || (whenNullResult?.MustEmit ?? false),
                    CompileTimeValue.Dynamic);
            }
        }

        public override DecorationRewriteResult VisitMethodGroup(BoundMethodGroup node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult receiverResult = Visit(node.ReceiverOpt, variableValues);
            if (receiverResult != null)
            {
                variableValues = receiverResult.UpdatedVariableValues;
            }

            return new DecorationRewriteResult(
                node.Update(
                    node.TypeArgumentsOpt,
                    node.Name,
                    node.Methods,
                    node.LookupSymbolOpt,
                    node.LookupError,
                    node.Flags,
                    (BoundExpression)receiverResult?.Node,
                    node.ResultKind),
                variableValues,
                true,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitMultipleLocalDeclarations(BoundMultipleLocalDeclarations node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            ImmutableHashSet<ExecutionContinuation> possibleContinuations;
            ImmutableArray<DecorationRewriteResult> localDeclarationsResults = VisitAndTrimStatements(node.LocalDeclarations, ref variableValues, out possibleContinuations);

            if (localDeclarationsResults.IsEmpty)
            {
                return MakeNoOpResult(node.Syntax, variableValues, possibleContinuations);
            }
            else
            {
                return new DecorationRewriteResult(
                    node.Update(localDeclarationsResults.SelectAsArray(r => (BoundLocalDeclaration)r.Node)),
                    variableValues,
                    true,
                    possibleContinuations);
            }
        }

        public override DecorationRewriteResult VisitNameOfOperator(BoundNameOfOperator node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            ConstantValue nameConstant = node.ConstantValue;
            Debug.Assert(nameConstant != null && nameConstant.IsString);
            return new DecorationRewriteResult(
                new BoundLiteral(node.Syntax, nameConstant, node.Type) { WasCompilerGenerated = true },
                variableValues,
                false,
                new ConstantStaticValue(nameConstant));
        }

        public override DecorationRewriteResult VisitNewT(BoundNewT node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult initializerExpressionResult = Visit(node.InitializerExpressionOpt, variableValues);
            if (initializerExpressionResult != null)
            {
                variableValues = initializerExpressionResult.UpdatedVariableValues;
            }

            return new DecorationRewriteResult(
                node.Update((BoundExpression)initializerExpressionResult?.Node, node.Type),
                variableValues,
                true,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitNoOpStatement(BoundNoOpStatement node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            return new DecorationRewriteResult(node, variableValues, false, ExecutionContinuation.NextStatement);
        }

        public override DecorationRewriteResult VisitNoPiaObjectCreationExpression(BoundNoPiaObjectCreationExpression node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult initializerExpressionResult = Visit(node.InitializerExpressionOpt, variableValues);
            if (initializerExpressionResult != null)
            {
                variableValues = initializerExpressionResult.UpdatedVariableValues;
            }

            return new DecorationRewriteResult(
                node.Update(node.GuidString, (BoundExpression)initializerExpressionResult?.Node, node.Type),
                variableValues,
                true,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitNullCoalescingOperator(BoundNullCoalescingOperator node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult leftOperandResult = Visit(node.LeftOperand, variableValues);
            CompileTimeValue leftOperandValue = leftOperandResult.Value;
            if (leftOperandValue.Kind != CompileTimeValueKind.Dynamic)
            {
                if (CheckIsNullStaticValue(leftOperandValue))
                {
                    return Visit(node.RightOperand, leftOperandResult.UpdatedVariableValues);
                }
                else
                {
                    return leftOperandResult;
                }
            }
            else
            {
                DecorationRewriteResult rightOperandResult = Visit(node.RightOperand, leftOperandResult.UpdatedVariableValues);

                // A lone null coalescing operator should never be a stand-alone statement, so we return MustEmit = false
                return new DecorationRewriteResult(
                    node.Update(
                        (BoundExpression)leftOperandResult.Node,
                        (BoundExpression)rightOperandResult.Node,
                        node.LeftConversion,
                        node.Type),
                    UnifyVariableValues(leftOperandResult.UpdatedVariableValues, rightOperandResult.UpdatedVariableValues),
                    true,
                    CompileTimeValue.Dynamic);
            }
        }

        public override DecorationRewriteResult VisitObjectCreationExpression(BoundObjectCreationExpression node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            ImmutableArray<DecorationRewriteResult> argumentsResults = VisitSequentialList(node.Arguments, ref variableValues);

            DecorationRewriteResult initializerExpressionResult = Visit(node.InitializerExpressionOpt, variableValues);
            if (initializerExpressionResult != null)
            {
                Debug.Assert(initializerExpressionResult.Value.Kind != CompileTimeValueKind.ArgumentArray);
                variableValues = initializerExpressionResult.UpdatedVariableValues;
            }

            return new DecorationRewriteResult(
                node.Update(
                    node.Constructor,
                    argumentsResults.SelectAsArray(r => (BoundExpression)r.Node),
                    node.ArgumentNamesOpt,
                    node.ArgumentRefKindsOpt,
                    node.Expanded,
                    node.ArgsToParamsOpt,
                    node.ConstantValueOpt,
                    node.InitializerExpressionOpt,
                    node.Type),
                variableValues,
                true,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitObjectInitializerExpression(BoundObjectInitializerExpression node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            ImmutableArray<DecorationRewriteResult> initializersResults = VisitSequentialList(node.Initializers, ref variableValues);

            // A lone object initializer should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(
                node.Update(initializersResults.SelectAsArray(r => (BoundExpression)r.Node), node.Type),
                variableValues,
                false,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitObjectInitializerMember(BoundObjectInitializerMember node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            ImmutableArray<DecorationRewriteResult> argumentsResults = VisitSequentialList(node.Arguments, ref variableValues);

            // A lone object initializer member should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(
                node.Update(
                    node.MemberSymbol,
                    argumentsResults.SelectAsArray(r => (BoundExpression)r.Node),
                    node.ArgumentNamesOpt,
                    node.ArgumentRefKindsOpt,
                    node.Expanded,
                    node.ArgsToParamsOpt,
                    node.ResultKind,
                    node.Type),
                variableValues,
                false,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitParameter(BoundParameter node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            Debug.Assert(!_flags.HasFlag(DecorationRewriterFlags.InDecoratorArgument));
            Debug.Assert(_decoratorMethod.ParameterCount == 3);

            ParameterSymbol parameterSymbol = node.ParameterSymbol;
            CompileTimeValue value;
            if (!variableValues.TryGetValue(parameterSymbol, out value))
            {
                value = CompileTimeValue.Dynamic;
            }
            BoundExpression rewrittenNode = null;
            if (parameterSymbol == _decoratorMethod.Parameters[1])
            {
                // Replace the object parameter of the decorator with a this reference in the decorated method
                rewrittenNode = new BoundThisReference(node.Syntax, node.Type) { WasCompilerGenerated = true };
            }
            else if (_decoratorMethod.Parameters.Contains(parameterSymbol))
            {
                Debug.Assert(value.Kind != CompileTimeValueKind.Simple);
                BindingTime bindingTime = _bindingTimeAnalyzer.VariableBindingTimes[parameterSymbol];
                switch (value.Kind)
                {
                    case CompileTimeValueKind.ArgumentArray:
                        Debug.Assert(bindingTime == BindingTime.StaticArgumentArray && !_flags.HasFlag(DecorationRewriterFlags.ExpectedDynamicArgumentArray));
                        // If the compile-time value is an argument array, the expression part of the rewrite result should never be used in the rewritten outer expression;
                        // we therefore use a BoundBadExpression to represent it (as we have no variable corresponding to this argument array in the rewritten code)
                        rewrittenNode = MakeBadExpression(node.Syntax, node.Type);
                        break;

                    case CompileTimeValueKind.Complex:
                        Debug.Assert(bindingTime == BindingTime.StaticComplexValue);
                        // If the compile-time value is a complex static value, the expression part of the rewrite result should never be used in the rewritten outer expression;
                        // we therefore use a BoundBadExpression to represent it (as we have no variable corresponding to this complex value in the rewritten code)
                        rewrittenNode = MakeBadExpression(node.Syntax, node.Type);
                        break;

                    case CompileTimeValueKind.Dynamic:
                        Debug.Assert(bindingTime == BindingTime.Dynamic);
                        ExtendedTypeInfo parameterType = _decoratorMethod.DecoratorMethodVariableTypes[parameterSymbol];
                        if (parameterType.Kind == ExtendedTypeKind.MemberValue)
                        {
                            // The member value parameter gets replaced by the corresponding parameter from the target method
                            Debug.Assert(_targetMemberKind == DecoratedMemberKind.IndexerSet || _targetMemberKind == DecoratedMemberKind.PropertySet);
                            ParameterSymbol valueParameter = _targetMethod.Parameters[_argumentCount];
                            rewrittenNode = MetaUtils.ConvertIfNeeded(node.Type, new BoundParameter(node.Syntax, valueParameter), _compilation);
                        }
                        else
                        {
                            // Replace the parameter from the decorator with a local
                            rewrittenNode = new BoundLocal(node.Syntax, GetReplacementSymbol(node.ParameterSymbol), null, node.Type) { WasCompilerGenerated = true };
                        }
                        break;
                }
            }
            else
            {
                // This must be a lambda parameter - keep it intact
                rewrittenNode = node;
            }

            // A lone parameter should never be a stand-alone expression, so we return MustEmit = false
            return new DecorationRewriteResult(rewrittenNode, variableValues, false, value);
        }

        public override DecorationRewriteResult VisitParameterEqualsValue(BoundParameterEqualsValue node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult valueResult = Visit(node.Value, variableValues);

            // A lone parameter equals value expression should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(
                node.Update(node.Parameter, (BoundExpression)valueResult.Node),
                valueResult.UpdatedVariableValues,
                false,
                valueResult.Value);
        }

        public override DecorationRewriteResult VisitPropertyEqualsValue(BoundPropertyEqualsValue node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult valueResult = Visit(node.Value, variableValues);

            // A lone property equals value expression should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(
                node.Update(node.Property, (BoundExpression)valueResult.Node),
                valueResult.UpdatedVariableValues,
                false,
                valueResult.Value);
        }

        public override DecorationRewriteResult VisitQueryClause(BoundQueryClause node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult valueResult = Visit(node.Value, variableValues);

            // A lone query clause should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(
                node.Update((BoundExpression)valueResult.Node, node.DefinedSymbol, node.Binder, node.Type),
                valueResult.UpdatedVariableValues,
                false,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitRangeVariable(BoundRangeVariable node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult valueResult = Visit(node.Value, variableValues);
            return new DecorationRewriteResult(
                (BoundExpression)valueResult.Node,
                valueResult.UpdatedVariableValues,
                true,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitReturnStatement(BoundReturnStatement node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult expressionResult = Visit(node.ExpressionOpt, variableValues);

            if (expressionResult == null)
            {
                // The decorator method itself should always have a return operand expression
                Debug.Assert((_targetMemberKind != DecoratedMemberKind.IndexerGet && _targetMemberKind != DecoratedMemberKind.Method && _targetMemberKind != DecoratedMemberKind.PropertyGet)
                             || _flags.HasFlag(DecorationRewriterFlags.InNestedLambdaBody));
                return new DecorationRewriteResult(node, variableValues, true, ExecutionContinuation.Return);
            }
            else if (_flags.HasFlag(DecorationRewriterFlags.InNestedLambdaBody))
            {
                return new DecorationRewriteResult(
                    node.Update((BoundExpression)expressionResult.Node),
                    expressionResult.UpdatedVariableValues,
                    true,
                    ExecutionContinuation.Return);
            }
            else
            {
                var rewrittenExpression = (BoundExpression)expressionResult.Node;

                BoundStatement rewrittenNode = null;
                if (_targetMethod.ReturnsVoid)
                {
                    if (MetaUtils.StripConversions(rewrittenExpression).Kind == BoundKind.Literal)
                    {
                        rewrittenNode = node.Update(null);
                    }
                    else
                    {
                        // Assign expression to dummy local to preserve side effects
                        CSharpSyntaxNode syntax = node.Syntax;
                        LocalSymbol dummyLocal = _factory.SynthesizedLocal(
                            rewrittenExpression.Type,
                            syntax,
                            kind: SynthesizedLocalKind.DecoratorTempLocal,
                            name: _variableNameGenerator.GenerateFreshName("tempResult"));

                        var statements = new BoundStatement[2];
                        statements[0] = new BoundLocalDeclaration(
                            syntax,
                            dummyLocal,
                            new BoundTypeExpression(syntax, null, dummyLocal.Type) { WasCompilerGenerated = true },
                            rewrittenExpression,
                            default(ImmutableArray<BoundExpression>))
                        {
                            WasCompilerGenerated = true,
                        };
                        statements[1] = node.Update(null);

                        rewrittenNode = new BoundBlock(syntax, ImmutableArray.Create(dummyLocal), statements.ToImmutableArray()) { WasCompilerGenerated = true };
                    }
                }
                else
                {
                    rewrittenNode = node.Update(MetaUtils.ConvertIfNeeded(_targetMethod.ReturnType, rewrittenExpression, _compilation));
                }
                return new DecorationRewriteResult(rewrittenNode, expressionResult.UpdatedVariableValues, true, ExecutionContinuation.Return);
            }
        }

        public override DecorationRewriteResult VisitSequencePointExpression(BoundSequencePointExpression node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult expressionResult = Visit(node.Expression, variableValues);

            return new DecorationRewriteResult(
                node.Update((BoundExpression)expressionResult.Node, node.Type),
                expressionResult.UpdatedVariableValues,
                expressionResult.MustEmit,
                expressionResult.Value);
        }

        public override DecorationRewriteResult VisitSizeOfOperator(BoundSizeOfOperator node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            // A lone sizeof operator should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(node, variableValues, false, CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitStackAllocArrayCreation(BoundStackAllocArrayCreation node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult countResult = Visit(node.Count, variableValues);

            return new DecorationRewriteResult(
                node.Update((BoundExpression)countResult.Node, node.Type),
                countResult.UpdatedVariableValues,
                true,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitStatementList(BoundStatementList node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            ImmutableHashSet<ExecutionContinuation> possibleContinuations;
            ImmutableArray<DecorationRewriteResult> statementsResults = VisitAndTrimStatements(node.Statements, ref variableValues, out possibleContinuations);

            if (statementsResults.IsEmpty)
            {
                return MakeNoOpResult(node.Syntax, variableValues, possibleContinuations);
            }
            else
            {
                return new DecorationRewriteResult(
                    node.Update(FlattenStatementList(statementsResults)),
                    variableValues,
                    true,
                    possibleContinuations);
            }
        }

        public override DecorationRewriteResult VisitStringInsert(BoundStringInsert node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult valueResult = Visit(node.Value, variableValues);
            variableValues = valueResult.UpdatedVariableValues;

            DecorationRewriteResult alignmentResult = Visit(node.Alignment, variableValues);
            if (alignmentResult != null)
            {
                variableValues = alignmentResult.UpdatedVariableValues;
            }

            DecorationRewriteResult formatResult = Visit(node.Format, variableValues);
            if (formatResult != null)
            {
                variableValues = formatResult.UpdatedVariableValues;
            }

            // A lone string insertion should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(
                node.Update((BoundExpression)valueResult.Node, (BoundExpression)alignmentResult?.Node, (BoundExpression)formatResult?.Node, node.Type),
                variableValues,
                true,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitSwitchLabel(BoundSwitchLabel node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult expressionResult = Visit(node.ExpressionOpt, variableValues);
            if (expressionResult != null)
            {
                variableValues = expressionResult.UpdatedVariableValues;
            }

            return new DecorationRewriteResult(
                node.Update(node.Label, (BoundExpression)expressionResult?.Node),
                variableValues,
                true,
                ExecutionContinuation.NextStatement);
        }

        public override DecorationRewriteResult VisitSwitchSection(BoundSwitchSection node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            ImmutableArray<DecorationRewriteResult> boundSwitchLabelsResults = VisitSequentialList(node.BoundSwitchLabels, ref variableValues);

            ImmutableHashSet<ExecutionContinuation> possibleContinuations;
            ImmutableArray<DecorationRewriteResult> statementsResults = VisitAndTrimStatements(node.Statements, ref variableValues, out possibleContinuations);
            // Verify that control does not flow out of the switch section
            Debug.Assert(!possibleContinuations.Contains(ExecutionContinuation.NextStatement));

            return new DecorationRewriteResult(
                node.Update(boundSwitchLabelsResults.SelectAsArray(r => (BoundSwitchLabel)r.Node), statementsResults.SelectAsArray(r => (BoundStatement)r.Node)),
                variableValues,
                true,
                possibleContinuations);
        }

        public override DecorationRewriteResult VisitSwitchStatement(BoundSwitchStatement node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            ImmutableArray<LocalSymbol>.Builder outerBlockLocalsBuilder = _blockLocalsBuilder;
            _blockLocalsBuilder = ImmutableArray.CreateBuilder<LocalSymbol>();

            int encapsulatingStatementIndex = _encapsulatingStatements.Count;
            DecorationRewriteResult expressionResult = Visit(node.BoundExpression, variableValues);
            CompileTimeValue expressionValue = expressionResult.Value;
            variableValues = expressionResult.UpdatedVariableValues;
            BoundStatement rewrittenNode;
            bool mustEmit;
            ImmutableHashSet<ExecutionContinuation> possibleContinuations;
            if (expressionValue.Kind == CompileTimeValueKind.Simple)
            {
                Debug.Assert(expressionValue is ConstantStaticValue || expressionValue is EnumValue);

                BoundSwitchSection matchingSection = null;
                foreach (BoundSwitchSection switchSection in node.SwitchSections)
                {
                    bool hasMatchingLabel = false;
                    foreach (BoundSwitchLabel switchLabel in switchSection.BoundSwitchLabels)
                    {
                        DecorationRewriteResult labelExpressionResult = Visit(switchLabel.ExpressionOpt, variableValues);
                        if (labelExpressionResult != null)
                        {
                            CompileTimeValue labelExpressionValue = labelExpressionResult.Value;
                            Debug.Assert((labelExpressionValue is ConstantStaticValue || labelExpressionValue is EnumValue)
                                         && labelExpressionResult.UpdatedVariableValues == variableValues);
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

                if (matchingSection == null)
                {
                    rewrittenNode = MakeNoOpStatement(node.Syntax);
                    mustEmit = false;
                    possibleContinuations = ImmutableHashSet.Create(ExecutionContinuation.NextStatement);
                }
                else
                {
                    _encapsulatingStatements.Add(EncapsulatingStatementKind.Switch);

                    LabelSymbol oldStaticSwitchEndLabel = _staticSwitchEndLabel;
                    // Generate a label for the end of the residual code of the static switch statement
                    _staticSwitchEndLabel = _factory.GenerateLabel("static_switch_end");

                    ImmutableArray<DecorationRewriteResult> results = VisitAndTrimStatements(matchingSection.Statements, ref variableValues, out possibleContinuations);
                    if (results.IsEmpty)
                    {
                        rewrittenNode = MakeNoOpStatement(node.Syntax);
                        mustEmit = false;
                    }
                    else
                    {
                        // We append the end label after the entire residual code of the static switch statement
                        rewrittenNode = new BoundBlock(
                            node.Syntax,
                            _blockLocalsBuilder.ToImmutable(),
                            results.SelectAsArray(r => (BoundStatement)r.Node).Add(new BoundLabelStatement(node.Syntax, _staticSwitchEndLabel) { WasCompilerGenerated = true }))
                        {
                            WasCompilerGenerated = true,
                        };
                        mustEmit = true;
                    }

                    _staticSwitchEndLabel = oldStaticSwitchEndLabel;

                    _encapsulatingStatements.RemoveAt(encapsulatingStatementIndex);
                    Debug.Assert(_encapsulatingStatements.Count == encapsulatingStatementIndex);
                }
            }
            else
            {
                Debug.Assert(expressionValue.Kind == CompileTimeValueKind.Dynamic);

                LabelSymbol oldStaticSwitchEndLabel = _staticSwitchEndLabel;
                _staticSwitchEndLabel = null;

                _encapsulatingStatements.Add(EncapsulatingStatementKind.Switch | EncapsulatingStatementKind.DynamicallyControlled);

                ImmutableArray<DecorationRewriteResult> switchSectionsResults = VisitIndependentList(node.SwitchSections, ref variableValues);
                possibleContinuations = ImmutableHashSet.Create<ExecutionContinuation>();
                if (!node.SwitchSections.Any(ss => ss.BoundSwitchLabels.Any(sl => sl.ExpressionOpt == null)))
                {
                    // If there is no default section, we assume that control can flow to the statement following the switch statement
                    possibleContinuations = possibleContinuations.Add(ExecutionContinuation.NextStatement);
                }
                foreach (DecorationRewriteResult switchSectionResult in switchSectionsResults)
                {
                    possibleContinuations = possibleContinuations.Union(switchSectionResult.PossibleContinuations);
                }

                rewrittenNode = node.Update(
                    (BoundExpression)expressionResult.Node,
                    node.ConstantTargetOpt,
                    _blockLocalsBuilder.ToImmutable(),
                    switchSectionsResults.SelectAsArray(r => (BoundSwitchSection)r.Node),
                    node.BreakLabel,
                    node.StringEquality);
                mustEmit = true;

                _staticSwitchEndLabel = oldStaticSwitchEndLabel;

                _encapsulatingStatements.RemoveAt(encapsulatingStatementIndex);
                Debug.Assert(_encapsulatingStatements.Count == encapsulatingStatementIndex);
            }

            _blockLocalsBuilder = outerBlockLocalsBuilder;

            return new DecorationRewriteResult(rewrittenNode, variableValues, mustEmit, possibleContinuations);
        }

        public override DecorationRewriteResult VisitThisReference(BoundThisReference node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            if (_flags.HasFlag(DecorationRewriterFlags.InDecoratorArgument))
            {
                return new DecorationRewriteResult(node, variableValues, false, CompileTimeValue.Dynamic);
            }
            else
            {
                throw ExceptionUtilities.Unreachable;
            }
        }

        public override DecorationRewriteResult VisitThrowStatement(BoundThrowStatement node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            // TODO: Handle compile-time errors and their reporting
            DecorationRewriteResult expressionResult = Visit(node.ExpressionOpt, variableValues);
            if (expressionResult != null)
            {
                variableValues = expressionResult.UpdatedVariableValues;
            }

            return new DecorationRewriteResult(
                node.Update((BoundExpression)expressionResult?.Node),
                variableValues,
                true,
                ExecutionContinuation.Throw);
        }

        public override DecorationRewriteResult VisitTryStatement(BoundTryStatement node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            int encapsulatingStatementIndex = _encapsulatingStatements.Count;
            _encapsulatingStatements.Add(EncapsulatingStatementKind.TryBlock | EncapsulatingStatementKind.DynamicallyControlled);

            DecorationRewriteResult tryBlockResult = Visit(node.TryBlock, variableValues);
            variableValues = tryBlockResult.UpdatedVariableValues;
            ImmutableHashSet<ExecutionContinuation> possibleContinuations = tryBlockResult.PossibleContinuations;

            _encapsulatingStatements.RemoveAt(encapsulatingStatementIndex);
            Debug.Assert(_encapsulatingStatements.Count == encapsulatingStatementIndex);

            ImmutableArray<DecorationRewriteResult> catchBlocksResults = VisitIndependentList(node.CatchBlocks, ref variableValues);
            // We assume that a catch block might be entered at any point of the try block's execution, and thus any continuation possibilities of the try block are
            // unified with the continuation possibilities of each catch block
            foreach (DecorationRewriteResult catchBlockResult in catchBlocksResults)
            {
                possibleContinuations = possibleContinuations.Union(catchBlockResult.PossibleContinuations);
            }

            _encapsulatingStatements.Add(EncapsulatingStatementKind.FinallyBlock);

            DecorationRewriteResult finallyBlockResult = Visit(node.FinallyBlockOpt, variableValues);
            if (finallyBlockResult != null)
            {
                Debug.Assert(finallyBlockResult.HasNextStatementContinuation
                    && !finallyBlockResult.PossibleContinuations.Any(
                        ec => ec.Kind == ExecutionContinuationKind.Break || ec.Kind == ExecutionContinuationKind.Continue
                              || ec.Kind == ExecutionContinuationKind.Return || ec.Kind == ExecutionContinuationKind.Jump));
                variableValues = finallyBlockResult.UpdatedVariableValues;
                if (finallyBlockResult.HasNextStatementContinuation)
                {
                    possibleContinuations = possibleContinuations.Union(finallyBlockResult.PossibleContinuations.Remove(ExecutionContinuation.NextStatement));
                }
                else
                {
                    // The finally block always throws an exception, which means that we do not need to respect any continuation possibilities from the try and catch blocks
                    possibleContinuations = finallyBlockResult.PossibleContinuations;
                }
            }

            _encapsulatingStatements.RemoveAt(encapsulatingStatementIndex);
            Debug.Assert(_encapsulatingStatements.Count == encapsulatingStatementIndex);

            BoundStatement rewrittenNode;
            bool mustEmit;
            if (tryBlockResult.MustEmit)
            {
                if (finallyBlockResult == null || !finallyBlockResult.MustEmit)
                {
                    if (catchBlocksResults.IsEmpty)
                    {
                        rewrittenNode = (BoundStatement)tryBlockResult.Node;
                    }
                    else
                    {
                        rewrittenNode = node.Update(
                            GetBlock(tryBlockResult.Node, node.TryBlock.Syntax),
                            catchBlocksResults.SelectAsArray(r => (BoundCatchBlock)r.Node),
                            null,
                            node.PreferFaultHandler);
                    }
                }
                else
                {
                    rewrittenNode = node.Update(
                        GetBlock(tryBlockResult.Node, node.TryBlock.Syntax),
                        catchBlocksResults.SelectAsArray(r => (BoundCatchBlock)r.Node),
                        GetBlock(finallyBlockResult.Node, node.FinallyBlockOpt.Syntax),
                        node.PreferFaultHandler);
                }
                mustEmit = true;
            }
            else if (finallyBlockResult == null || !finallyBlockResult.MustEmit)
            {
                rewrittenNode = MakeNoOpStatement(node.Syntax);
                mustEmit = false;
            }
            else
            {
                rewrittenNode = (BoundStatement)finallyBlockResult.Node;
                mustEmit = true;
            }

            return new DecorationRewriteResult(rewrittenNode, variableValues, mustEmit, possibleContinuations);
        }

        public override DecorationRewriteResult VisitTypeExpression(BoundTypeExpression node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            // A lone type expression should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(node, variableValues, false, new TypeValue(node.Type));
        }

        public override DecorationRewriteResult VisitTypeOfOperator(BoundTypeOfOperator node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            // A lone typeof operator should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(node, variableValues, false, new TypeValue(node.SourceType.Type));
        }

        public override DecorationRewriteResult VisitTypeOrValueExpression(BoundTypeOrValueExpression node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult typeExpressionResult = Visit(node.Data.TypeExpression, variableValues);

            DecorationRewriteResult valueExpressionResult = Visit(node.Data.ValueExpression, variableValues);

            // A lone type or value expression should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(
                node.Update(
                    new BoundTypeOrValueData(
                        node.Data.ValueSymbol,
                        (BoundExpression)valueExpressionResult.Node,
                        node.Data.ValueDiagnostics,
                        (BoundExpression)typeExpressionResult.Node,
                        node.Data.TypeDiagnostics),
                    node.Type),
                UnifyVariableValues(typeExpressionResult.UpdatedVariableValues, valueExpressionResult.UpdatedVariableValues),
                false,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitUnaryOperator(BoundUnaryOperator node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult operandResult = Visit(node.Operand, variableValues);

            BoundExpression rewrittenNode = null;
            CompileTimeValue value = null;
            if (operandResult.Value.Kind == CompileTimeValueKind.Simple)
            {
                value = StaticValueUtils.FoldUnaryOperator(node.Syntax, node.OperatorKind, operandResult.Value, node.Type.SpecialType, _compilation, _diagnostics);
                rewrittenNode = MakeSimpleStaticValueExpression(value, node.Type, node.Syntax);
            }
            else
            {
                Debug.Assert(operandResult.Value.Kind == CompileTimeValueKind.Dynamic);

                rewrittenNode = node.Update(
                    node.OperatorKind,
                    (BoundExpression)operandResult.Node,
                    node.ConstantValueOpt,
                    node.MethodOpt,
                    node.ResultKind,
                    node.Type);
                value = CompileTimeValue.Dynamic;
            }

            // A lone unary operator should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(rewrittenNode, variableValues, false, value);
        }

        public override DecorationRewriteResult VisitUserDefinedConditionalLogicalOperator(
            BoundUserDefinedConditionalLogicalOperator node,
            ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult leftResult = Visit(node.Left, variableValues);
            DecorationRewriteResult rightResult = Visit(node.Right, variableValues);

            // A lone user-defined conditional operator should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(
                node.Update(
                    node.OperatorKind,
                    (BoundExpression)leftResult.Node,
                    (BoundExpression)rightResult.Node,
                    node.LogicalOperator,
                    node.TrueOperator,
                    node.FalseOperator,
                    node.ResultKind,
                    node.Type),
                UnifyVariableValues(leftResult.UpdatedVariableValues, rightResult.UpdatedVariableValues),
                false,
                CompileTimeValue.Dynamic);
        }

        public override DecorationRewriteResult VisitUsingStatement(BoundUsingStatement node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            ImmutableArray<LocalSymbol>.Builder outerBlockLocalsBuilder = _blockLocalsBuilder;
            _blockLocalsBuilder = ImmutableArray.CreateBuilder<LocalSymbol>();

            DecorationRewriteResult declarationsResult = VisitWithExtraFlags(DecorationRewriterFlags.ProhibitSpliceLocation, node.DeclarationsOpt, variableValues);
            if (declarationsResult != null)
            {
                Debug.Assert(declarationsResult.Node is BoundMultipleLocalDeclarations);
                variableValues = declarationsResult.UpdatedVariableValues;
            }

            DecorationRewriteResult expressionResult = VisitWithExtraFlags(DecorationRewriterFlags.ProhibitSpliceLocation, node.ExpressionOpt, variableValues);
            if (expressionResult != null)
            {
                variableValues = expressionResult.UpdatedVariableValues;
            }

            DecorationRewriteResult bodyResult = Visit(node.Body, variableValues);

            ImmutableArray<LocalSymbol> locals = _blockLocalsBuilder.ToImmutable();
            _blockLocalsBuilder = outerBlockLocalsBuilder;

            // Even if the body is empty, we want to preserve the using statement, as it will execute the Dispose method of the argument
            return new DecorationRewriteResult(
                node.Update(
                    locals,
                    (BoundMultipleLocalDeclarations)declarationsResult?.Node,
                    (BoundExpression)expressionResult?.Node,
                    node.IDisposableConversion,
                    (BoundStatement)bodyResult.Node),
                bodyResult.UpdatedVariableValues,
                true,
                bodyResult.PossibleContinuations);
        }

        public override DecorationRewriteResult VisitWhileStatement(BoundWhileStatement node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            ImmutableDictionary<Symbol, CompileTimeValue> originalVariableValues = variableValues;

            // At first, we assume that the loop is statically controlled
            bool isDynamicLoop = false;
            int encapsulatingStatementIndex = _encapsulatingStatements.Count;
            _encapsulatingStatements.Add(EncapsulatingStatementKind.Loop);

            ImmutableArray<BoundStatement>.Builder statementsBuilder = ImmutableArray.CreateBuilder<BoundStatement>();
            ImmutableHashSet<ExecutionContinuation> possibleContinuations = ImmutableHashSet.Create<ExecutionContinuation>();
            bool performMoreIterations;
            do
            {
                if (isDynamicLoop)
                {
                    DecorationRewriteResult conditionResult = VisitWithExtraFlags(DecorationRewriterFlags.ProhibitSpliceLocation, node.Condition, originalVariableValues);
                    DecorationRewriteResult bodyResult = Visit(node.Body, conditionResult.UpdatedVariableValues);
                    statementsBuilder.Add(node.Update((BoundExpression)conditionResult.Node, (BoundStatement)bodyResult.Node, node.BreakLabel, node.ContinueLabel));
                    variableValues = bodyResult.UpdatedVariableValues;

                    performMoreIterations = false;
                    possibleContinuations = bodyResult.PossibleContinuations.Remove(ExecutionContinuation.Break).Remove(ExecutionContinuation.Continue);
                    if (bodyResult.HasBreakContinuation || bodyResult.HasContinueContinuation)
                    {
                        // If the loop may be interrupted by a break or continue statement, execution may proceed with the next statement after the loop
                        possibleContinuations = possibleContinuations.Add(ExecutionContinuation.NextStatement);
                    }
                }
                else
                {
                    DecorationRewriteResult conditionResult = VisitWithExtraFlags(DecorationRewriterFlags.ProhibitSpliceLocation, node.Condition, variableValues);
                    CompileTimeValue conditionValue = conditionResult.Value;
                    if (conditionValue.Kind == CompileTimeValueKind.Dynamic)
                    {
                        isDynamicLoop = true;
                        _encapsulatingStatements[encapsulatingStatementIndex] = EncapsulatingStatementKind.Loop | EncapsulatingStatementKind.DynamicallyControlled;
                        performMoreIterations = true;
                        continue;
                    }
                    variableValues = conditionResult.UpdatedVariableValues;

                    Debug.Assert(conditionValue.Kind == CompileTimeValueKind.Simple && conditionValue is ConstantStaticValue);
                    ConstantValue conditionConstantValue = ((ConstantStaticValue)conditionValue).Value;
                    Debug.Assert(conditionConstantValue.IsBoolean);
                    if (conditionConstantValue.BooleanValue)
                    {
                        DecorationRewriteResult bodyResult = Visit(node.Body, variableValues);

                        if (bodyResult.HasNextStatementContinuation || bodyResult.HasContinueContinuation)
                        {
                            performMoreIterations = true;
                            if (bodyResult.PossibleContinuations.Count(ec => ec.AffectsLoopControlFlow()) > 1)
                            {
                                // The loop body contains multiple dynamically-reachable fragments that affect its flow, and it will perform more than one iteration.
                                // Therefore, we convert it into a dynamically-controlled loop and process it again
                                isDynamicLoop = true;
                                _encapsulatingStatements[encapsulatingStatementIndex] = EncapsulatingStatementKind.Loop | EncapsulatingStatementKind.DynamicallyControlled;
                                continue;
                            }
                        }
                        else
                        {
                            // All of the body's possible continuations terminate the loop
                            performMoreIterations = false;
                        }
                        variableValues = bodyResult.UpdatedVariableValues;

                        if (bodyResult.MustEmit)
                        {
                            statementsBuilder.Add((BoundStatement)bodyResult.Node);
                        }

                        possibleContinuations = possibleContinuations.Union(bodyResult.PossibleContinuations.Remove(ExecutionContinuation.Break).Remove(ExecutionContinuation.Continue));
                        if (bodyResult.HasBreakContinuation || bodyResult.HasContinueContinuation)
                        {
                            // If the loop may be interrupted by a break or continue statement, execution may proceed with the next statement after the loop
                            possibleContinuations = possibleContinuations.Add(ExecutionContinuation.NextStatement);
                        }
                    }
                    else
                    {
                        performMoreIterations = false;
                        // If we leave the loop before the first iteration is unrolled, we want to proceed with the next statement
                        possibleContinuations = possibleContinuations.Add(ExecutionContinuation.NextStatement);
                    }
                }
            }
            while (performMoreIterations);
            Debug.Assert(!possibleContinuations.IsEmpty);

            _encapsulatingStatements.RemoveAt(encapsulatingStatementIndex);
            Debug.Assert(_encapsulatingStatements.Count == encapsulatingStatementIndex);

            switch (statementsBuilder.Count)
            {
                case 0:
                    return MakeNoOpResult(node.Syntax, variableValues, possibleContinuations);

                case 1:
                    return new DecorationRewriteResult(statementsBuilder[0], variableValues, true, possibleContinuations);

                default:
                    return new DecorationRewriteResult(
                        new BoundStatementList(node.Syntax, statementsBuilder.ToImmutable()) { WasCompilerGenerated = true },
                        variableValues,
                        true,
                        possibleContinuations);
            }
        }

        private static DecoratedMemberKind GetTargetMemberKind(MethodSymbol targetMethod)
        {
            PropertySymbol associatedProperty;
            CSharpSyntaxNode associatedPropertySyntax;
            switch (targetMethod.MethodKind)
            {
                case MethodKind.Constructor:
                case MethodKind.StaticConstructor:
                    return DecoratedMemberKind.Constructor;

                case MethodKind.Destructor:
                    return DecoratedMemberKind.Destructor;

                case MethodKind.PropertyGet:
                    Debug.Assert(targetMethod is SourcePropertyAccessorSymbol && targetMethod.AssociatedSymbol is PropertySymbol);
                    associatedProperty = (PropertySymbol)targetMethod.AssociatedSymbol;
                    associatedPropertySyntax = associatedProperty.GetNonNullSyntaxNode();
                    Debug.Assert(associatedPropertySyntax != null);
                    if (associatedPropertySyntax is PropertyDeclarationSyntax)
                    {
                        return DecoratedMemberKind.PropertyGet;
                    }
                    else
                    {
                        Debug.Assert(associatedPropertySyntax is IndexerDeclarationSyntax);
                        return DecoratedMemberKind.IndexerGet;
                    }

                case MethodKind.PropertySet:
                    Debug.Assert(targetMethod is SourcePropertyAccessorSymbol && targetMethod.AssociatedSymbol is PropertySymbol);
                    associatedProperty = (PropertySymbol)targetMethod.AssociatedSymbol;
                    associatedPropertySyntax = associatedProperty.GetNonNullSyntaxNode();
                    Debug.Assert(associatedPropertySyntax != null);
                    if (associatedPropertySyntax is PropertyDeclarationSyntax)
                    {
                        return DecoratedMemberKind.PropertySet;
                    }
                    else
                    {
                        Debug.Assert(associatedPropertySyntax is IndexerDeclarationSyntax);
                        return DecoratedMemberKind.IndexerSet;
                    }

                default:
                    return DecoratedMemberKind.Method;
            }
        }

        private static SourceMemberMethodSymbol GetDecoratorMethod(
            CSharpCompilation compilation,
            MethodSymbol targetMethod,
            DecoratedMemberKind targetMemberKind,
            DecoratorData decoratorData,
            TypeCompilationState compilationState,
            DiagnosticBag diagnostics)
        {
            var decoratorClass = decoratorData.DecoratorClass as SourceNamedTypeSymbol;
            if (decoratorClass == null)
            {
                diagnostics.Add(ErrorCode.ERR_NonSourceDecoratorClass, decoratorData.ApplicationSyntaxReference.GetLocation(), decoratorData.DecoratorClass);
                return null;
            }

            SourceMemberMethodSymbol decoratorMethod = decoratorClass.FindDecoratorMethod(targetMemberKind);
            while (decoratorMethod == null)
            {
                decoratorClass = decoratorClass.BaseType as SourceNamedTypeSymbol;
                if (decoratorClass == null)
                {
                    switch (targetMemberKind)
                    {
                        case DecoratedMemberKind.Constructor:
                            diagnostics.Add(ErrorCode.ERR_DecoratorDoesNotSupportConstructors, decoratorData.ApplicationSyntaxReference.GetLocation(), decoratorData.DecoratorClass, targetMethod);
                            break;

                        case DecoratedMemberKind.Destructor:
                            diagnostics.Add(ErrorCode.ERR_DecoratorDoesNotSupportDestructors, decoratorData.ApplicationSyntaxReference.GetLocation(), decoratorData.DecoratorClass, targetMethod);
                            break;

                        case DecoratedMemberKind.IndexerGet:
                        case DecoratedMemberKind.IndexerSet:
                            diagnostics.Add(
                                ErrorCode.ERR_DecoratorDoesNotSupportIndexers,
                                decoratorData.ApplicationSyntaxReference.GetLocation(),
                                decoratorData.DecoratorClass,
                                targetMethod.AssociatedSymbol);
                            break;

                        case DecoratedMemberKind.Method:
                            diagnostics.Add(ErrorCode.ERR_DecoratorDoesNotSupportMethods, decoratorData.ApplicationSyntaxReference.GetLocation(), decoratorData.DecoratorClass, targetMethod);
                            break;

                        case DecoratedMemberKind.PropertyGet:
                        case DecoratedMemberKind.PropertySet:
                            diagnostics.Add(
                                ErrorCode.ERR_DecoratorDoesNotSupportProperties,
                                decoratorData.ApplicationSyntaxReference.GetLocation(),
                                decoratorData.DecoratorClass,
                                targetMethod.AssociatedSymbol);
                            break;

                        default:
                            throw ExceptionUtilities.Unreachable;
                    }
                    return null;
                }
                decoratorMethod = decoratorClass.FindDecoratorMethod(targetMemberKind);
            }
            return decoratorMethod;
        }

        private static bool CheckIsSpecificParameter(BoundExpression node, ParameterSymbol parameter)
        {
            while (node.Kind == BoundKind.Conversion)
            {
                node = ((BoundConversion)node).Operand;
            }
            return node.Kind == BoundKind.Parameter && ((BoundParameter)node).ParameterSymbol == parameter;
        }

        private static bool CheckIsNullStaticValue(CompileTimeValue value)
        {
            return value.Kind == CompileTimeValueKind.Simple
                   && value is ConstantStaticValue
                   && ((ConstantStaticValue)value).Value.IsNull;
        }

        private static ImmutableArray<BoundStatement> FlattenStatementList(ImmutableArray<DecorationRewriteResult> statementsResults)
        {
            ImmutableArray<BoundStatement>.Builder statementsBuilder = ImmutableArray.CreateBuilder<BoundStatement>();
            foreach (DecorationRewriteResult statementResult in statementsResults)
            {
                AddStatements((BoundStatement)statementResult.Node, statementsBuilder);
            }
            return statementsBuilder.ToImmutable();
        }

        private static void AddStatements(BoundStatement statement, ImmutableArray<BoundStatement>.Builder statementsBuilder)
        {
            if (statement.Kind == BoundKind.StatementList)
            {
                foreach (BoundStatement childStatement in ((BoundStatementList)statement).Statements)
                {
                    AddStatements(childStatement, statementsBuilder);
                }
            }
            else
            {
                statementsBuilder.Add(statement);
            }
        }

        private static ImmutableDictionary<Symbol, BoundExpression> BuildDecoratorArguments(DecoratorData decoratorData)
        {
            ImmutableDictionary<Symbol, BoundExpression>.Builder decoratorArgumentsBuilder = ImmutableDictionary.CreateBuilder<Symbol, BoundExpression>();

            // Process decorator constructor and its arguments
            Debug.Assert(decoratorData.DecoratorConstructor != null);
            if (decoratorData.DecoratorConstructor is SourceConstructorSymbol)
            {
                var constructor = (SourceConstructorSymbol)decoratorData.DecoratorConstructor;
                if (constructor.SimpleConstructorAssignments != null)
                {
                    foreach (KeyValuePair<Symbol, SimpleConstructorAssignmentOperand> kv in constructor.SimpleConstructorAssignments)
                    {
                        BoundExpression argument = kv.Value.Expression;
                        if (argument == null)
                        {
                            ParameterSymbol parameter = kv.Value.Parameter;
                            Debug.Assert(parameter != null);
                            int parameterIndex = constructor.Parameters.IndexOf(parameter);
                            Debug.Assert(parameterIndex >= 0 && parameterIndex < decoratorData.ConstructorArguments.Length);
                            argument = decoratorData.ConstructorArguments[parameterIndex];
                        }
                        decoratorArgumentsBuilder[kv.Key] = argument;
                    }
                }
            }

            // Process named arguments
            if (!decoratorData.NamedArguments.IsEmpty)
            {
                NamedTypeSymbol decoratorClass = decoratorData.DecoratorClass;
                foreach (KeyValuePair<string, BoundExpression> kv in decoratorData.NamedArguments)
                {
                    ImmutableArray<Symbol> matchingMembers = decoratorClass.GetMembers(kv.Key);
                    Debug.Assert(matchingMembers.Length == 1);
                    Symbol matchingMember = matchingMembers[0];
                    Debug.Assert(matchingMember.Kind == SymbolKind.Field || matchingMember.Kind == SymbolKind.Property);
                    decoratorArgumentsBuilder[matchingMember] = kv.Value;
                }
            }

            return decoratorArgumentsBuilder.ToImmutable();
        }

        private BoundBlock Rewrite(BoundBlock decoratorBody)
        {
            _encapsulatingStatements.Clear();
            _flags = DecorationRewriterFlags.None;
            _replacementSymbols = ImmutableDictionary<Symbol, LocalSymbol>.Empty;
            _splicedStatementsBuilder = null;
            _blockLocalsBuilder = null;
            _spliceOrdinal = 0;
            CSharpSyntaxNode methodSyntax = _factory.Syntax;

            // Prepare the initial variable values dictionary
            ImmutableDictionary<Symbol, CompileTimeValue>.Builder variableValuesBuilder = ImmutableDictionary.CreateBuilder<Symbol, CompileTimeValue>();
            ParameterSymbol memberParameter = _decoratorMethod.Parameters[0];
            CompileTimeValue memberParameterValue;
            PropertySymbol associatedProperty = null;
            if (_bindingTimeAnalyzer.VariableBindingTimes[memberParameter] == BindingTime.StaticComplexValue)
            {
                switch (_targetMemberKind)
                {
                    case DecoratedMemberKind.Constructor:
                        memberParameterValue = new ConstructorInfoValue(_targetMethod);
                        break;

                    case DecoratedMemberKind.Destructor:
                    case DecoratedMemberKind.Method:
                        memberParameterValue = new MethodInfoValue(_targetMethod);
                        break;

                    case DecoratedMemberKind.IndexerGet:
                    case DecoratedMemberKind.IndexerSet:
                    case DecoratedMemberKind.PropertyGet:
                    case DecoratedMemberKind.PropertySet:
                        Debug.Assert(_targetMethod is SourcePropertyAccessorSymbol);
                        associatedProperty = (PropertySymbol)_targetMethod.AssociatedSymbol;
                        memberParameterValue = new PropertyInfoValue(associatedProperty);
                        break;

                    default:
                        throw ExceptionUtilities.Unreachable;
                }
            }
            else
            {
                Debug.Assert(_bindingTimeAnalyzer.VariableBindingTimes[memberParameter] == BindingTime.Dynamic);
                memberParameterValue = CompileTimeValue.Dynamic;
            }
            variableValuesBuilder.Add(memberParameter, memberParameterValue);

            variableValuesBuilder.Add(_decoratorMethod.Parameters[1], CompileTimeValue.Dynamic);

            if (_targetMemberKind == DecoratedMemberKind.IndexerSet || _targetMemberKind == DecoratedMemberKind.PropertySet)
            {
                variableValuesBuilder.Add(_decoratorMethod.Parameters[2], CompileTimeValue.Dynamic);
            }

            ParameterSymbol argumentsParameter = null;
            if (_targetMemberKind != DecoratedMemberKind.Destructor && _targetMemberKind != DecoratedMemberKind.PropertyGet && _targetMemberKind != DecoratedMemberKind.PropertySet)
            {
                argumentsParameter = _decoratorMethod.Parameters[_decoratorMethod.ParameterCount - 1];
                if (_bindingTimeAnalyzer.VariableBindingTimes[argumentsParameter] == BindingTime.StaticArgumentArray)
                {
                    variableValuesBuilder.Add(argumentsParameter, new ArgumentArrayValue(_targetMethod.Parameters.AsImmutable<Symbol>()));
                }
                else
                {
                    Debug.Assert(_bindingTimeAnalyzer.VariableBindingTimes[argumentsParameter] == BindingTime.Dynamic);
                    variableValuesBuilder.Add(argumentsParameter, CompileTimeValue.Dynamic);
                }
            }

            try
            {
                // Decorate target method body
                DecorationRewriteResult bodyRewriteResult = Visit(decoratorBody, variableValuesBuilder.ToImmutable());
                var decoratedBody = GetBlock(bodyRewriteResult.Node, decoratorBody.Syntax);

                // Generate the decoration prologue and epilogue
                ImmutableArray<BoundStatement>.Builder prologueStatements = ImmutableArray.CreateBuilder<BoundStatement>();
                ImmutableArray<BoundStatement>.Builder epilogueStatements = ImmutableArray.CreateBuilder<BoundStatement>();
                ImmutableArray<LocalSymbol>.Builder prologueLocals = ImmutableArray.CreateBuilder<LocalSymbol>();
                if (_replacementSymbols.ContainsKey(memberParameter))
                {
                    Debug.Assert(_bindingTimeAnalyzer.VariableBindingTimes[memberParameter] == BindingTime.Dynamic);

                    // Assign the target method's info to the corresponding decorator method parameter's replacement local variable in the prologue
                    LocalSymbol methodLocal = GetReplacementSymbol(memberParameter);
                    prologueLocals.Add(methodLocal);

                    BoundExpression memberInfoExpression;
                    switch (_targetMemberKind)
                    {
                        case DecoratedMemberKind.Constructor:
                            memberInfoExpression = _factory.ConstructorInfo(_targetMethod);
                            break;

                        case DecoratedMemberKind.Destructor:
                        case DecoratedMemberKind.Method:
                            memberInfoExpression = _factory.MethodInfo(_targetMethod);
                            break;

                        case DecoratedMemberKind.IndexerGet:
                        case DecoratedMemberKind.IndexerSet:
                        case DecoratedMemberKind.PropertyGet:
                        case DecoratedMemberKind.PropertySet:
                            if (associatedProperty == null)
                            {
                                Debug.Assert(_targetMethod is SourcePropertyAccessorSymbol);
                                associatedProperty = (PropertySymbol)_targetMethod.AssociatedSymbol;
                            }

                            // As there is no CIL instruction for directly getting a PropertyInfo object from a token, we need to resort to reflection
                            memberInfoExpression = MakePropertyInfoExpression(methodSyntax, associatedProperty);
                            break;

                        default:
                            throw ExceptionUtilities.Unreachable;
                    }

                    prologueStatements.Add(
                        new BoundLocalDeclaration(
                            methodSyntax,
                            methodLocal,
                            new BoundTypeExpression(methodSyntax, null, methodLocal.Type) { WasCompilerGenerated = true },
                            memberInfoExpression,
                            default(ImmutableArray<BoundExpression>))
                        {
                            WasCompilerGenerated = true,
                        });
                }
                if (argumentsParameter != null && _replacementSymbols.ContainsKey(argumentsParameter))
                {
                    Debug.Assert(_bindingTimeAnalyzer.VariableBindingTimes[argumentsParameter] == BindingTime.Dynamic);

                    // Assign an array containing all target method arguments to the dynamic-valued decorator method parameter's replacement local variable in the prologue
                    LocalSymbol argumentsLocal = GetReplacementSymbol(argumentsParameter);
                    prologueLocals.Add(argumentsLocal);

                    var boundsExpression = new BoundLiteral(
                        methodSyntax,
                        ConstantValue.Create(_argumentCount),
                        _compilation.GetSpecialType(SpecialType.System_Int32))
                    {
                        WasCompilerGenerated = true,
                    };

                    BoundArrayInitialization arrayInitialization = null;
                    if (_argumentCount > 0)
                    {
                        var arrayInitializationExpressions = new BoundExpression[_argumentCount];
                        for (int i = 0; i < _argumentCount; i++)
                        {
                            arrayInitializationExpressions[i] = MetaUtils.ConvertIfNeeded(
                                _compilation.ObjectType,
                                new BoundParameter(methodSyntax, _targetMethod.Parameters[i]) { WasCompilerGenerated = true },
                                _compilation);
                        }
                        arrayInitialization = new BoundArrayInitialization(methodSyntax, arrayInitializationExpressions.ToImmutableArray()) { WasCompilerGenerated = true };
                    }

                    var argumentArrayExpression = new BoundArrayCreation(
                        methodSyntax,
                        ImmutableArray.Create<BoundExpression>(boundsExpression),
                        arrayInitialization,
                        _compilation.CreateArrayTypeSymbol(_compilation.ObjectType))
                    {
                        WasCompilerGenerated = true,
                    };

                    prologueStatements.Add(
                        new BoundLocalDeclaration(
                            methodSyntax,
                            argumentsLocal,
                            new BoundTypeExpression(methodSyntax, null, argumentsLocal.Type) { WasCompilerGenerated = true },
                            argumentArrayExpression,
                            default(ImmutableArray<BoundExpression>))
                        {
                            WasCompilerGenerated = true,
                        });

                    // Update the values of all ref and out parameters with the corresponding values in the dynamically-valued argument array in the epilogue
                    if (!_targetMethod.ParameterRefKinds.IsDefaultOrEmpty)
                    {
                        for (int i = 0; i < _argumentCount; i++)
                        {
                            RefKind refKind = _targetMethod.ParameterRefKinds[i];
                            if (refKind != RefKind.None)
                            {
                                ParameterSymbol parameter = _targetMethod.Parameters[i];
                                epilogueStatements.Add(new BoundExpressionStatement(
                                    methodSyntax,
                                    new BoundAssignmentOperator(
                                        methodSyntax,
                                        new BoundParameter(methodSyntax, parameter) { WasCompilerGenerated = true },
                                        MetaUtils.ConvertIfNeeded(
                                            parameter.Type,
                                            new BoundArrayAccess(
                                                methodSyntax,
                                                new BoundLocal(methodSyntax, argumentsLocal, null, argumentsLocal.Type) { WasCompilerGenerated = true },
                                                ImmutableArray.Create<BoundExpression>(
                                                    new BoundLiteral(methodSyntax, ConstantValue.Create(i), _compilation.GetSpecialType(SpecialType.System_Int32)) { WasCompilerGenerated = true }),
                                                _compilation.ObjectType)
                                            {
                                                WasCompilerGenerated = true,
                                            },
                                            _compilation),
                                        refKind,
                                        parameter.Type)
                                    {
                                        WasCompilerGenerated = true,
                                    })
                                {
                                    WasCompilerGenerated = true,
                                });
                            }
                        }
                    }
                }

                if (_targetMethod.ReturnsVoid && bodyRewriteResult.PossibleContinuations.Contains(ExecutionContinuation.NextStatement))
                {
                    epilogueStatements.Add(new BoundReturnStatement(methodSyntax, null) { WasCompilerGenerated = true, });
                }

                // If a prologue was generated, prepend it to the decorated body and append any epilogue after the decorated body
                if (prologueStatements.Count > 0 || epilogueStatements.Count > 0)
                {
                    ImmutableArray<BoundStatement> newStatements =
                        prologueStatements.ToImmutable()
                        .AddRange(decoratedBody.Statements)
                        .AddRange(epilogueStatements.ToImmutable());
                    ImmutableArray<LocalSymbol> newLocals =
                        prologueLocals.ToImmutable()
                        .AddRange(decoratedBody.Locals);
                    decoratedBody = decoratedBody.Update(newLocals, newStatements);
                }

                return decoratedBody;
            }
            catch (SyntheticBoundNodeFactory.MissingPredefinedMember ex)
            {
                _diagnostics.Add(ex.Diagnostic);
                return new BoundBlock(_targetBody.Syntax, ImmutableArray<LocalSymbol>.Empty, ImmutableArray.Create<BoundStatement>(_targetBody), hasErrors: true) { WasCompilerGenerated = true };
            }
        }

        private DecorationRewriteResult VisitWithExtraFlags(DecorationRewriterFlags extraFlags, BoundNode node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriterFlags oldFlags = _flags;
            _flags |= extraFlags;

            DecorationRewriteResult result = Visit(node, variableValues);

            _flags = oldFlags;

            return result;
        }

        private ImmutableArray<DecorationRewriteResult> VisitSequentialList<T>(
            ImmutableArray<T> list,
            ref ImmutableDictionary<Symbol, CompileTimeValue> variableValues,
            DecorationRewriterFlags extraFlags = DecorationRewriterFlags.None)
            where T : BoundNode
        {
            if (list.IsDefaultOrEmpty)
            {
                return ImmutableArray<DecorationRewriteResult>.Empty;
            }
            else
            {
                DecorationRewriterFlags oldFlags = _flags;
                _flags |= extraFlags;

                var itemResults = new DecorationRewriteResult[list.Length];
                for (int i = 0; i < list.Length; i++)
                {
                    DecorationRewriteResult itemResult = Visit(list[i], variableValues);
                    variableValues = itemResult.UpdatedVariableValues;
                    itemResults[i] = itemResult;
                }

                _flags = oldFlags;

                return itemResults.ToImmutableArray();
            }
        }

        private ImmutableArray<DecorationRewriteResult> VisitIndependentList<T>(
            ImmutableArray<T> list,
            ref ImmutableDictionary<Symbol, CompileTimeValue> variableValues,
            DecorationRewriterFlags extraFlags = DecorationRewriterFlags.None)
            where T : BoundNode
        {
            if (list.IsDefaultOrEmpty)
            {
                return ImmutableArray<DecorationRewriteResult>.Empty;
            }
            else
            {
                DecorationRewriterFlags oldFlags = _flags;
                _flags |= extraFlags;

                var itemResults = new DecorationRewriteResult[list.Length];
                ImmutableDictionary<Symbol, CompileTimeValue> newVariableValues = variableValues;
                for (int i = 0; i < list.Length; i++)
                {
                    DecorationRewriteResult itemResult = Visit(list[i], variableValues);
                    newVariableValues = UnifyVariableValues(newVariableValues, itemResult.UpdatedVariableValues);
                    itemResults[i] = itemResult;
                }
                variableValues = newVariableValues;

                _flags = oldFlags;

                return itemResults.ToImmutableArray();
            }
        }

        private ImmutableArray<DecorationRewriteResult> VisitAndTrimStatements<T>(
            ImmutableArray<T> list,
            ref ImmutableDictionary<Symbol, CompileTimeValue> variableValues,
            out ImmutableHashSet<ExecutionContinuation> possibleContinuations)
            where T : BoundStatement
        {
            possibleContinuations = ImmutableHashSet.Create(ExecutionContinuation.NextStatement);
            if (list.IsDefaultOrEmpty)
            {
                return ImmutableArray<DecorationRewriteResult>.Empty;
            }
            else
            {
                ImmutableArray<DecorationRewriteResult>.Builder itemResultsBuilder = ImmutableArray.CreateBuilder<DecorationRewriteResult>(list.Length);
                for (int i = 0; i < list.Length; i++)
                {
                    BoundStatement statement = list[i];
                    if (!possibleContinuations.Contains(ExecutionContinuation.NextStatement))
                    {
                        // If the control cannot flow directly to the next statement, the only way to reach it would be through a label,
                        // so we check whether the statement bears a label. If not, we skip this statement
                        if (statement.Kind != BoundKind.LabelStatement && statement.Kind != BoundKind.LabeledStatement)
                        {
                            continue;
                        }
                    }

                    DecorationRewriteResult itemResult = Visit(statement, variableValues);
                    variableValues = itemResult.UpdatedVariableValues;
                    if (itemResult.MustEmit)
                    {
                        itemResultsBuilder.Add(itemResult);
                    }
                    if (itemResult.HasNextStatementContinuation)
                    {
                        possibleContinuations = possibleContinuations.Union(itemResult.PossibleContinuations);
                    }
                    else
                    {
                        possibleContinuations = possibleContinuations.Remove(ExecutionContinuation.NextStatement).Union(itemResult.PossibleContinuations);
                    }
                }
                return itemResultsBuilder.ToImmutable();
            }
        }

        private DecorationRewriteResult VisitDecoratorArgument(BoundExpression node, Symbol decoratorMember, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            BoundExpression decoratorArgument;
            if (_decoratorArguments.TryGetValue(decoratorMember, out decoratorArgument))
            {
                return VisitWithExtraFlags(DecorationRewriterFlags.InDecoratorArgument, decoratorArgument, variableValues);
            }
            else
            {
                // No value was assigned to the field/property manually or in the decorator constructor, so it contains the default value for the type
                TypeSymbol type = node.Type;
                BoundExpression rewrittenNode;
                CompileTimeValue value;
                if (type.IsClassType())
                {
                    value = new ConstantStaticValue(ConstantValue.Null);
                    rewrittenNode = MakeSimpleStaticValueExpression(value, type, node.Syntax);
                }
                else if (MetaUtils.CheckIsSimpleStaticValueType(type, _compilation))
                {
                    if (type.SpecialType != SpecialType.None)
                    {
                        value = new ConstantStaticValue(ConstantValue.Default(type.SpecialType));
                    }
                    else
                    {
                        Debug.Assert(type.IsEnumType());
                        value = new EnumValue(type, ConstantValue.Default(type.GetEnumUnderlyingType().SpecialType));
                    }
                    rewrittenNode = MakeSimpleStaticValueExpression(value, type, node.Syntax);
                }
                else
                {
                    rewrittenNode = new BoundDefaultOperator(node.Syntax, node.Type) { WasCompilerGenerated = true };
                    value = CompileTimeValue.Dynamic;
                }

                // A lone default expression should never be a stand-alone statement, so we return MustEmit = false
                return new DecorationRewriteResult(rewrittenNode, variableValues, false, value);
            }
        }

        private ImmutableDictionary<Symbol, CompileTimeValue> UnifyVariableValues(
            ImmutableDictionary<Symbol, CompileTimeValue> values1,
            ImmutableDictionary<Symbol, CompileTimeValue> values2)
        {
            Debug.Assert(values1 != null && values2 != null);

            if (values1 != values2)
            {
                var seenKeys = new HashSet<Symbol>();
                foreach (KeyValuePair<Symbol, CompileTimeValue> kv in values1)
                {
                    Symbol key = kv.Key;
                    seenKeys.Add(key);
                    CompileTimeValue value1 = kv.Value;
                    CompileTimeValue value2;
                    if (values2.TryGetValue(key, out value2) && !Equals(value1, value2))
                    {
                        Debug.Assert(value1.Kind == CompileTimeValueKind.Dynamic || value2.Kind == CompileTimeValueKind.Dynamic);
                        values1 = values1.SetItem(key, CompileTimeValue.Dynamic);
                    }
                }

                foreach (KeyValuePair<Symbol, CompileTimeValue> kv in values2)
                {
                    Symbol key = kv.Key;
                    if (seenKeys.Contains(key))
                    {
                        // The values for this key have already been unified in the first loop
                        continue;
                    }
                    values1 = values1.Add(key, kv.Value);
                }
            }

            return values1;
        }

        private LocalSymbol GetReplacementSymbol(Symbol originalSymbol)
        {
            LocalSymbol replacementSymbol;
            if (!_replacementSymbols.TryGetValue(originalSymbol, out replacementSymbol))
            {
                if (originalSymbol.Kind == SymbolKind.Parameter)
                {
                    var parameter = (ParameterSymbol)originalSymbol;
                    replacementSymbol = _factory.SynthesizedLocal(
                        parameter.Type,
                        syntax: parameter.DeclaringSyntaxReferences[0].GetSyntax(),
                        kind: SynthesizedLocalKind.DecoratorParameter,
                        name: _variableNameGenerator.GenerateFreshName(parameter.Name));
                }
                else
                {
                    Debug.Assert(originalSymbol.Kind == SymbolKind.Local);
                    var local = (LocalSymbol)originalSymbol;
                    if (_decoratorMethod.DecoratorMethodVariableTypes[local].Kind == ExtendedTypeKind.ReturnValue && !_targetMethod.ReturnsVoid)
                    {
                        replacementSymbol = _factory.SynthesizedLocal(
                            _targetMethod.ReturnType,
                            syntax: local.GetDeclaratorSyntax(),
                            kind: SynthesizedLocalKind.DecoratorLocal,
                            name: _variableNameGenerator.GenerateFreshName(local.Name));
                    }
                    else if (_decoratorMethod.DecoratorMethodVariableTypes[local].Kind == ExtendedTypeKind.MemberValue)
                    {
                        Debug.Assert(_targetMethod is SourcePropertyAccessorSymbol && _targetMethod.AssociatedSymbol is PropertySymbol);
                        var associatedProperty = (PropertySymbol)_targetMethod.AssociatedSymbol;
                        replacementSymbol = _factory.SynthesizedLocal(
                            associatedProperty.Type,
                            syntax: local.GetDeclaratorSyntax(),
                            kind: SynthesizedLocalKind.DecoratorLocal,
                            name: _variableNameGenerator.GenerateFreshName(local.Name));
                    }
                    else
                    {
                        replacementSymbol = _factory.SynthesizedLocal(
                            local.Type,
                            syntax: local.GetDeclaratorSyntax(),
                            kind: SynthesizedLocalKind.DecoratorLocal,
                            name: _variableNameGenerator.GenerateFreshName(local.Name));
                    }
                }
                Debug.Assert(replacementSymbol != null);
                _replacementSymbols = _replacementSymbols.Add(originalSymbol, replacementSymbol);
            }
            return replacementSymbol;
        }

        private BoundExpression MakeSimpleStaticValueExpression(CompileTimeValue value, TypeSymbol type, CSharpSyntaxNode syntax)
        {
            Debug.Assert(value.Kind == CompileTimeValueKind.Simple);
            if (value is ConstantStaticValue)
            {
                ConstantValue constantValue = ((ConstantStaticValue)value).Value;
                if (constantValue.IsNull)
                {
                    Debug.Assert(type.IsReferenceType || type.IsNullableTypeOrTypeParameter());
                    return new BoundLiteral(syntax, constantValue, type) { WasCompilerGenerated = true };
                }
                else
                {
                    return MetaUtils.ConvertIfNeeded(
                        type,
                        new BoundLiteral(syntax, constantValue, _compilation.GetSpecialType(constantValue.SpecialType)) { WasCompilerGenerated = true },
                        _compilation);
                }
            }
            else
            {
                Debug.Assert(value is TypeValue);
                return MetaUtils.ConvertIfNeeded(
                    type,
                    new BoundTypeOfOperator(
                        syntax,
                        new BoundTypeExpression(syntax, null, ((TypeValue)value).Type) { WasCompilerGenerated = true },
                        null,
                        _compilation.GetWellKnownType(WellKnownType.System_Type))
                    {
                        WasCompilerGenerated = true,
                    },
                    _compilation);
            }
        }

        private BoundBadExpression MakeBadExpression(CSharpSyntaxNode syntax, TypeSymbol type)
        {
            return new BoundBadExpression(syntax, LookupResultKind.Empty, ImmutableArray<Symbol>.Empty, ImmutableArray<BoundNode>.Empty, type, true)
            {
                WasCompilerGenerated = true,
            };
        }

        public BoundExpression MakePropertyInfoExpression(CSharpSyntaxNode syntax, PropertySymbol property)
        {
            return new BoundCall(
                syntax,
                new BoundTypeOfOperator(
                    syntax,
                    new BoundTypeExpression(syntax, null, property.ContainingType) { WasCompilerGenerated = true },
                    null,
                    _compilation.GetWellKnownType(WellKnownType.System_Type))
                {
                    WasCompilerGenerated = true,
                },
                (MethodSymbol)_compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__GetProperty),
                ImmutableArray.Create(
                    new BoundLiteral(syntax, ConstantValue.Create(property.MetadataName), _compilation.GetSpecialType(SpecialType.System_String))
                    {
                        WasCompilerGenerated = true,
                    },
                    MetaUtils.ConvertIfNeeded(
                        _compilation.GetWellKnownType(WellKnownType.System_Reflection_BindingFlags),
                        new BoundLiteral(
                            syntax,
                            ConstantValue.Create((int)(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)),
                            _compilation.GetSpecialType(SpecialType.System_Int32))
                        {
                            WasCompilerGenerated = true,
                        },
                        _compilation)),
                default(ImmutableArray<string>),
                default(ImmutableArray<RefKind>),
                false,
                false,
                false,
                default(ImmutableArray<int>),
                LookupResultKind.Viable,
                _compilation.GetWellKnownType(WellKnownType.System_Reflection_PropertyInfo))
            {
                WasCompilerGenerated = true,
            };
        }

        private BoundNoOpStatement MakeNoOpStatement(CSharpSyntaxNode syntax)
        {
            return new BoundNoOpStatement(syntax, NoOpStatementFlavor.Default)
            {
                WasCompilerGenerated = true,
            };
        }

        private DecorationRewriteResult MakeNoOpResult(CSharpSyntaxNode syntax, ImmutableDictionary<Symbol, CompileTimeValue> updatedVariableValues, ExecutionContinuation possibleContinuation)
        {
            return new DecorationRewriteResult(MakeNoOpStatement(syntax), updatedVariableValues, false, possibleContinuation);
        }

        private DecorationRewriteResult MakeNoOpResult(
            CSharpSyntaxNode syntax,
            ImmutableDictionary<Symbol, CompileTimeValue> updatedVariableValues,
            ImmutableHashSet<ExecutionContinuation> possibleContinuations)
        {
            return new DecorationRewriteResult(MakeNoOpStatement(syntax), updatedVariableValues, false, possibleContinuations);
        }

        private BoundBlock GetBlock(BoundNode node, CSharpSyntaxNode syntax)
        {
            if (node.Kind == BoundKind.Block)
            {
                return (BoundBlock)node;
            }
            else
            {
                Debug.Assert(node is BoundStatement);
                return new BoundBlock(syntax, ImmutableArray<LocalSymbol>.Empty, ImmutableArray.Create((BoundStatement)node)) { WasCompilerGenerated = true, };
            }
        }
    }
}
