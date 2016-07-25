// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp.Symbols.Meta
{
    internal class DecoratorTypingResult
    {
        private readonly bool _isSuccessful;
        private readonly ExtendedTypeInfo _type;
        private readonly ImmutableHashSet<SubtypingAssertion> _updatedSubtypingAssertions;
        private readonly ImmutableHashSet<SubtypingAssertion> _assertionsIfTrue;
        private readonly ImmutableHashSet<SubtypingAssertion> _assertionsIfFalse;

        public bool IsSuccessful
        {
            get { return _isSuccessful; }
        }

        public ExtendedTypeInfo Type
        {
            get { return _type; }
        }

        public ImmutableHashSet<SubtypingAssertion> UpdatedSubtypingAssertions
        {
            get { return _updatedSubtypingAssertions; }
        }

        public ImmutableHashSet<SubtypingAssertion> AssertionsIfTrue
        {
            get { return _assertionsIfTrue; }
        }

        public ImmutableHashSet<SubtypingAssertion> AssertionsIfFalse
        {
            get { return _assertionsIfFalse; }
        }

        public DecoratorTypingResult(
            bool isSuccessful,
            ExtendedTypeInfo type,
            ImmutableHashSet<SubtypingAssertion> updatedSubtypingAssertions,
            ImmutableHashSet<SubtypingAssertion> assertionsIfTrue = default(ImmutableHashSet<SubtypingAssertion>),
            ImmutableHashSet<SubtypingAssertion> assertionsIfFalse = default(ImmutableHashSet<SubtypingAssertion>))
        {
            // Only the type-checking of boolean expressions should have assertions
            if (assertionsIfTrue != null || assertionsIfFalse != null)
            {
                Debug.Assert(type.IsOrdinaryType && type.OrdinaryType.SpecialType == SpecialType.System_Boolean);
            }

            _isSuccessful = isSuccessful;
            _type = type;
            _updatedSubtypingAssertions = updatedSubtypingAssertions;
            _assertionsIfTrue = assertionsIfTrue;
            _assertionsIfFalse = assertionsIfFalse;
        }

        public DecoratorTypingResult WithIsSuccessful(bool isSuccessful)
        {
            if (IsSuccessful == isSuccessful)
            {
                return this;
            }
            else
            {
                return new DecoratorTypingResult(isSuccessful, Type, UpdatedSubtypingAssertions, AssertionsIfTrue, AssertionsIfFalse);
            }
        }

        public DecoratorTypingResult WithType(ExtendedTypeInfo type)
        {
            if (Type == type)
            {
                return this;
            }
            else
            {
                return new DecoratorTypingResult(IsSuccessful, type, UpdatedSubtypingAssertions, AssertionsIfTrue, AssertionsIfFalse);
            }
        }

        public DecoratorTypingResult WithUpdatedSubtypingAssertions(ImmutableHashSet<SubtypingAssertion> updatedSubtypingAssertions)
        {
            if (UpdatedSubtypingAssertions == updatedSubtypingAssertions)
            {
                return this;
            }
            else
            {
                return new DecoratorTypingResult(IsSuccessful, Type, updatedSubtypingAssertions, AssertionsIfTrue, AssertionsIfFalse);
            }
        }

        public DecoratorTypingResult Update(bool isSuccessful, ExtendedTypeInfo type, ImmutableHashSet<SubtypingAssertion> updatedSubtypingAssertions)
        {
            if (IsSuccessful == isSuccessful
                && Type == type
                && UpdatedSubtypingAssertions == updatedSubtypingAssertions)
            {
                return this;
            }
            else
            {
                return new DecoratorTypingResult(isSuccessful, type, updatedSubtypingAssertions, AssertionsIfTrue, AssertionsIfFalse);
            }
        }
    }
}
