using Umbraco.Cms.Core.Scoping;

namespace Relewise.Integrations.Umbraco.Tests.Indexing;

internal sealed class TestScopeContext : IScopeContext
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public int CreatedThreadId { get; } = Environment.CurrentManagedThreadId;
    private readonly Dictionary<string, object?> _items = new();
    private readonly Dictionary<string, Action<bool>> _actions = new();

    public void Enlist(string key, Action<bool> action, int priority = 100)
    {
        _actions.TryAdd(key, action);
    }

    public T? Enlist<T>(string key, Func<T> creator, Action<bool, T?>? action = null, int priority = 100)
    {
        if (!_items.ContainsKey(key))
        {
            T value = creator();

            _items.Add(key, value);

            if (action != null)
            {
                _actions.Add(key, completed => action(completed, value));
            }
        }

        return (T?)_items[key];
    }

    public T? GetEnlisted<T>(string key)
    {
        return _items.TryGetValue(key, out object? value) ? (T?)value : default;
    }

    public void ScopeExit(bool completed)
    {
        foreach (Action<bool> action in _actions.Values)
        {
            action(completed);
        }
    }
}
