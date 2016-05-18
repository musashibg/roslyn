using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Symbols.Meta;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal partial class Binder
    {
        internal static void BindMetaDecorationTypes(
            ImmutableArray<Binder> binders,
            ImmutableArray<MetaDecorationSyntax> metaDecorationsToBind,
            Symbol ownerSymbol,
            NamedTypeSymbol[] boundMetaDecorationTypes,
            DiagnosticBag diagnostics)
        {
            Debug.Assert(binders.Any());
            Debug.Assert(metaDecorationsToBind.Any());
            Debug.Assert((object)ownerSymbol != null);
            Debug.Assert(binders.Length == metaDecorationsToBind.Length);
            Debug.Assert(boundMetaDecorationTypes != null);

            for (int i = 0; i < metaDecorationsToBind.Length; i++)
            {
                var binder = binders[i];

                // BindType for MetaDecorationSyntax's name is handled specially during lookup, see Binder.LookupDecoratorType and Binder.LookupMetaclassType.
                // When looking up a name in decorator type context, we generate a diagnostic + error type if it is not a decorator/metaclass type,
                // i.e. named type deriving from CSharp.Meta.Decorator or CSharp.Meta.Metaclass, respectively.
                // Hence we can assume here that BindType returns a NamedTypeSymbol.
                boundMetaDecorationTypes[i] = (NamedTypeSymbol)binder.BindType(metaDecorationsToBind[i].Name, diagnostics);
            }
        }

        // Method to bind all decorators (decorator arguments and constructor)
        internal static void GetDecorators(
            ImmutableArray<Binder> binders,
            ImmutableArray<MetaDecorationSyntax> decoratorsToBind,
            ImmutableArray<NamedTypeSymbol> boundDecoratorTypes,
            DecoratorData[] decoratorsBuilder,
            DiagnosticBag diagnostics)
        {
            Debug.Assert(binders.Any());
            Debug.Assert(decoratorsToBind.Any());
            Debug.Assert(boundDecoratorTypes.Any());
            Debug.Assert(binders.Length == decoratorsToBind.Length);
            Debug.Assert(boundDecoratorTypes.Length == decoratorsToBind.Length);
            Debug.Assert(decoratorsBuilder != null);

            for (int i = 0; i < decoratorsToBind.Length; i++)
            {
                MetaDecorationSyntax decoratorSyntax = decoratorsToBind[i];
                NamedTypeSymbol boundDecoratorType = boundDecoratorTypes[i];
                Binder binder = binders[i];

                decoratorsBuilder[i] = binder.GetDecorator(decoratorSyntax, boundDecoratorType, diagnostics);
            }
        }

        #region Bind Single Decorator

        internal DecoratorData GetDecorator(MetaDecorationSyntax node, NamedTypeSymbol boundDecoratorType, DiagnosticBag diagnostics)
        {
            var boundDecorator = BindDecorator(node, boundDecoratorType, diagnostics);

            return GetDecorator(boundDecorator, diagnostics);
        }

        internal BoundDecorator BindDecorator(MetaDecorationSyntax node, NamedTypeSymbol decoratorType, DiagnosticBag diagnostics)
        {
            // If decorator name bound to an error type with a single named type
            // candidate symbol, we want to bind the decorator constructor
            // and arguments with that named type to generate better semantic info.

            NamedTypeSymbol nonErrorDecoratorType = null;
            LookupResultKind resultKind = LookupResultKind.Viable;
            if (decoratorType.IsErrorType())
            {
                var errorType = (ErrorTypeSymbol)decoratorType;
                resultKind = errorType.ResultKind;
                if (errorType.CandidateSymbols.Length == 1 && errorType.CandidateSymbols[0] is NamedTypeSymbol)
                {
                    nonErrorDecoratorType = errorType.CandidateSymbols[0] as SourceNamedTypeSymbol;
                }
            }
            else
            {
                nonErrorDecoratorType = decoratorType;
            }

            // Bind constructor and named decorator arguments using the decorator binder
            var argumentListOpt = node.ArgumentList;
            Binder decoratorArgumentBinder = this.WithAdditionalFlags(BinderFlags.MetaDecorationArgument);
            AnalyzedMetaDecorationArguments analyzedArguments = decoratorArgumentBinder.BindMetaDecorationArguments(argumentListOpt, nonErrorDecoratorType, diagnostics);

            MethodSymbol decoratorConstructor = null;
            if (nonErrorDecoratorType != null)
            {
                var decoratorTypeForBinding = nonErrorDecoratorType as SourceNamedTypeSymbol;
                if (decoratorTypeForBinding == null)
                {
                    diagnostics.Add(ErrorCode.ERR_NonSourceDecoratorClass, node.Location, decoratorType);
                }

                // Bind the decorator type's constructor based on the bound constructor arguments
                HashSet<DiagnosticInfo> useSiteDiagnostics = null;
                decoratorConstructor = BindMetaDecorationConstructor(
                    node,
                    decoratorTypeForBinding,
                    analyzedArguments.ConstructorArguments,
                    diagnostics,
                    ref resultKind,
                    suppressErrors: false,
                    useSiteDiagnostics: ref useSiteDiagnostics);
                diagnostics.Add(node, useSiteDiagnostics);
            }

            if ((object)decoratorConstructor != null)
            {
                ReportDiagnosticsIfObsolete(diagnostics, decoratorConstructor, node, hasBaseReceiver: false);
            }

            var constructorArguments = analyzedArguments.ConstructorArguments;
            ImmutableArray<BoundExpression> boundConstructorArguments = constructorArguments.Arguments.ToImmutableAndFree();
            ImmutableArray<string> boundConstructorArgumentNamesOpt = constructorArguments.GetNames();
            ImmutableArray<BoundExpression> boundNamedArguments = analyzedArguments.NamedArguments;
            constructorArguments.Free();

            return new BoundDecorator(
                node,
                decoratorConstructor,
                boundConstructorArguments,
                boundConstructorArgumentNamesOpt,
                boundNamedArguments,
                resultKind,
                nonErrorDecoratorType ?? decoratorType,
                hasErrors: resultKind != LookupResultKind.Viable);
        }

        private DecoratorData GetDecorator(BoundDecorator boundDecorator, DiagnosticBag diagnostics)
        {
            var decoratorType = (NamedTypeSymbol)boundDecorator.Type;
            var decoratorConstructor = boundDecorator.Constructor;

            Debug.Assert((object)decoratorType != null);

            bool hasErrors = boundDecorator.HasAnyErrors;

            if (decoratorType.IsErrorType() || decoratorType.IsAbstract || (object)decoratorConstructor == null)
            {
                // prevent cascading diagnostics
                Debug.Assert(hasErrors);
                return new DecoratorData(boundDecorator.Syntax.GetReference(), decoratorType, decoratorConstructor, hasErrors);
            }

            // Validate the decorator arguments and generate TypedConstant for argument's BoundExpression.
            ImmutableArray<BoundExpression> arguments = boundDecorator.ConstructorArguments;
            ImmutableArray<KeyValuePair<string, BoundExpression>> namedArguments = UnpackNamedArguments(boundDecorator.NamedArguments, diagnostics, ref hasErrors);

            ImmutableArray<int> constructorArgumentsSourceIndices;
            ImmutableArray<BoundExpression> constructorArguments;
            if (hasErrors || decoratorConstructor.ParameterCount == 0)
            {
                constructorArgumentsSourceIndices = default(ImmutableArray<int>);
                constructorArguments = arguments;
            }
            else
            {
                constructorArguments = GetRewrittenMetaDecorationConstructorArguments(
                    out constructorArgumentsSourceIndices,
                    decoratorConstructor,
                    arguments,
                    boundDecorator.ConstructorArgumentNamesOpt,
                    (MetaDecorationSyntax)boundDecorator.Syntax,
                    diagnostics,
                    ref hasErrors);
            }

            return new DecoratorData(
                boundDecorator.Syntax.GetReference(),
                decoratorType,
                decoratorConstructor,
                constructorArguments,
                constructorArgumentsSourceIndices,
                namedArguments,
                hasErrors);
        }

        #endregion

        // Method to bind all metaclasses (metaclass arguments and constructor)
        internal static void GetMetaclasses(
            ImmutableArray<Binder> binders,
            ImmutableArray<MetaDecorationSyntax> metaclassesToBind,
            ImmutableArray<NamedTypeSymbol> boundMetaclassTypes,
            MetaclassData[] metaclassesBuilder,
            DiagnosticBag diagnostics)
        {
            Debug.Assert(binders.Any());
            Debug.Assert(metaclassesToBind.Any());
            Debug.Assert(boundMetaclassTypes.Any());
            Debug.Assert(binders.Length == metaclassesToBind.Length);
            Debug.Assert(boundMetaclassTypes.Length == metaclassesToBind.Length);
            Debug.Assert(metaclassesBuilder != null);

            for (int i = 0; i < metaclassesToBind.Length; i++)
            {
                MetaDecorationSyntax metaclassSyntax = metaclassesToBind[i];
                NamedTypeSymbol boundMetaclassType = boundMetaclassTypes[i];
                Binder binder = binders[i];

                metaclassesBuilder[i] = binder.GetMetaclass(metaclassSyntax, boundMetaclassType, diagnostics);
            }
        }

        #region Bind Single Metaclass

        internal MetaclassData GetMetaclass(MetaDecorationSyntax node, NamedTypeSymbol boundMetaclassType, DiagnosticBag diagnostics)
        {
            var boundMetaclass = BindMetaclass(node, boundMetaclassType, diagnostics);

            return GetMetaclass(boundMetaclass, diagnostics);
        }

        internal BoundMetaclass BindMetaclass(MetaDecorationSyntax node, NamedTypeSymbol metaclassType, DiagnosticBag diagnostics)
        {
            // If metaclass name bound to an error type with a single named type
            // candidate symbol, we want to bind the metaclass constructor
            // and arguments with that named type to generate better semantic info.

            NamedTypeSymbol nonErrorMetaclassType = null;
            LookupResultKind resultKind = LookupResultKind.Viable;
            if (metaclassType.IsErrorType())
            {
                var errorType = (ErrorTypeSymbol)metaclassType;
                resultKind = errorType.ResultKind;
                if (errorType.CandidateSymbols.Length == 1 && errorType.CandidateSymbols[0] is NamedTypeSymbol)
                {
                    nonErrorMetaclassType = errorType.CandidateSymbols[0] as SourceNamedTypeSymbol;
                }
            }
            else
            {
                nonErrorMetaclassType = metaclassType;
            }

            // Bind constructor and named metaclass arguments using the metaclass binder
            var argumentListOpt = node.ArgumentList;
            Binder metaclassArgumentBinder = this.WithAdditionalFlags(BinderFlags.MetaDecorationArgument);
            AnalyzedMetaDecorationArguments analyzedArguments = metaclassArgumentBinder.BindMetaDecorationArguments(argumentListOpt, nonErrorMetaclassType, diagnostics);

            MethodSymbol metaclassConstructor = null;
            if (nonErrorMetaclassType != null)
            {
                var metaclassTypeForBinding = nonErrorMetaclassType as SourceNamedTypeSymbol;
                if (metaclassTypeForBinding == null)
                {
                    diagnostics.Add(ErrorCode.ERR_NonSourceMetaclass, node.Location, metaclassType);
                }

                // Bind the metaclass type's constructor based on the bound constructor arguments
                HashSet<DiagnosticInfo> useSiteDiagnostics = null;
                metaclassConstructor = BindMetaDecorationConstructor(
                    node,
                    metaclassTypeForBinding,
                    analyzedArguments.ConstructorArguments,
                    diagnostics,
                    ref resultKind,
                    suppressErrors: false,
                    useSiteDiagnostics: ref useSiteDiagnostics);
                diagnostics.Add(node, useSiteDiagnostics);
            }

            if ((object)metaclassConstructor != null)
            {
                ReportDiagnosticsIfObsolete(diagnostics, metaclassConstructor, node, hasBaseReceiver: false);
            }

            var constructorArguments = analyzedArguments.ConstructorArguments;
            ImmutableArray<BoundExpression> boundConstructorArguments = constructorArguments.Arguments.ToImmutableAndFree();
            ImmutableArray<string> boundConstructorArgumentNamesOpt = constructorArguments.GetNames();
            ImmutableArray<BoundExpression> boundNamedArguments = analyzedArguments.NamedArguments;
            constructorArguments.Free();

            return new BoundMetaclass(
                node,
                metaclassConstructor,
                boundConstructorArguments,
                boundConstructorArgumentNamesOpt,
                boundNamedArguments,
                resultKind,
                nonErrorMetaclassType ?? metaclassType,
                hasErrors: resultKind != LookupResultKind.Viable);
        }

        private MetaclassData GetMetaclass(BoundMetaclass boundMetaclass, DiagnosticBag diagnostics)
        {
            var metaclassType = (NamedTypeSymbol)boundMetaclass.Type;
            var metaclassConstructor = boundMetaclass.Constructor;

            Debug.Assert((object)metaclassType != null);

            bool hasErrors = boundMetaclass.HasAnyErrors;

            if (metaclassType.IsErrorType() || metaclassType.IsAbstract || (object)metaclassConstructor == null)
            {
                // prevent cascading diagnostics
                Debug.Assert(hasErrors);
                return new MetaclassData(boundMetaclass.Syntax.GetReference(), metaclassType, metaclassConstructor, hasErrors);
            }

            // Validate the metaclass arguments and generate TypedConstant for argument's BoundExpression.
            ImmutableArray<BoundExpression> arguments = boundMetaclass.ConstructorArguments;
            ImmutableArray<KeyValuePair<string, BoundExpression>> namedArguments = UnpackNamedArguments(boundMetaclass.NamedArguments, diagnostics, ref hasErrors);

            ImmutableArray<int> constructorArgumentsSourceIndices;
            ImmutableArray<BoundExpression> constructorArguments;
            if (hasErrors || metaclassConstructor.ParameterCount == 0)
            {
                constructorArgumentsSourceIndices = default(ImmutableArray<int>);
                constructorArguments = arguments;
            }
            else
            {
                constructorArguments = GetRewrittenMetaDecorationConstructorArguments(
                    out constructorArgumentsSourceIndices,
                    metaclassConstructor,
                    arguments,
                    boundMetaclass.ConstructorArgumentNamesOpt,
                    (MetaDecorationSyntax)boundMetaclass.Syntax,
                    diagnostics,
                    ref hasErrors);
            }

            return new MetaclassData(
                boundMetaclass.Syntax.GetReference(),
                metaclassType,
                metaclassConstructor,
                constructorArguments,
                constructorArgumentsSourceIndices,
                namedArguments,
                hasErrors);
        }

        #endregion

        #region Shared decorator and metaclass routines

        private AnalyzedMetaDecorationArguments BindMetaDecorationArguments(
            MetaDecorationArgumentListSyntax metaDecorationArgumentList,
            NamedTypeSymbol metaDecorationType,
            DiagnosticBag diagnostics)
        {
            var boundConstructorArguments = AnalyzedArguments.GetInstance();
            var boundNamedArguments = ImmutableArray<BoundExpression>.Empty;
            if (metaDecorationArgumentList != null)
            {
                ArrayBuilder<BoundExpression> boundNamedArgumentsBuilder = null;
                HashSet<string> boundNamedArgumentsSet = null;

                // Avoid "cascading" errors for constructor arguments.
                // We will still generate errors for each duplicate named decorator/metaclass argument,
                // matching Dev10 compiler behavior.
                bool hadError = false;

                foreach (var argument in metaDecorationArgumentList.Arguments)
                {
                    if (argument.NameEquals == null)
                    {
                        // Constructor argument
                        hadError |= this.BindArgumentAndName(
                            boundConstructorArguments,
                            diagnostics,
                            hadError,
                            argument,
                            argument.Expression,
                            argument.NameColon,
                            refKind: RefKind.None,
                            allowArglist: false);

                        if (boundNamedArgumentsBuilder != null)
                        {
                            // Error CS1016: Named decorator/metaclass argument expected
                            // This has been reported by the parser.
                            hadError = true;
                        }
                    }
                    else
                    {
                        // Named argument
                        // TODO: use fully qualified identifier name for boundNamedArgumentsSet
                        string argumentName = argument.NameEquals.Name.Identifier.ValueText;
                        if (boundNamedArgumentsBuilder == null)
                        {
                            boundNamedArgumentsBuilder = ArrayBuilder<BoundExpression>.GetInstance();
                            boundNamedArgumentsSet = new HashSet<string>();
                        }
                        else if (boundNamedArgumentsSet.Contains(argumentName))
                        {
                            // Duplicate named argument
                            Error(diagnostics, ErrorCode.ERR_DuplicateNamedMetaDecorationArgument, argument, argumentName);
                            hadError = true;
                        }

                        BoundExpression boundNamedArgument = BindNamedMetaDecorationArgument(argument, metaDecorationType, diagnostics);
                        boundNamedArgumentsBuilder.Add(boundNamedArgument);
                        boundNamedArgumentsSet.Add(argumentName);
                    }
                }

                if (boundNamedArgumentsBuilder != null)
                {
                    boundNamedArguments = boundNamedArgumentsBuilder.ToImmutableAndFree();
                }
            }

            return new AnalyzedMetaDecorationArguments(boundConstructorArguments, boundNamedArguments);
        }

        private BoundExpression BindNamedMetaDecorationArgument(MetaDecorationArgumentSyntax namedArgument, NamedTypeSymbol metaDecorationType, DiagnosticBag diagnostics)
        {
            bool wasError;
            LookupResultKind resultKind;
            Symbol namedArgumentNameSymbol = BindNamedMetaDecorationArgumentName(namedArgument, metaDecorationType, diagnostics, out wasError, out resultKind);

            ReportDiagnosticsIfObsolete(diagnostics, namedArgumentNameSymbol, namedArgument, hasBaseReceiver: false);

            Debug.Assert(resultKind == LookupResultKind.Viable || wasError);

            TypeSymbol namedArgumentType;
            if (wasError)
            {
                namedArgumentType = CreateErrorType();  // don't generate cascaded errors.
            }
            else
            {
                namedArgumentType = BindNamedMetaDecorationArgumentType(namedArgument, namedArgumentNameSymbol, metaDecorationType, diagnostics);
            }

            // BindRValue just binds the expression without doing any validation (if its a valid expression for a decorator/metaclass argument).
            BoundExpression namedArgumentValue = this.BindValue(namedArgument.Expression, diagnostics, BindValueKind.RValue);
            namedArgumentValue = GenerateConversionForAssignment(namedArgumentType, namedArgumentValue, diagnostics);

            // TODO: should we create an entry even if there are binding errors?
            var fieldSymbol = namedArgumentNameSymbol as FieldSymbol;
            IdentifierNameSyntax nameSyntax = namedArgument.NameEquals.Name;
            BoundExpression lvalue;
            if ((object)fieldSymbol != null)
            {
                var containingAssembly = fieldSymbol.ContainingAssembly as SourceAssemblySymbol;

                // We do not want to generate any unassigned field or unreferenced field diagnostics.
                containingAssembly?.NoteFieldAccess(fieldSymbol, read: true, write: true);

                lvalue = new BoundFieldAccess(nameSyntax, null, fieldSymbol, ConstantValue.NotAvailable, resultKind, fieldSymbol.Type);
            }
            else
            {
                var propertySymbol = namedArgumentNameSymbol as PropertySymbol;
                if ((object)propertySymbol != null)
                {
                    lvalue = new BoundPropertyAccess(nameSyntax, null, propertySymbol, resultKind, namedArgumentType);
                }
                else
                {
                    lvalue = BadExpression(nameSyntax, resultKind);
                }
            }

            return new BoundAssignmentOperator(namedArgument, lvalue, namedArgumentValue, namedArgumentType);
        }

        private Symbol BindNamedMetaDecorationArgumentName(
            MetaDecorationArgumentSyntax namedArgument,
            NamedTypeSymbol metaDecorationType,
            DiagnosticBag diagnostics,
            out bool wasError,
            out LookupResultKind resultKind)
        {
            var identifierName = namedArgument.NameEquals.Name;
            var name = identifierName.Identifier.ValueText;
            LookupResult result = LookupResult.GetInstance();
            HashSet<DiagnosticInfo> useSiteDiagnostics = null;
            this.LookupMembersWithFallback(result, metaDecorationType, name, 0, ref useSiteDiagnostics);
            diagnostics.Add(identifierName, useSiteDiagnostics);
            Symbol resultSymbol = this.ResultSymbol(result, name, 0, identifierName, diagnostics, false, out wasError);
            resultKind = result.Kind;
            result.Free();
            return resultSymbol;
        }

        private TypeSymbol BindNamedMetaDecorationArgumentType(
            MetaDecorationArgumentSyntax namedArgument,
            Symbol namedArgumentNameSymbol,
            NamedTypeSymbol metaDecorationType,
            DiagnosticBag diagnostics)
        {
            if (namedArgumentNameSymbol.Kind == SymbolKind.ErrorType)
            {
                return (TypeSymbol)namedArgumentNameSymbol;
            }

            // SPEC:    For each named-argument Arg in named-argument-list N:
            // SPEC:        Let Name be the identifier of the named-argument Arg.
            // SPEC:        Name must identify a non-static read-write public field or property on 
            // SPEC:            decorator/metaclass class T. If T has no such field or property, then a compile-time error occurs.

            bool invalidNamedArgument = false;
            TypeSymbol namedArgumentType = null;
            invalidNamedArgument |= (namedArgumentNameSymbol.DeclaredAccessibility != Accessibility.Public);
            invalidNamedArgument |= namedArgumentNameSymbol.IsStatic;

            if (!invalidNamedArgument)
            {
                switch (namedArgumentNameSymbol.Kind)
                {
                    case SymbolKind.Field:
                        var fieldSymbol = (FieldSymbol)namedArgumentNameSymbol;
                        namedArgumentType = fieldSymbol.Type;
                        invalidNamedArgument |= fieldSymbol.IsReadOnly;
                        invalidNamedArgument |= fieldSymbol.IsConst;
                        break;

                    case SymbolKind.Property:
                        var propertySymbol = ((PropertySymbol)namedArgumentNameSymbol).GetLeastOverriddenProperty(this.ContainingType);
                        namedArgumentType = propertySymbol.Type;
                        invalidNamedArgument |= propertySymbol.IsReadOnly;
                        var getMethod = propertySymbol.GetMethod;
                        var setMethod = propertySymbol.SetMethod;
                        invalidNamedArgument = invalidNamedArgument || (object)getMethod == null || (object)setMethod == null;
                        if (!invalidNamedArgument)
                        {
                            invalidNamedArgument =
                                getMethod.DeclaredAccessibility != Accessibility.Public ||
                                setMethod.DeclaredAccessibility != Accessibility.Public;
                        }
                        break;

                    default:
                        invalidNamedArgument = true;
                        break;
                }
            }

            if (invalidNamedArgument)
            {
                return new ExtendedErrorTypeSymbol(metaDecorationType,
                    namedArgumentNameSymbol,
                    LookupResultKind.NotAVariable,
                    diagnostics.Add(ErrorCode.ERR_BadNamedMetaDecorationArgument,
                        namedArgument.NameEquals.Name.Location,
                        namedArgumentNameSymbol.Name));
            }

            return namedArgumentType;
        }

        protected virtual MethodSymbol BindMetaDecorationConstructor(
            MetaDecorationSyntax node,
            NamedTypeSymbol metaDecorationType,
            AnalyzedArguments boundConstructorArguments,
            DiagnosticBag diagnostics,
            ref LookupResultKind resultKind,
            bool suppressErrors,
            ref HashSet<DiagnosticInfo> useSiteDiagnostics)
        {
            MemberResolutionResult<MethodSymbol> memberResolutionResult;
            ImmutableArray<MethodSymbol> candidateConstructors;
            if (!TryPerformConstructorOverloadResolution(
                metaDecorationType,
                boundConstructorArguments,
                metaDecorationType.Name,
                node.Location,
                suppressErrors, //don't cascade in these cases
                diagnostics,
                out memberResolutionResult,
                out candidateConstructors,
                allowProtectedConstructorsOfBaseType: true))
            {
                resultKind = resultKind.WorseResultKind(
                    memberResolutionResult.IsValid && !IsConstructorAccessible(memberResolutionResult.Member, ref useSiteDiagnostics) ?
                        LookupResultKind.Inaccessible :
                        LookupResultKind.OverloadResolutionFailure);
            }

            return memberResolutionResult.Member;
        }

        /// <summary>
        /// Gets the rewritten decorator/metaclass constructor arguments, i.e. the arguments
        /// are in the order of parameters, which may differ from the source
        /// if named constructor arguments are used.
        /// 
        /// For example:
        ///     void Foo(int x, int y, int z, int w = 3);
        /// 
        ///     Foo(0, z: 2, y: 1);
        ///     
        ///     Arguments returned: 0, 1, 2, 3
        /// </summary>
        /// <returns>Rewritten decorator/metaclass constructor arguments</returns>
        /// <remarks>
        /// CONSIDER: Can we share some code with call rewriting in the local rewriter?
        /// </remarks>
        private ImmutableArray<BoundExpression> GetRewrittenMetaDecorationConstructorArguments(
            out ImmutableArray<int> constructorArgumentsSourceIndices,
            MethodSymbol metaDecorationConstructor,
            ImmutableArray<BoundExpression> constructorArgsArray,
            ImmutableArray<string> constructorArgumentNamesOpt,
            MetaDecorationSyntax syntax,
            DiagnosticBag diagnostics,
            ref bool hasErrors)
        {
            Debug.Assert((object)metaDecorationConstructor != null);
            Debug.Assert(!constructorArgsArray.IsDefault);
            Debug.Assert(!hasErrors);

            int argumentsCount = constructorArgsArray.Length;

            // argsConsumedCount keeps track of the number of constructor arguments
            // consumed from this.ConstructorArguments array
            int argsConsumedCount = 0;

            bool hasNamedCtorArguments = !constructorArgumentNamesOpt.IsDefault;
            Debug.Assert(!hasNamedCtorArguments ||
                constructorArgumentNamesOpt.Length == argumentsCount);

            // index of the first named constructor argument
            int firstNamedArgIndex = -1;

            ImmutableArray<ParameterSymbol> parameters = metaDecorationConstructor.Parameters;
            int parameterCount = parameters.Length;

            var reorderedArguments = new BoundExpression[parameterCount];
            int[] sourceIndices = null;

            for (int i = 0; i < parameterCount; i++)
            {
                Debug.Assert(argsConsumedCount <= argumentsCount);

                ParameterSymbol parameter = parameters[i];
                BoundExpression reorderedArgument;

                if (parameter.IsParams && parameter.Type.IsSZArray() && i + 1 == parameterCount)
                {
                    reorderedArgument = GetParamArrayArgument(parameter, constructorArgsArray, argumentsCount, argsConsumedCount, this.Conversions);
                    sourceIndices = sourceIndices ?? CreateSourceIndicesArray(i, parameterCount);
                }
                else if (argsConsumedCount < argumentsCount)
                {
                    if (!hasNamedCtorArguments ||
                        constructorArgumentNamesOpt[argsConsumedCount] == null)
                    {
                        // positional constructor argument
                        reorderedArgument = constructorArgsArray[argsConsumedCount];
                        if (sourceIndices != null)
                        {
                            sourceIndices[i] = argsConsumedCount;
                        }
                        argsConsumedCount++;
                    }
                    else
                    {
                        // named constructor argument

                        // Store the index of the first named constructor argument
                        if (firstNamedArgIndex == -1)
                        {
                            firstNamedArgIndex = argsConsumedCount;
                        }

                        // Current parameter must either have a matching named argument or a default value
                        // For the former case, argsConsumedCount must be incremented to note that we have
                        // consumed a named argument. For the latter case, argsConsumedCount stays same.
                        int matchingArgumentIndex;
                        reorderedArgument = GetMatchingNamedOrOptionalConstructorArgument(out matchingArgumentIndex, constructorArgsArray,
                            constructorArgumentNamesOpt, parameter, firstNamedArgIndex, argumentsCount, ref argsConsumedCount, syntax, diagnostics);

                        sourceIndices = sourceIndices ?? CreateSourceIndicesArray(i, parameterCount);
                        sourceIndices[i] = matchingArgumentIndex;
                    }
                }
                else
                {
                    reorderedArgument = GetDefaultValueArgument(parameter, syntax, diagnostics);
                    sourceIndices = sourceIndices ?? CreateSourceIndicesArray(i, parameterCount);
                }

                if (!hasErrors)
                {
                    if (reorderedArgument.HasAnyErrors)
                    {
                        hasErrors = true;
                    }
                    else if (reorderedArgument.Type.TypeKind == TypeKind.Array && parameter.Type.TypeKind == TypeKind.Array && (TypeSymbol)reorderedArgument.Type != parameter.Type)
                    {
                        // NOTE: As in dev11, we don't allow array covariance conversions (presumably, we don't have a way to
                        // represent the conversion in metadata).
                        diagnostics.Add(ErrorCode.ERR_BadMetaDecorationArgument, syntax.Location);
                        hasErrors = true;
                    }
                }

                reorderedArguments[i] = reorderedArgument;
            }

            constructorArgumentsSourceIndices = sourceIndices != null ? sourceIndices.AsImmutableOrNull() : default(ImmutableArray<int>);
            return reorderedArguments.AsImmutableOrNull();
        }

        private BoundExpression GetMatchingNamedOrOptionalConstructorArgument(
            out int matchingArgumentIndex,
            ImmutableArray<BoundExpression> constructorArgsArray,
            ImmutableArray<string> constructorArgumentNamesOpt,
            ParameterSymbol parameter,
            int startIndex,
            int argumentsCount,
            ref int argsConsumedCount,
            MetaDecorationSyntax syntax,
            DiagnosticBag diagnostics)
        {
            int index = GetMatchingNamedConstructorArgumentIndex(parameter.Name, constructorArgumentNamesOpt, startIndex, argumentsCount);

            if (index < argumentsCount)
            {
                // found a matching named argument
                Debug.Assert(index >= startIndex);

                // increment argsConsumedCount
                argsConsumedCount++;
                matchingArgumentIndex = index;
                return constructorArgsArray[index];
            }
            else
            {
                matchingArgumentIndex = -1;
                return GetDefaultValueArgument(parameter, syntax, diagnostics);
            }
        }

        private BoundLiteral GetDefaultValueArgument(ParameterSymbol parameter, MetaDecorationSyntax syntax, DiagnosticBag diagnostics)
        {
            var parameterType = parameter.Type;
            ConstantValue defaultConstantValue = parameter.IsOptional ? parameter.ExplicitDefaultConstantValue : ConstantValue.NotAvailable;

            ConstantValue defaultValue = null;

            if (parameter.IsCallerLineNumber)
            {
                int line = syntax.SyntaxTree.GetDisplayLineNumber(syntax.Name.Span);

                HashSet<DiagnosticInfo> useSiteDiagnostics = null;
                var conversion = Conversions.GetCallerLineNumberConversion(parameterType, ref useSiteDiagnostics);
                diagnostics.Add(syntax, useSiteDiagnostics);

                if (!conversion.IsNumeric && !conversion.IsConstantExpression)
                {
                    parameterType = Compilation.GetSpecialType(SpecialType.System_Int32);
                }
                defaultValue = ConstantValue.Create(line, parameterType.SpecialType);
            }
            else if (parameter.IsCallerFilePath)
            {
                parameterType = Compilation.GetSpecialType(SpecialType.System_String);
                defaultValue = ConstantValue.Create(syntax.SyntaxTree.GetDisplayPath(syntax.Name.Span, Compilation.Options.SourceReferenceResolver));
            }
            else if (parameter.IsCallerMemberName && this is ContextualDecoratorBinder
                     && (object)((ContextualDecoratorBinder)this).DecoratedMember != null)
            {
                parameterType = Compilation.GetSpecialType(SpecialType.System_String);
                defaultValue = ConstantValue.Create(((ContextualDecoratorBinder)this).DecoratedMember.GetMemberCallerName());
            }
            else if (defaultConstantValue == ConstantValue.NotAvailable)
            {
                // There is no constant value given for the parameter in source/metadata.
                // For example, the decorator constructor with signature: M([Optional] int x), has no default value from syntax or decorators.
                // Default value for these cases is "default(parameterType)".
                if (parameterType.SpecialType == SpecialType.System_Object)
                {
                    // CS7359: Decorator constructor parameter '{0}' is optional, but no default parameter value was specified.
                    diagnostics.Add(ErrorCode.ERR_BadMetaDecorationParamDefaultArgument, syntax.Name.Location, parameter.Name);
                    defaultValue = ConstantValue.Bad;
                }
                else
                {
                    defaultValue = parameterType.GetDefaultValue();
                }
            }
            else if (defaultConstantValue.IsBad)
            {
                // Constant value through syntax had errors, don't generate cascading diagnostics.
                defaultValue = ConstantValue.Bad;
            }
            else if (parameterType.SpecialType == SpecialType.System_Object && !defaultConstantValue.IsNull)
            {
                // error CS1763: '{0}' is of type '{1}'. A default parameter value of a reference type other than string can only be initialized with null
                diagnostics.Add(ErrorCode.ERR_NotNullRefDefaultParameter, syntax.Location, parameter.Name, parameterType);
                defaultValue = ConstantValue.Bad;
            }
            else
            {
                defaultValue = defaultConstantValue;
            }

            return new BoundLiteral(syntax, defaultValue, parameterType, defaultValue.IsBad);
        }

        private static BoundExpression GetParamArrayArgument(
            ParameterSymbol parameter,
            ImmutableArray<BoundExpression> constructorArgsArray,
            int argumentsCount,
            int argsConsumedCount,
            Conversions conversions)
        {
            Debug.Assert(argsConsumedCount <= argumentsCount);

            int paramArrayArgCount = argumentsCount - argsConsumedCount;
            if (paramArrayArgCount == 0)
            {
                return new BoundArrayCreation(
                    null,
                    ImmutableArray.Create<BoundExpression>(
                        new BoundLiteral(null, ConstantValue.Create(0, SpecialType.System_Int32), parameter.DeclaringCompilation.GetSpecialType(SpecialType.System_Int32))
                    ),
                    null,
                    parameter.Type);
            }

            // If there's exactly one argument and it's an array of an appropriate type, then just return it.
            if (paramArrayArgCount == 1 && constructorArgsArray[argsConsumedCount].Type.IsArray())
            {
                TypeSymbol argumentType = constructorArgsArray[argsConsumedCount].Type;

                // Easy out (i.e. don't both classifying conversion).
                if (argumentType == parameter.Type)
                {
                    return constructorArgsArray[argsConsumedCount];
                }

                HashSet<DiagnosticInfo> useSiteDiagnostics = null; // ignoring, since already bound argument and parameter
                Conversion conversion = conversions.ClassifyConversion(argumentType, parameter.Type, ref useSiteDiagnostics, builtinOnly: true);

                // NOTE: Won't always succeed, even though we've performed overload resolution.
                // For example, passing int[] to params object[] actually treats the int[] as an element of the object[].
                if (conversion.IsValid && conversion.Kind == ConversionKind.ImplicitReference)
                {
                    return constructorArgsArray[argsConsumedCount];
                }
            }

            Debug.Assert(!constructorArgsArray.IsDefault);
            Debug.Assert(argsConsumedCount <= constructorArgsArray.Length);

            var arguments = new BoundExpression[paramArrayArgCount];

            for (int i = 0; i < paramArrayArgCount; i++)
            {
                arguments[i] = constructorArgsArray[argsConsumedCount++];
            }

            return new BoundArrayCreation(
                null,
                ImmutableArray.Create<BoundExpression>(
                    new BoundLiteral(null, ConstantValue.Create(0, SpecialType.System_Int32), parameter.DeclaringCompilation.GetSpecialType(SpecialType.System_Int32))
                ),
                new BoundArrayInitialization(null, arguments.ToImmutableArray()),
                parameter.Type);
        }

        public ImmutableArray<KeyValuePair<string, BoundExpression>> UnpackNamedArguments(ImmutableArray<BoundExpression> arguments, DiagnosticBag diagnostics, ref bool attrHasErrors)
        {
            ArrayBuilder<KeyValuePair<string, BoundExpression>> builder = null;
            foreach (var argument in arguments)
            {
                var kv = UnpackNamedArgument(argument, diagnostics, ref attrHasErrors);

                if (kv.HasValue)
                {
                    if (builder == null)
                    {
                        builder = ArrayBuilder<KeyValuePair<string, BoundExpression>>.GetInstance();
                    }

                    builder.Add(kv.Value);
                }
            }

            if (builder == null)
            {
                return ImmutableArray<KeyValuePair<string, BoundExpression>>.Empty;
            }

            return builder.ToImmutableAndFree();
        }

        private KeyValuePair<string, BoundExpression>? UnpackNamedArgument(BoundExpression argument, DiagnosticBag diagnostics, ref bool attrHasErrors)
        {
            switch (argument.Kind)
            {
                case BoundKind.AssignmentOperator:
                    var assignment = (BoundAssignmentOperator)argument;

                    switch (assignment.Left.Kind)
                    {
                        case BoundKind.FieldAccess:
                            var fa = (BoundFieldAccess)assignment.Left;
                            return new KeyValuePair<string, BoundExpression>(fa.FieldSymbol.Name, assignment.Right);

                        case BoundKind.PropertyAccess:
                            var pa = (BoundPropertyAccess)assignment.Left;
                            return new KeyValuePair<string, BoundExpression>(pa.PropertySymbol.Name, assignment.Right);
                    }

                    break;
            }

            return null;
        }

        #endregion

        #region AnalyzedMetaDecorationArguments

        private struct AnalyzedMetaDecorationArguments
        {
            internal readonly AnalyzedArguments ConstructorArguments;
            internal readonly ImmutableArray<BoundExpression> NamedArguments;

            internal AnalyzedMetaDecorationArguments(AnalyzedArguments constructorArguments, ImmutableArray<BoundExpression> namedArguments)
            {
                this.ConstructorArguments = constructorArguments;
                this.NamedArguments = namedArguments;
            }
        }

        #endregion
    }
}
