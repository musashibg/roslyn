﻿using System.Threading;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal abstract class StaticOnlyBindingTimeAnalyzer : BaseBindingTimeAnalyzer
    {
        protected StaticOnlyBindingTimeAnalyzer(CSharpCompilation compilation, DiagnosticBag diagnostics, CancellationToken cancellationToken, Location sourceLocation)
            : base(compilation, diagnostics, cancellationToken, sourceLocation)
        {
        }

        public override BindingTimeAnalysisResult Visit(BoundNode node, BindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult result = base.Visit(node, flags);

            if (result != null && result.BindingTime == BindingTime.Dynamic && !flags.HasFlag(BindingTimeAnalyzerFlags.InDecoratorCreationExpression))
            {
                Error(ErrorCode.ERR_DynamicBindingTimeInCompileTimeOnlyCode, node.Syntax.Location);
                throw new BindingTimeAnalysisException();
            }

            return result;
        }

        public override BindingTimeAnalysisResult VisitCatchBlock(BoundCatchBlock node, BindingTimeAnalyzerFlags flags)
        {
            Error(ErrorCode.ERR_DynamicBindingTimeInCompileTimeOnlyCode, node.Syntax.Location);
            throw new BindingTimeAnalysisException();
        }

        public override BindingTimeAnalysisResult VisitFixedStatement(BoundFixedStatement node, BindingTimeAnalyzerFlags flags)
        {
            Error(ErrorCode.ERR_DynamicBindingTimeInCompileTimeOnlyCode, node.Syntax.Location);
            throw new BindingTimeAnalysisException();
        }

        public override BindingTimeAnalysisResult VisitLockStatement(BoundLockStatement node, BindingTimeAnalyzerFlags flags)
        {
            Error(ErrorCode.ERR_DynamicBindingTimeInCompileTimeOnlyCode, node.Syntax.Location);
            throw new BindingTimeAnalysisException();
        }

        public override BindingTimeAnalysisResult VisitTryStatement(BoundTryStatement node, BindingTimeAnalyzerFlags flags)
        {
            Error(ErrorCode.ERR_DynamicBindingTimeInCompileTimeOnlyCode, node.Syntax.Location);
            throw new BindingTimeAnalysisException();
        }

        public override BindingTimeAnalysisResult VisitUsingStatement(BoundUsingStatement node, BindingTimeAnalyzerFlags flags)
        {
            Error(ErrorCode.ERR_DynamicBindingTimeInCompileTimeOnlyCode, node.Syntax.Location);
            throw new BindingTimeAnalysisException();
        }
    }
}
