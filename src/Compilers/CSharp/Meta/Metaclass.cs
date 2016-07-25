﻿// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;

namespace CSharp.Meta
{
    public abstract class Metaclass
    {
        public abstract void ApplyToType(Type type);
    }
}