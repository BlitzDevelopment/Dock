using System.Collections.Generic;
using System;
using System.Linq;

public class EventAggregator
{
    private static readonly Lazy<EventAggregator> _instance = new(() => new EventAggregator());
    public static EventAggregator Instance => _instance.Value;

    private readonly Dictionary<Type, List<Delegate>> _subscriptions = new();

    // Private constructor to prevent instantiation from outside
    private EventAggregator() { }

    public void Subscribe<TEvent>(Action<TEvent> action)
    {
        var eventType = typeof(TEvent);
        if (!_subscriptions.ContainsKey(eventType))
        {
            _subscriptions[eventType] = new List<Delegate>();
        }
        _subscriptions[eventType].Add(action);
    }

    public void Publish<TEvent>(TEvent eventToPublish)
    {
        var eventType = typeof(TEvent);
        if (_subscriptions.ContainsKey(eventType))
        {
            foreach (var action in _subscriptions[eventType].OfType<Action<TEvent>>())
            {
                action(eventToPublish);
            }
        }
    }
}