namespace Microsoft.CodeAnalysis.CSharp.Symbols.Meta
{
    internal struct SimpleConstructorAssignmentOperand
    {
        public readonly ParameterSymbol Parameter;
        public readonly BoundExpression Expression;

        public SimpleConstructorAssignmentOperand(ParameterSymbol parameter)
        {
            Parameter = parameter;
            Expression = null;
        }

        public SimpleConstructorAssignmentOperand(BoundExpression expression)
        {
            Parameter = null;
            Expression = expression;
        }
    }
}
