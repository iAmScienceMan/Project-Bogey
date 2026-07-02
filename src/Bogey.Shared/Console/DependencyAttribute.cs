using System;

namespace Bogey.Shared.Console;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class DependencyAttribute : Attribute
{
}
