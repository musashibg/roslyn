// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal abstract class StaticOnlyBindingTimeAnalyzer : BaseBindingTimeAnalyzer
    {
        protected StaticOnlyBindingTimeAnalyzer(CSharpCompilation compilation, DiagnosticBag diagnostics, Location sourceLocation, CancellationToken cancellationToken)
            : base(compilation, diagnostics, sourceLocation, cancellationToken)
        {
        }

        public override BindingTimeAnalysisResult Visit(BoundNode node, BindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult result = base.Visit(node, flags);

            if (result != null && result.BindingTime == BindingTime.Dynamic && !flags.HasFlag(BindingTimeAnalyzerFlags.InDecoratorCreationExpression))
            {
                AddDiagnostic(ErrorCode.ERR_DynamicBindingTimeInCompileTimeOnlyCode, node.Syntax.Location);
                throw new BindingTimeAnalysisException();
            }

            return result;
        }

        public override BindingTimeAnalysisResult VisitCatchBlock(BoundCatchBlock node, BindingTimeAnalyzerFlags flags)
        {
            AddDiagnostic(ErrorCode.ERR_DynamicBindingTimeInCompileTimeOnlyCode, node.Syntax.Location);
            throw new BindingTimeAnalysisException();
        }

        public override BindingTimeAnalysisResult VisitFixedStatement(BoundFixedStatement node, BindingTimeAnalyzerFlags flags)
        {
            AddDiagnostic(ErrorCode.ERR_DynamicBindingTimeInCompileTimeOnlyCode, node.Syntax.Location);
            throw new BindingTimeAnalysisException();
        }

        public override BindingTimeAnalysisResult VisitLockStatement(BoundLockStatement node, BindingTimeAnalyzerFlags flags)
        {
            AddDiagnostic(ErrorCode.ERR_DynamicBindingTimeInCompileTimeOnlyCode, node.Syntax.Location);
            throw new BindingTimeAnalysisException();
        }

        public override BindingTimeAnalysisResult VisitTryStatement(BoundTryStatement node, BindingTimeAnalyzerFlags flags)
        {
            AddDiagnostic(ErrorCode.ERR_DynamicBindingTimeInCompileTimeOnlyCode, node.Syntax.Location);
            throw new BindingTimeAnalysisException();
        }

        public override BindingTimeAnalysisResult VisitUsingStatement(BoundUsingStatement node, BindingTimeAnalyzerFlags flags)
        {
            AddDiagnostic(ErrorCode.ERR_DynamicBindingTimeInCompileTimeOnlyCode, node.Syntax.Location);
            throw new BindingTimeAnalysisException();
        }
    }
}
