using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp.Symbols.Meta
{
    internal class ExtendedTypeInfo
    {
        private readonly ExtendedTypeKind _kind;
        private readonly TypeSymbol _ordinaryType;
        private readonly bool _isAmbiguous;
        private readonly LocalSymbol _parameterIndexLocal;
        private readonly Symbol _rootSymbol;

        public ExtendedTypeKind Kind
        {
            get { return _kind; }
        }

        public TypeSymbol OrdinaryType
        {
            get { return _ordinaryType; }
        }

        public bool IsAmbiguous
        {
            get { return _isAmbiguous; }
        }

        public LocalSymbol ParameterIndexLocal
        {
            get { return _parameterIndexLocal; }
        }

        public Symbol RootSymbol
        {
            get { return _rootSymbol; }
        }

        public bool IsOrdinaryType
        {
            get
            {
                return Kind == ExtendedTypeKind.OrdinaryType;
            }
        }

        public ExtendedTypeInfo(TypeSymbol ordinaryType)
        {
            _kind = ExtendedTypeKind.OrdinaryType;
            _ordinaryType = ordinaryType;
            _isAmbiguous = false;
            _parameterIndexLocal = null;
        }

        public ExtendedTypeInfo(ExtendedTypeKind kind, TypeSymbol ordinaryType, bool isAmbiguous, LocalSymbol parameterIndexLocal = null, Symbol rootSymbol = null)
        {
            Debug.Assert(kind != ExtendedTypeKind.Parameter || parameterIndexLocal != null);
            Debug.Assert(!IsAmbiguous || rootSymbol != null);

            _kind = kind;
            _ordinaryType = ordinaryType;
            _isAmbiguous = isAmbiguous;
            _parameterIndexLocal = parameterIndexLocal;
            _rootSymbol = rootSymbol;
        }

        public static ExtendedTypeInfo CreateThisObjectType(CSharpCompilation compilation)
        {
            return new ExtendedTypeInfo(ExtendedTypeKind.ThisObject, compilation.ObjectType, false);
        }

        public static ExtendedTypeInfo CreateArgumentArrayType(CSharpCompilation compilation, bool isAmbiguous, Symbol rootSymbol = null)
        {
            TypeSymbol objectArrayType = compilation.CreateArrayTypeSymbol(compilation.ObjectType);
            return new ExtendedTypeInfo(ExtendedTypeKind.ArgumentArray, objectArrayType, isAmbiguous, rootSymbol: rootSymbol);
        }

        public static ExtendedTypeInfo CreateParameterType(CSharpCompilation compilation, LocalSymbol parameterIndexLocal)
        {
            return new ExtendedTypeInfo(ExtendedTypeKind.Parameter, compilation.ObjectType, false, parameterIndexLocal);
        }

        public static ExtendedTypeInfo CreateReturnValueType(CSharpCompilation compilation, bool isAmbiguous, Symbol rootSymbol = null)
        {
            TypeSymbol objectType = compilation.ObjectType;
            return new ExtendedTypeInfo(ExtendedTypeKind.ReturnValue, objectType, isAmbiguous, rootSymbol: rootSymbol);
        }

        public static ExtendedTypeInfo CreateMemberValueType(CSharpCompilation compilation, bool isAmbiguous, Symbol rootSymbol = null)
        {
            TypeSymbol objectType = compilation.ObjectType;
            return new ExtendedTypeInfo(ExtendedTypeKind.MemberValue, objectType, isAmbiguous, rootSymbol: rootSymbol);
        }

        public bool MatchesSpecialType(ExtendedTypeInfo other)
        {
            return Kind == other.Kind && ParameterIndexLocal == other.ParameterIndexLocal;
        }

        public ExtendedTypeInfo UpdateToUnambiguousOrdinaryType()
        {
            Debug.Assert(IsAmbiguous);

            return new ExtendedTypeInfo(OrdinaryType);
        }

        public ExtendedTypeInfo UpdateToUnambiguousSpecialType()
        {
            Debug.Assert(IsAmbiguous);

            return new ExtendedTypeInfo(Kind, OrdinaryType, false, ParameterIndexLocal);
        }
    }
}
