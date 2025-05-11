namespace EzBot.Core.IndicatorParameter;

public class McGinleyParameter : IndicatorParameterBase
{
    private int _period = 14;

    // Ranges
    private static readonly (int Min, int Max) PeriodRange = (10, 100);

    // Steps
    private const int PeriodRangeStep = 10;

    // Calculate permutations
    private static readonly int PeriodPermutations = CalculateSteps(PeriodRange.Min, PeriodRange.Max, PeriodRangeStep);

    public static readonly int TotalPermutations = PeriodPermutations;

    // Type identifier for McGinleyDynamicParameter (unique across all parameter types)
    private const byte TYPE_ID = 0x05;

    protected override byte GetTypeIdentifier() => TYPE_ID;

    protected override byte[] GetParameterSpecificBinaryData()
    {
        byte[] data = new byte[4]; // 4 bytes for Period

        // Store Period (4 bytes)
        BitConverter.GetBytes(_period).CopyTo(data, 0);

        return data;
    }

    /// <summary>
    /// Updates the instance fields from the parameter-specific binary data.
    /// </summary>
    /// <param name="data">Byte array containing only the specific data for this parameter type.</param>
    /// <exception cref="ArgumentException">Thrown if data length is incorrect.</exception>
    protected override void UpdateFromParameterSpecificBinaryData(byte[] data)
    {
        if (data.Length != 4) // Ensure the data length matches serialization format
            throw new ArgumentException("Invalid data length for McGinleyDynamicParameter update", nameof(data));

        // Update field using property to leverage validation logic
        Period = BitConverter.ToInt32(data, 0);
    }

    // Static method to create McGinleyDynamicParameter from binary data
    public static McGinleyParameter FromBinary(string name, byte[] data)
    {
        if (data.Length != 4)
            throw new ArgumentException("Invalid data length for McGinleyDynamicParameter");

        int period = BitConverter.ToInt32(data, 0);

        // Name is set by the base constructor via the 'name' parameter passed to this method
        var param = new McGinleyParameter(period);
        return param;
    }

    // Register the type with the factory method
    static McGinleyParameter()
    {
        // This will be called when the class is first used
        RegisterType();
    }

    private static void RegisterType()
    {
        // Register this type with the factory
        IndicatorParameterBase.RegisterParameterType(TYPE_ID, (name, data) => McGinleyParameter.FromBinary(name, data));
    }

    public override int GetPermutationCount() => TotalPermutations;

    public override List<ParameterDescriptor> GetProperties()
    {
        return [
            new ParameterDescriptor(_period, PeriodRange.Min, PeriodRange.Max, PeriodRangeStep, "Period")
        ];
    }

    public override void UpdateFromDescriptor(ParameterDescriptor descriptor)
    {
        switch (descriptor.Name)
        {
            case "Period":
                Period = (int)descriptor.Value;
                break;
        }
    }

    public int Period
    {
        get => _period;
        set => ValidateAndSetValue(ref _period, value, PeriodRange);
    }

    public McGinleyParameter() : base("mcginley")
    {
        Period = PeriodRange.Min;
    }

    public McGinleyParameter(int period) : base("mcginley")
    {
        Period = period;
    }

    public override bool IncrementSingle()
    {
        if (IncrementValue(ref _period, PeriodRangeStep, PeriodRange))
            return true;

        return false;
    }

    public override McGinleyParameter DeepClone()
    {
        return new McGinleyParameter(Period);
    }

    public override McGinleyParameter GetRandomNeighbor(Random random)
    {
        // Calculate how many steps are possible in the range
        int periodSteps = (PeriodRange.Max - PeriodRange.Min) / PeriodRangeStep + 1;

        // Choose a random step
        var period = PeriodRange.Min + (random.Next(periodSteps) * PeriodRangeStep);

        return new McGinleyParameter(period);
    }

    public override void Reset()
    {
        Period = PeriodRange.Min;
    }

    protected override int GetAdditionalHashCodeComponents()
    {
        return Period.GetHashCode();
    }
}