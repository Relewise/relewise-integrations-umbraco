using Microsoft.AspNetCore.Http.Features;

namespace Relewise.Integrations.Umbraco.Tests.Tracking;

internal sealed class ResponseCompletion : HttpResponseFeature
{
    private readonly Stack<(Func<object, Task> Callback, object State)> _callbacks = new();

    public override void OnCompleted(Func<object, Task> callback, object state)
    {
        _callbacks.Push((callback, state));
    }

    public async Task CompleteAsync()
    {
        while (_callbacks.TryPop(out (Func<object, Task> Callback, object State) item))
        {
            await item.Callback(item.State);
        }
    }
}
