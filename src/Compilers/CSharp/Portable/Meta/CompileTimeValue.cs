// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal abstract class CompileTimeValue
    {
        private readonly CompileTimeValueKind _kind;

        public static DynamicValue Dynamic
        {
            get { return DynamicValue.Singleton; }
        }

        public CompileTimeValueKind Kind
        {
            get { return _kind; }
        }

        protected CompileTimeValue(CompileTimeValueKind kind)
        {
            _kind = kind;
        }
    }
}
