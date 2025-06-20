using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Dalamud.IoC;
using Dalamud.Plugin;
using WhichMount.ComponentInjector;

namespace DalamudInjector
{
    public class ServiceManager : IDisposable
    {
        private readonly ServiceCollection _collection = new ();
        public ServiceProvider? Provider { get; private set; }
        public ComponentContainer Container { get; private set; }

        public ServiceManager()
        {
            _collection.AddSingleton(this);
            Container = new ComponentContainer();
        }

        public IEnumerable<T> GetServicesImplementing<T>()
        {
            if (Provider == null)
                yield break;

            var type = typeof(T);
            foreach (var typeDescriptor in _collection)
            {
                if (typeDescriptor.Lifetime == ServiceLifetime.Singleton &&
                    type.IsAssignableFrom(typeDescriptor.ServiceType))
                {
                    yield return (T) Provider.GetRequiredService(typeDescriptor.ServiceType);
                }
            }
        }

        public T ServiceInitialise<T>(IDalamudPluginInterface pi) where T : class
        {
            AddDalamudService<T>(pi);
            return GetService<T>();
        }

        public T GetService<T>() where T : class
            => Provider!.GetRequiredService<T>();

        public ServiceProvider CreateProvider()
        {
            if (Provider != null)
                return Provider;

            Provider = _collection.BuildServiceProvider(new ServiceProviderOptions()
            {
                ValidateOnBuild = true,
                ValidateScopes = false,
            });

            return Provider;
        }

        public void EnsureRequiredServices()
        {
            CreateProvider();

            foreach (var service in _collection)
            {
                if (typeof(IRequiredService).IsAssignableFrom(service.ServiceType))
                    Provider!.GetRequiredService(service.ServiceType);
            }
        }

        public ServiceManager AddSingleton<T>()
            => AddSingleton(typeof(T));

        public ServiceManager AddSingleton<T>(Func<IServiceProvider, T> factory) where T : class
        {
            _collection.AddSingleton<T>(Func);
            return this;

            T Func(IServiceProvider p)
            {
                return factory(p);
            }
        }

        public void AddIServices(Assembly assembly)
        {
            var iType = typeof(IService);
            foreach (var type in assembly.ExportedTypes.Where(t =>
                         t is {IsInterface: false, IsAbstract: false} && iType.IsAssignableFrom(t)))
            {
                if (_collection.All(t => t.ServiceType != type))
                    AddSingleton(type);
            }
        }

        public ServiceManager AddDalamudService<T>(IDalamudPluginInterface pi) where T : class
        {
            var wrapper = new DalamudServiceWrapper<T>(pi);
            _collection.AddSingleton(wrapper.Service);
            _collection.AddSingleton(pi);
            Container.BindInstance(wrapper.Service);
            return this;
        }

        public ServiceManager AddExistingService<T>(T service) where T : class
        {
            _collection.AddSingleton(service);
            Container.BindInstance(service);
            return this;
        }

        public void Dispose()
        {
            Provider?.Dispose();
            Container.Dispose();
            GC.SuppressFinalize(this);
        }

        private ServiceManager AddSingleton(Type type)
        {
            _collection.AddSingleton(type, Func);
            return this;

            object Func(IServiceProvider p)
            {
                var constructor = type.GetConstructors().MaxBy(c => c.GetParameters().Length);
                if (constructor == null)
                    return Activator.CreateInstance(type) ??
                           throw new Exception($"No constructor available for {type.Name}.");

                var parameterTypes = constructor.GetParameters();
                var parameters = parameterTypes.Select(t => p.GetRequiredService(t.ParameterType)).ToArray();
                return constructor.Invoke(parameters);
            }
        }

        private class DalamudServiceWrapper<T>
        {
            [PluginService] public T Service { get; private set; } = default!;

            public DalamudServiceWrapper(IDalamudPluginInterface pi)
            {
                pi.Inject(this);
            }
        }
    }
}
