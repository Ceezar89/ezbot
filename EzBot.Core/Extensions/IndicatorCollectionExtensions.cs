using EzBot.Core.Indicator;
using EzBot.Models;
using System.Reflection;

namespace EzBot.Core.Extensions;

public static class IndicatorCollectionExtensions
{
    public static void FromDto(this IndicatorCollection collection, IndicatorParameterDto[] dtos)
    {
        try
        {
            // Process each DTO to create or update indicators
            foreach (var dto in dtos)
            {
                // Find the indicator type by name
                var indicatorType = Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .FirstOrDefault(t => t.Name == dto.IndicatorType && typeof(IIndicator).IsAssignableFrom(t));

                if (indicatorType == null)
                {
                    Console.WriteLine($"Warning: Indicator type '{dto.IndicatorType}' not found.");
                    continue;
                }

                // Find existing indicator of the same type or create a new one
                var existingIndicator = collection.FirstOrDefault(i => i.GetType().Name == dto.IndicatorType);

                if (existingIndicator != null)
                {
                    // Update existing indicator parameters
                    var parameters = existingIndicator.GetParameters();
                    var paramType = parameters.GetType();

                    foreach (var param in dto.Parameters)
                    {
                        try
                        {
                            // Get the property by name
                            var property = paramType.GetProperty(param.Key);
                            if (property != null && property.CanWrite)
                            {
                                object typedValue;

                                // Handle special conversions based on property type
                                if (property.PropertyType == typeof(int) && param.Value is long longValue)
                                {
                                    typedValue = (int)longValue;
                                }
                                else if (property.PropertyType == typeof(double) && param.Value is long or int)
                                {
                                    typedValue = Convert.ToDouble(param.Value);
                                }
                                else
                                {
                                    // Standard conversion
                                    typedValue = Convert.ChangeType(param.Value, property.PropertyType);
                                }

                                // Set the property value
                                property.SetValue(parameters, typedValue);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error setting parameter '{param.Key}': {ex.Message}");
                        }
                    }

                    // Update the indicator with new parameters
                    existingIndicator.UpdateParameters(parameters);
                }
                else
                {
                    Console.WriteLine($"Warning: No existing indicator found for type '{dto.IndicatorType}'.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading parameters from DTO: {ex.Message}");
        }
    }
}
