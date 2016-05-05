namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal enum DecorationRewriterFlags
    {
        None,
        ProhibitSpliceLocation = 1 << 0,
        ExpectedDynamicArgumentArray = 1 << 1,
    }
}
