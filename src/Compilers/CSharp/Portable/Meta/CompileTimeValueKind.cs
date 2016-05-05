using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal enum CompileTimeValueKind
    {
        Simple,
        Complex,
        ArgumentArray,
        Dynamic,
    }
}
