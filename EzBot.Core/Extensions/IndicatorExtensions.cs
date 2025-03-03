using EzBot.Core.Indicator;
using EzBot.Models;
using System.Reflection;

namespace EzBot.Core.Extensions;

public static class IndicatorExtensions
{
    public static IndicatorParameterDto[] ToDto(this IndicatorCollection collection)
    {
        var dtos = new List<IndicatorParameterDto>();

        foreach (var indicator in collection)
        {
            var parameters = indicator.GetParameters();
            var parameterDict = new Dictionary<string, object>();

            // Use reflection to extract parameter values
            var props = parameters.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props.Where(p => p.Name != "Name"))
            {
                parameterDict[prop.Name] = prop.GetValue(parameters) ?? DBNull.Value;
            }

            dtos.Add(new IndicatorParameterDto
            {
                IndicatorType = indicator.GetType().Name,
                Parameters = parameterDict
            });
        }

        return [.. dtos];
    }
}