using System.Text.Json;
using System.Text.Json.Serialization;
using EzBot.Core.Indicator;
using EzBot.Core.Strategy;
using EzBot.Models;

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

}

// Class to hold serializable parameter values
public class ParameterSet
{
    // Using an array of parameter dictionaries where each element corresponds to an indicator
    [JsonPropertyName("indicators")]
    public Dictionary<string, Dictionary<string, object>> Indicators { get; set; } = new();

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

    // Helper method to convert serialized values to the right type
    private object ConvertToRightType(object value, object originalValue)
    {
        try
        {
            if (originalValue is int)
            {
                if (value is JsonElement jsonElement)
                {
                    if (jsonElement.ValueKind == JsonValueKind.Number)
                        return jsonElement.GetInt32();
                    else
                        return int.Parse(jsonElement.GetString() ?? "0");
                }
                return Convert.ToInt32(value);
            }
            else if (originalValue is double)
            {
                if (value is JsonElement jsonElement)
                {
                    if (jsonElement.ValueKind == JsonValueKind.Number)
                        return jsonElement.GetDouble();
                    else
                        return double.Parse(jsonElement.GetString() ?? "0");
                }
                return Convert.ToDouble(value);
            }
            else if (originalValue is string)
            {
                return value.ToString() ?? "";
            }
            else if (originalValue is bool)
            {
                if (value is JsonElement jsonElement)
                {
                    if (jsonElement.ValueKind == JsonValueKind.True)
                        return true;
                    else if (jsonElement.ValueKind == JsonValueKind.False)
                        return false;
                    else
                        return bool.Parse(jsonElement.GetString() ?? "false");
                }
                return Convert.ToBoolean(value);
            }
        }
        catch
        {
            // If conversion fails, return the original value
            return originalValue;
        }

        // Default to just returning the value as is
        return value;
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