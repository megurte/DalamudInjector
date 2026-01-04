using System;

namespace WhichMount.ComponentInjector;

[AttributeUsage(AttributeTargets.Class)]
public sealed class InjectFields : Attribute {}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class InjectAttribute : Attribute {}
