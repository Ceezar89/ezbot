using System.Text.Json;
using System.Text.Json.Serialization;
using EzBot.Core.Indicator;
using EzBot.Models;
using System.Reflection;

namespace EzBot.Core.Optimization;

// Class for storing test results with proper serialization support
public class TestResult
{
    // These properties will be serialized to JSON
    public ParameterSet Parameters { get; set; } = null!;
    public BacktestResult Result { get; set; } = null!;

    // Default constructor for serialization
    public TestResult() { }

    // Constructor from tuple
    public TestResult((IndicatorCollection Params, BacktestResult Result) tuple)
    {
        // Extract parameter values from the indicator collection
        Parameters = new ParameterSet(tuple.Params);
        Result = tuple.Result;
    }

    // Create an IndicatorCollection from this TestResult
    public IndicatorCollection ToIndicatorCollection()
    {
        return Parameters.ToIndicatorCollection();
    }
}

// Class to hold serializable parameter values
public class ParameterSet
{
    // Using an array of parameter dictionaries where each element corresponds to an indicator
    [JsonPropertyName("indicators")]
    public Dictionary<string, Dictionary<string, object>> Indicators { get; set; } = [];

    // Default constructor for serialization
    public ParameterSet() { }

    // Constructor from IndicatorCollection
    public ParameterSet(IndicatorCollection collection)
    {
        Indicators = new Dictionary<string, Dictionary<string, object>>();

        try
        {
            // Need to handle each indicator separately
            foreach (var indicator in collection)
            {
                if (indicator == null) continue;

                var indicatorType = indicator.GetType().Name;
                var parameters = indicator.GetParameters();

                if (parameters == null) continue;

                var props = parameters.GetProperties();

                var paramDict = new Dictionary<string, object>();
                foreach (var prop in props)
                {
                    // Use ToString() for serializing non-primitive types 
                    var value = prop.Value;
                    if (value is int or double or string or bool)
                    {
                        paramDict[prop.Name] = value;
                    }
                    else
                    {
                        // For non-basic types, convert to string
                        paramDict[prop.Name] = value?.ToString() ?? "null";
                    }
                }

                if (Indicators.ContainsKey(indicatorType))
                {
                    // Add a unique suffix if duplicate indicator types exist
                    int suffix = 1;
                    while (Indicators.ContainsKey($"{indicatorType}_{suffix}"))
                    {
                        suffix++;
                    }
                    Indicators.Add($"{indicatorType}_{suffix}", paramDict);
                }
                else
                {
                    Indicators.Add(indicatorType, paramDict);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating ParameterSet: {ex.Message}");
        }
    }

    // Convert this ParameterSet back to an IndicatorCollection
    public IndicatorCollection ToIndicatorCollection()
    {
        var collection = new IndicatorCollection();

        foreach (var indicatorEntry in Indicators)
        {
            // Extract indicator type name (remove any suffix like "_1" if present)
            string typeName = indicatorEntry.Key;
            int underscoreIndex = typeName.LastIndexOf('_');
            if (underscoreIndex > 0 && int.TryParse(typeName[(underscoreIndex + 1)..], out _))
            {
                typeName = typeName[..underscoreIndex];
            }

            // Find the indicator type by name
            var indicatorType = FindIndicatorType(typeName);
            if (indicatorType == null)
            {
                Console.WriteLine($"Warning: Could not find indicator type '{typeName}'");
                continue;
            }

            // Create parameters object
            var parameters = CreateParametersObject(indicatorType, indicatorEntry.Value);

            // Create indicator instance
            if (Activator.CreateInstance(indicatorType, parameters) is IIndicator indicator)
            {
                collection.Add(indicator);
            }
            else
            {
                Console.WriteLine($"Warning: Failed to create instance of indicator type '{typeName}'");
            }
        }

        return collection;
    }

    // Find indicator type by name
    private Type? FindIndicatorType(string typeName)
    {
        // Look in all loaded assemblies for the indicator type
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                // Try to find a type that implements IIndicator with the given name
                var type = assembly.GetTypes()
                    .FirstOrDefault(t => t.Name == typeName && typeof(IIndicator).IsAssignableFrom(t));

                if (type != null)
                {
                    return type;
                }
            }
            catch
            {
                // Ignore exceptions from assemblies we can't reflect on
            }
        }

        return null;
    }

    // Create a parameters object for the indicator
    private object CreateParametersObject(Type indicatorType, Dictionary<string, object> paramValues)
    {
        // Find parameter type from constructor
        var constructor = indicatorType.GetConstructors()
            .FirstOrDefault(c => c.GetParameters().Length == 1);

        if (constructor == null)
        {
            throw new InvalidOperationException($"No suitable constructor found for {indicatorType.Name}");
        }

        var paramType = constructor.GetParameters()[0].ParameterType;
        var parameters = Activator.CreateInstance(paramType);

        if (parameters == null)
        {
            throw new InvalidOperationException($"Failed to create parameters instance for {indicatorType.Name}");
        }

        // Set parameter values using reflection
        foreach (var param in paramValues)
        {
            var property = paramType.GetProperty(param.Key);
            if (property != null)
            {
                // Convert parameter value to the property type
                object convertedValue = ConvertParameterValue(param.Value, property.PropertyType);
                property.SetValue(parameters, convertedValue);
            }
        }

        return parameters;
    }

    // Convert parameter value to the expected type
    private object ConvertParameterValue(object value, Type targetType)
    {
        try
        {
            if (value == null)
                return targetType.IsValueType ? Activator.CreateInstance(targetType)! : null!;

            if (targetType == value.GetType())
                return value;

            // Handle numeric conversions
            if (value is JsonElement jsonElement)
            {
                return jsonElement.ValueKind switch
                {
                    JsonValueKind.Number when targetType == typeof(int) => jsonElement.GetInt32(),
                    JsonValueKind.Number when targetType == typeof(double) => jsonElement.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String => jsonElement.GetString()!,
                    _ => Convert.ChangeType(jsonElement.ToString(), targetType)
                };
            }

            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            // If conversion fails, return default value
            return targetType.IsValueType ? Activator.CreateInstance(targetType)! : null!;
        }
    }

    // ToString to display parameter values
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("Parameters{");

        int indicatorCount = 0;
        foreach (var indicator in Indicators)
        {
            sb.Append($"{indicator.Key}[");

            int paramCount = 0;
            foreach (var param in indicator.Value)
            {
                sb.Append($"{param.Key}={param.Value}");

                if (++paramCount < indicator.Value.Count)
                    sb.Append(", ");
            }

            sb.Append("]");

            if (++indicatorCount < Indicators.Count)
                sb.Append(", ");
        }

        sb.Append("}");
        return sb.ToString();
    }
}