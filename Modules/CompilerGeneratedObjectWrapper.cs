
using System;

namespace TONE.Modules;

public class CompilerGeneratedObjectWrapper
{

    public object GeneratedObject { get; }

    protected Type GeneratedType { get; }

    protected Dictionary<string, PropertyInfo> PropertyCache { get; }

    public CompilerGeneratedObjectWrapper(object generatedObject)
    {
        GeneratedObject = generatedObject;
        GeneratedType = generatedObject.GetType();

        PropertyCache = [];
    }

    public TField GetField<TField>(string fieldName)
    {
        if (!PropertyCache.TryGetValue(fieldName, out var propertyInfo))
        {
            propertyInfo = AccessTools.Property(GeneratedType, fieldName);
            if (propertyInfo == null)
            {
                throw new MissingMemberException($"Could not find field '{fieldName}' in type '{GeneratedType}'.");
            }
            PropertyCache[fieldName] = propertyInfo;
        }

        return (TField)propertyInfo.GetValue(GeneratedObject)!;
    }

    public void SetField<TField>(string fieldName, TField value)
    {
        if (!PropertyCache.TryGetValue(fieldName, out var propertyInfo))
        {
            propertyInfo = AccessTools.Property(GeneratedType, fieldName);
            if (propertyInfo == null)
            {
                throw new MissingMemberException($"Could not find field '{fieldName}' in type '{GeneratedType}'.");
            }
            PropertyCache[fieldName] = propertyInfo;
        }

        propertyInfo.SetValue(GeneratedObject, value);
    }
}