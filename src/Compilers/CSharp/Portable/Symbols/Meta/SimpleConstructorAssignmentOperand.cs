// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
