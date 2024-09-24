using System;
using System.Collections.Generic;

namespace Multiplayer.Networking.Data;

public class TrackedValue<T>
{
    private T lastSentValue;
    private Func<T> valueGetter;
    private Action<T> valueSetter;
    public string Key { get; }

    public TrackedValue(string key, Func<T> valueGetter, Action<T> valueSetter)
    {
        Key = key;
        this.valueGetter = valueGetter;
        this.valueSetter = valueSetter;
        lastSentValue = valueGetter();
    }

    public bool IsDirty => !EqualityComparer<T>.Default.Equals(CurrentValue, lastSentValue);

    public T CurrentValue
    {
        get => valueGetter();
        set
        {
            valueSetter(value);
            lastSentValue = value;
        }
    }

    public void MarkClean()
    {
        lastSentValue = CurrentValue;
    }

    public object GetValueAsObject() => CurrentValue;

    public void SetValueFromObject(object value)
    {
        if (value is T typedValue)
        {
            CurrentValue = typedValue;
        }
        else
        {
            throw new ArgumentException($"Value type mismatch. Expected {typeof(T)}, got {value.GetType()}");
        }
    }

    public string GetDebugString()
    {
        return $"{Key}: {lastSentValue} -> {CurrentValue}";
    }

}
