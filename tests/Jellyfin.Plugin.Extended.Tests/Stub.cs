using System;
using System.Reflection;

namespace Jellyfin.Plugin.Extended.Tests;

/// <summary>
/// Stands in for a large server interface: only the members a test answers do anything.
/// </summary>
public class Stub<T> : DispatchProxy
    where T : class
{
    private Func<MethodInfo, object?[]?, object?> _answer = (_, _) => null;

    public static T Create(Func<MethodInfo, object?[]?, object?> answer)
    {
        var stub = DispatchProxy.Create<T, Stub<T>>();
        ((Stub<T>)(object)stub)._answer = answer;
        return stub;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var returnType = targetMethod!.ReturnType;
        return _answer(targetMethod, args)
            ?? (returnType.IsValueType && returnType != typeof(void) ? Activator.CreateInstance(returnType) : null);
    }
}
