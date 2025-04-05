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
            int newValue = intField + intStep;
            if (newValue <= ((int)(object)range.Max))
            {
                field = (T)(object)newValue;
                return true;
            }
        }
        else if (field is double doubleField && step is double doubleStep)
        {
            double newValue = doubleField + doubleStep;
            // Use epsilon comparison for double to avoid floating point issues
            if (newValue <= ((double)(object)range.Max) + 1e-10)
            {
                field = (T)(object)newValue;
                return true;
            }
        }
        return false;
    }

    // Utility method to calculate the number of steps in a range
    protected static int CalculateSteps<T>(T min, T max, T step) where T : IComparable
    {
        if (min is int intMin && max is int intMax && step is int intStep)
        {
            return (intMax - intMin) / intStep + 1;
        }
        else if (min is double doubleMin && max is double doubleMax && step is double doubleStep)
        {
            // Use Math.Round to handle floating point precision issues
            return (int)Math.Round((doubleMax - doubleMin) / doubleStep) + 1;
        }
        throw new ArgumentException("Unsupported type for step calculation");
    }

    public abstract void IncrementSingle();
    public abstract bool CanIncrement();
    public abstract IIndicatorParameter DeepClone();
    public abstract List<ParameterDescriptor> GetProperties();
    public abstract IIndicatorParameter GetRandomNeighbor(Random random);
    public abstract void UpdateFromDescriptor(ParameterDescriptor descriptor);
    public abstract int GetPermutationCount();
    public abstract void Reset();

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