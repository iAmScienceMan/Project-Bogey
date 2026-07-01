using System;

namespace Bogey.Sim.Engine;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class DependencyAttribute : Attribute
{

}
