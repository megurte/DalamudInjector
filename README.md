# DalamudInjector

**DalamudInjector** is a lightweight dependency injection (DI) container designed for use in Dalamud plugins. It allows automatic resolution of class dependencies, service registration, and safe disposal of plugin resources.

## Features

- **Constructor injection**: Automatically creates instances with resolved dependencies.
- **Service registration**: Supports both manual and Dalamud service registration.
- **Lifecycle control**: Disposable components can implement `IPluginComponent` to clean up on plugin unload.
- **Initialization support** via `IInitializable`.
- **Simple API**.

## How It Works

### ComponentContainer

The core container. It handles binding, resolving, and lifecycle management of components:

```c#
public interface IComponentContainer : IDisposable
{
    T BindInstance<T>(T instance);
    void Bind<T>() where T : class;
    T Resolve<T>();
}
```
Use `BindInstance<T>()` to register an existing instance, or `Bind<T>()` to create and register it automatically via its constructor.

---

### Component Lifecycle
Any class that implements:

```c#
public interface IPluginComponent
{
    void Release();
}
```
Will have its `Release()` method called during `Dispose()` of the container. This ensures clean unsubscription from Dalamud events or unmanaged resources.

Optionally, classes may also implement:
```c#
public interface IInitializable
{
    void Initialize();
}
```
This will trigger `Initialize()` right after the object is constructed and dependencies are injected.

---

### How to Start
1. Make installer class to register all dalamud services you require 
```c#
public class ServiceInstaller
{
    public readonly ServiceManager Service;

    public ServiceInstaller(IDalamudPluginInterface pluginInterface)
    {
        Service = new ServiceManager(); // Automaticly create DI container inside 
        Service.AddExistingService(pluginInterface);
        Service.AddExistingService(pluginInterface.UiBuilder);
        Service.AddDalamudService<IChatGui>(pluginInterface);
        Service.AddDalamudService<IDataManager>(pluginInterface);
        Service.AddDalamudService<ICommandManager>(pluginInterface);
        Service.AddDalamudService<ITextureProvider>(pluginInterface);
        ...
        Service.CreateProvider(); // Finish setup
    }
}
```
2. Create installer and bind to the `_container` in your plugin's main class
```c#
public class YourPluginMain : IDalamudPlugin
{
    private readonly ComponentContainer _container;

    public YourPluginMain(IDalamudPluginInterface pluginInterface)
    {
        var config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration(pluginInterface);
        var installer = new ServiceInstaller(pluginInterface);
        var service = installer.Service;

        _container = service.Container;
        _container.BindInstance(this);
        _container.BindInstance(pluginInterface);
        _container.BindInstance(config);

        // Register plugin systems
        _container.Bind<BattleStatsManager>();
        _container.Bind<BattleStatsHandler>();
        _container.Bind<EnemyInfo>();
        ...
    }

    public void Dispose()
    {
        _container.Dispose(); // Safely disposes IPluginComponent implementations
    }
}
```
Now all the classes `BattleStatsManager`, `BattleStatsHandler`, `EnemyInfo` and etc. gain it's dependencies via constructor

---

### Plugin component code example
```c#
public class YourPluginWindow : IPluginComponent, IInitializable
{
    private readonly IDataManager _dataManager;

    public YourPluginWindow(IDataManager dataManager)
    {
        _dataManager = dataManager;
    }

    public void Initialize()
    {
        // Setup logic if needed
    }

    public void Release()
    {
        // Cleanup, unsubscribe events, etc.
    }
}
```
