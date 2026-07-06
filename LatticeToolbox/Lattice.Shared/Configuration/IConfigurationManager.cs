using System;
using System.Collections.Generic;

namespace Lattice.Shared.Configuration;

public interface IConfigurationManager
{
    IReadOnlyCollection<CVarDef> Definitions { get; }

    void RegisterCVars(Type holder);

    T GetCVar<T>(CVarDef<T> cVar)
        where T : notnull;

    void SetCVar<T>(CVarDef<T> cVar, T value)
        where T : notnull;

    bool TrySetCVar(string name, string value, out string? error);

    string? GetCVarString(string name);

    bool IsRegistered(string name);

    void OnValueChanged<T>(CVarDef<T> cVar, Action<T> handler, bool invokeImmediately = false)
        where T : notnull;

    void ResetToDefault(CVarDef cVar);
}
