using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace WhichMount.ComponentInjector;

public interface IComponentContainer : IDisposable
{
    T BindInstance<T>(T instance);
    void Bind<T>() where T : class;
    T Resolve<T>();
}

public class ComponentContainer : IComponentContainer
{
    private readonly Dictionary<Type, object> _instances = new();
    private bool _disposed;

    public T BindInstance<T>(T instance)
    {
        if (instance == null) throw new Exception("Cannot bind null instance");
        _instances[typeof(T)] = instance;
        return instance;
    }
    
    public void Bind<T>() where T : class
    {
        var type = typeof(T);

        if (_instances.ContainsKey(type))
            return;

        var ctor = type
                   .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                   .MaxBy(c => c.GetParameters().Length);

        if (ctor == null)
            throw new InvalidOperationException($"No public constructor found for type {type.Name}");

        var parameters = ctor.GetParameters();
        var args = new object?[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            if (!_instances.TryGetValue(paramType, out var dependency))
            {
                throw new InvalidOperationException($"Missing dependency for {type.Name}: {paramType.Name}");
            }

            args[i] = dependency;
        }

        var instance = (T)Activator.CreateInstance(type, args)!;
        
        if (Attribute.IsDefined(type, typeof(InjectFields)))
            InjectMembers(instance);
        
        _instances[type] = instance;

        if (instance is IInitializable component)
            component.Initialize();
    }
    
    private void InjectMembers(object instance)
    {
        var type = instance.GetType();

        const BindingFlags flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (var f in type.GetFields(flags))
        {
            if (!Attribute.IsDefined(f, typeof(InjectAttribute)))
                continue;

            if (f.IsInitOnly)
                throw new InvalidOperationException($"{type.Name}.{f.Name} is readonly, can't inject.");

            var dep = ResolveByTypeOrThrow(f.FieldType, $"{type.Name}.{f.Name}");
            f.SetValue(instance, dep);
        }

        foreach (var p in type.GetProperties(flags))
        {
            if (!Attribute.IsDefined(p, typeof(InjectAttribute)))
                continue;

            if (!p.CanWrite)
                throw new InvalidOperationException($"{type.Name}.{p.Name} has no setter, can't inject.");

            var dep = ResolveByTypeOrThrow(p.PropertyType, $"{type.Name}.{p.Name}");
            p.SetValue(instance, dep);
        }
    }

    private object ResolveByTypeOrThrow(Type depType, string target)
    {
        if (_instances.TryGetValue(depType, out var dep))
            return dep;

        throw new InvalidOperationException($"Missing dependency for {target}: {depType.Name}");
    }

    public T Resolve<T>()
    {
        return (T)_instances[typeof(T)];
    }
    
    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (var instance in _instances.Values)
        {
            if (instance is IPluginComponent disposable)
            {
                disposable.Release();
            }
        }

        _instances.Clear();
        _disposed = true;
    }
}
