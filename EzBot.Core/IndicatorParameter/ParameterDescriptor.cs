
namespace EzBot.Core.IndicatorParameter;

public class ParameterDescriptor(object value, object min, object max, object step, string name)
{
    public object Value { get; set; } = value;
    public object Min { get; set; } = min;
    public object Max { get; set; } = max;
    public object Step { get; set; } = step;
    public string Name { get; set; } = name;
}
