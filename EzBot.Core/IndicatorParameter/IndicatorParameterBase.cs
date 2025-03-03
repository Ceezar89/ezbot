
namespace EzBot.Core.IndicatorParameter;

public abstract class IndicatorParameterBase(string name) : IIndicatorParameter
{
    public string Name { get; set; } = name;

    protected static void ValidateAndSetValue<T>(ref T field, T value, (T Min, T Max) range) where T : IComparable<T>
    {
        if (value.CompareTo(range.Min) < 0 || value.CompareTo(range.Max) > 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"Value must be between {range.Min} and {range.Max}");
        }
        field = value;
    }

    protected static bool IncrementValue<T>(ref T field, T step, (T Min, T Max) range) where T : IComparable<T>
    {
        if (field is int intField && step is int intStep)
        {
            if (intField + intStep <= ((int)(object)range.Max))
            {
                field = (T)(object)(intField + intStep);
                return true;
            }
        }
        else if (field is double doubleField && step is double doubleStep)
        {
            if (doubleField + doubleStep <= ((double)(object)range.Max))
            {
                field = (T)(object)(doubleField + doubleStep);
                return true;
            }
        }
        return false;
    }

    public abstract void IncrementSingle();
    public abstract bool CanIncrement();
    public abstract IIndicatorParameter DeepClone();
    public abstract List<ParameterDescriptor> GetProperties();
    public abstract IIndicatorParameter GetRandomNeighbor(Random random);
    public abstract void UpdateFromDescriptor(ParameterDescriptor descriptor);

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, GetAdditionalHashCodeComponents());
    }

    protected abstract int GetAdditionalHashCodeComponents();

    public bool Equals(IIndicatorParameter? other)
    {
        if (other == null || GetType() != other.GetType())
            return false;

        var baseOther = (IndicatorParameterBase)other;
        return Name == baseOther.Name && EqualsCore(other);
    }

    protected abstract bool EqualsCore(IIndicatorParameter other);
}