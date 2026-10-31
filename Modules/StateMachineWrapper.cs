
using System;

namespace TONE.Modules;

public class StateMachineWrapper<T> : CompilerGeneratedObjectWrapper
{

    private readonly PropertyInfo _thisProperty;
    private readonly PropertyInfo _stateProperty;

    private T _parentInstance;

    public T Instance => _parentInstance ??= (T)_thisProperty.GetValue(GeneratedObject)!;

    public int State
    {
        get => (int)_stateProperty.GetValue(GeneratedObject)!;
        set => _stateProperty.SetValue(GeneratedObject, value);
    }

    public StateMachineWrapper(object stateMachine) : base(stateMachine)
    {
        _thisProperty = AccessTools.Property(GeneratedType, "__4__this");
        _stateProperty = AccessTools.Property(GeneratedType, "__1__state");

        if (_thisProperty == null || _stateProperty == null)
        {
            throw new MissingMemberException($"Could not find required properties in type '{GeneratedType}'.");
        }
    }

    public TField GetParameter<TField>(string parameterName)
    {
        return GetField<TField>(parameterName);
    }

    public void SetParameter<TField>(string parameterName, TField value)
    {
        SetField(parameterName, value);
    }
}
