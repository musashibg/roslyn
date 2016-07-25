// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Symbols.Meta;
using Roslyn.Utilities;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal sealed class DecorationBindingTimeAnalyzer : BaseBindingTimeAnalyzer
    {
        private readonly DecoratedMemberKind _targetMemberKind;
        private readonly SourceMemberMethodSymbol _decoratorMethod;
        private readonly ImmutableDictionary<Symbol, BoundExpression> _decoratorArguments;

        public DecorationBindingTimeAnalyzer(
            CSharpCompilation compilation,
            DiagnosticBag diagnostics,
            Location sourceLocation,
            DecoratedMemberKind targetMemberKind,
            SourceMemberMethodSymbol decoratorMethod,
            ImmutableDictionary<Symbol, BoundExpression> decoratorArguments,
            CancellationToken cancellationToken)
            : base(compilation, diagnostics, sourceLocation, cancellationToken)
        {
            _targetMemberKind = targetMemberKind;
            _decoratorMethod = decoratorMethod;
            _decoratorArguments = decoratorArguments;
        }

        public override BindingTimeAnalysisResult VisitCall(BoundCall node, BindingTimeAnalyzerFlags flags)
        {
            if (!flags.HasFlag(BindingTimeAnalyzerFlags.InMetaDecorationArgument)
                && (MetaUtils.CheckIsSpliceLocation(node, Compilation, _targetMemberKind, _decoratorMethod, AddDiagnostic)
                    || MetaUtils.CheckIsBaseMethodCall(node, _decoratorMethod)))
            {
                return new BindingTimeAnalysisResult(BindingTime.Dynamic);
            }

            MethodSymbol method = node.Method;
            if (method == Compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_PropertyInfo__GetValue)
                || method == Compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_PropertyInfo__SetValue))
            {
                // We will transform calls to PropertyInfo.GetValue and PropertyInfo.SetValue to direct property accesses, so we do not need to force
                // the property variable to have dynamic binding time
                BindingTimeAnalysisResult receiverResult = Visit(node.ReceiverOpt, flags);
                Debug.Assert(receiverResult.BindingTime != BindingTime.StaticArgumentArray);

                ImmutableArray<BindingTimeAnalysisResult> argumentsResults = VisitArguments(node.Arguments, node.ArgumentRefKindsOpt, flags, true);

                return new BindingTimeAnalysisResult(BindingTime.Dynamic);
            }

            return base.VisitCall(node, flags);
        }

        public override BindingTimeAnalysisResult VisitFieldAccess(BoundFieldAccess node, BindingTimeAnalyzerFlags flags)
        {
            if (!flags.HasFlag(BindingTimeAnalyzerFlags.InMetaDecorationArgument))
            {
                BoundExpression strippedReceiver = MetaUtils.StripConversions(node.ReceiverOpt);
                if (strippedReceiver != null &&(strippedReceiver.Kind == BoundKind.BaseReference || strippedReceiver.Kind == BoundKind.ThisReference))
                {
                    return VisitDecoratorArgument(node, node.FieldSymbol, flags);
                }
            }

            return base.VisitFieldAccess(node, flags);
        }

        public override BindingTimeAnalysisResult VisitPropertyAccess(BoundPropertyAccess node, BindingTimeAnalyzerFlags flags)
        {
            if (!flags.HasFlag(BindingTimeAnalyzerFlags.InMetaDecorationArgument))
            {
                BoundExpression strippedReceiver = MetaUtils.StripConversions(node.ReceiverOpt);
                if (strippedReceiver != null &&(strippedReceiver.Kind == BoundKind.BaseReference || strippedReceiver.Kind == BoundKind.ThisReference))
                {
                    return VisitDecoratorArgument(node, node.PropertySymbol, flags);
                }
            }

            return base.VisitPropertyAccess(node, flags);
        }

        public bool PerformAnalysis()
        {
            Debug.Assert(_decoratorMethod.EarlyBoundBody != null);
            BoundBlock decoratorBody = _decoratorMethod.EarlyBoundBody;
            return PerformAnalysis(decoratorBody);
        }

        protected override ImmutableDictionary<Symbol, BindingTime> GetInitialVariableBindingTimes()
        {
            Debug.Assert(_decoratorMethod.DecoratorMethodVariableTypes != null);
            // Build the most optimistic variable binding times configuration, where as many variables as possible have static binding times
            return _decoratorMethod.DecoratorMethodVariableTypes.ToImmutableDictionary(
                kv => kv.Key,
                kv =>
                {
                    if (kv.Value.Kind == ExtendedTypeKind.ArgumentArray)
                    {
                        // All argument array parameters and variables are initially assumed to be static argument arrays
                        return BindingTime.StaticArgumentArray;
                    }
                    else if (kv.Key == _decoratorMethod.Parameters[1]
                             || ((_targetMemberKind == DecoratedMemberKind.IndexerSet || _targetMemberKind == DecoratedMemberKind.PropertySet) && kv.Key == _decoratorMethod.Parameters[2])
                             || kv.Key is RangeVariableSymbol)
                    {
                        // The thisObject parameter of the decorator method is initially dynamic, and so are all range variables
                        return BindingTime.Dynamic;
                    }
                    else
                    {
                        // The method parameter and all other variables are initialy assumed to be static
                        return MetaUtils.CheckIsSimpleStaticValueType(kv.Value.OrdinaryType, Compilation)
                                ? BindingTime.StaticSimpleValue
                                : BindingTime.StaticComplexValue;
                    }
                });
        }

        protected override bool IsGuaranteedStaticSymbol(Symbol symbol)
        {
            return symbol.Kind == SymbolKind.Parameter && _decoratorMethod.Parameters.Contains((ParameterSymbol)symbol);
        }

        private BindingTimeAnalysisResult VisitDecoratorArgument(BoundExpression node, Symbol decoratorMember, BindingTimeAnalyzerFlags flags)
        {
            BoundExpression decoratorArgument;
            if (_decoratorArguments.TryGetValue(decoratorMember, out decoratorArgument))
            {
                return Visit(decoratorArgument, flags | BindingTimeAnalyzerFlags.InMetaDecorationArgument);
            }
            else
            {
                // No value was assigned to the field/property manually or in the decorator constructor, so it contains the default value for the type
                TypeSymbol type = node.Type;
                return (type.IsClassType() || MetaUtils.CheckIsSimpleStaticValueType(node.Type, Compilation))
                        ? new BindingTimeAnalysisResult(BindingTime.StaticSimpleValue)
                        : new BindingTimeAnalysisResult(BindingTime.Dynamic);
            }
        }
    }
}
