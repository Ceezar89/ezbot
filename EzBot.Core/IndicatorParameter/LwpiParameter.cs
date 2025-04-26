
namespace EzBot.Core.IndicatorParameter;

public class LwpiParameter : IndicatorParameterBase
{
    private int _period = 8;
    private int _smoothingPeriod = 6;

    // Ranges
    private static readonly (int Min, int Max) PeriodRange = (5, 50);
    private static readonly (int Min, int Max) SmoothingPeriodRange = (5, 50);

    // Steps
    private const int PeriodRangeStep = 5;
    private const int SmoothingPeriodRangeStep = 5;

    // Calculate permutations
    private static readonly int PeriodPermutations = CalculateSteps(PeriodRange.Min, PeriodRange.Max, PeriodRangeStep);
    private static readonly int SmoothingPeriodPermutations = CalculateSteps(SmoothingPeriodRange.Min, SmoothingPeriodRange.Max, SmoothingPeriodRangeStep);

    public static readonly int TotalPermutations = PeriodPermutations * SmoothingPeriodPermutations;

    // Type identifier for LwpiParameter (unique across all parameter types)
    private const byte TYPE_ID = 0x04;

    protected override byte GetTypeIdentifier() => TYPE_ID;

    protected override byte[] GetParameterSpecificBinaryData()
    {
        byte[] data = new byte[8]; // 4 bytes for Period, 4 bytes for SmoothingPeriod

        // Store Period (4 bytes)
        BitConverter.GetBytes(_period).CopyTo(data, 0);

        // Store SmoothingPeriod (4 bytes)
        BitConverter.GetBytes(_smoothingPeriod).CopyTo(data, 4);

        return data;
    }

    /// <summary>
    /// Updates the instance fields from the parameter-specific binary data.
    /// </summary>
    /// <param name="data">Byte array containing only the specific data for this parameter type.</param>
    /// <exception cref="ArgumentException">Thrown if data length is incorrect.</exception>
    protected override void UpdateFromParameterSpecificBinaryData(byte[] data)
    {
        if (data.Length != 8) // Ensure the data length matches serialization format
            throw new ArgumentException("Invalid data length for LwpiParameter update", nameof(data));

        // Update fields using properties to leverage validation logic
        Period = BitConverter.ToInt32(data, 0);
        SmoothingPeriod = BitConverter.ToInt32(data, 4);
    }

    // Static method to create LwpiParameter from binary data
    public static LwpiParameter FromBinary(string name, byte[] data)
    {
        if (data.Length != 8)
            throw new ArgumentException("Invalid data length for LwpiParameter");

        int period = BitConverter.ToInt32(data, 0);
        int smoothingPeriod = BitConverter.ToInt32(data, 4);

        var param = new LwpiParameter(period, smoothingPeriod);
        return param;
    }

    // Register the type with the factory method
    static LwpiParameter()
    {
        // This will be called when the class is first used
        RegisterType();
    }

    private static void RegisterType()
    {
        // Register this type with the factory
        IndicatorParameterBase.RegisterParameterType(TYPE_ID, (name, data) => LwpiParameter.FromBinary(name, data));
    }

    public override int GetPermutationCount() => TotalPermutations;

    public override List<ParameterDescriptor> GetProperties()
    {
        return [
            new ParameterDescriptor(_period, PeriodRange.Min, PeriodRange.Max, PeriodRangeStep, "Period"),
            new ParameterDescriptor(_smoothingPeriod, SmoothingPeriodRange.Min, SmoothingPeriodRange.Max, SmoothingPeriodRangeStep, "Smoothing Period")
        ];
    }

    public override void UpdateFromDescriptor(ParameterDescriptor descriptor)
    {
        switch (descriptor.Name)
        {
            case "Period":
                Period = (int)descriptor.Value;
                break;
            case "Smoothing Period":
                SmoothingPeriod = (int)descriptor.Value;
                break;
        }
    }

    public int Period
    {
        get => _period;
        set => ValidateAndSetValue(ref _period, value, PeriodRange);
    }

    public int SmoothingPeriod
    {
        get => _smoothingPeriod;
        set => ValidateAndSetValue(ref _smoothingPeriod, value, SmoothingPeriodRange);
    }

    // Fixed value - not changeable as per requirements
    public string SmoothingType => "SMA";

    public LwpiParameter() : base("lwpi")
    {
        Period = PeriodRange.Min;
        SmoothingPeriod = SmoothingPeriodRange.Min;
    }

    public LwpiParameter(int period, int smoothingPeriod) : base("lwpi")
    {
        Period = period;
        SmoothingPeriod = smoothingPeriod;
    }

    public override bool IncrementSingle()
    {
        if (IncrementValue(ref _smoothingPeriod, SmoothingPeriodRangeStep, SmoothingPeriodRange))
            return true;
        _smoothingPeriod = SmoothingPeriodRange.Min;

        if (IncrementValue(ref _period, PeriodRangeStep, PeriodRange))
            return true;

        return false;
    }

    public override LwpiParameter DeepClone()
    {
        return new LwpiParameter(Period, SmoothingPeriod);
    }

    public override LwpiParameter GetRandomNeighbor(Random random)
    {
        // Calculate how many steps are possible in each range
        int periodSteps = (PeriodRange.Max - PeriodRange.Min) / PeriodRangeStep + 1;
        int smoothingPeriodSteps = (SmoothingPeriodRange.Max - SmoothingPeriodRange.Min) / SmoothingPeriodRangeStep + 1;

        // Choose random steps
        var period = PeriodRange.Min + (random.Next(periodSteps) * PeriodRangeStep);
        var smoothingPeriod = SmoothingPeriodRange.Min + (random.Next(smoothingPeriodSteps) * SmoothingPeriodRangeStep);

        return new LwpiParameter(period, smoothingPeriod);
    }

    public override void Reset()
    {
        Period = PeriodRange.Min;
        SmoothingPeriod = SmoothingPeriodRange.Min;
    }

    protected override int GetAdditionalHashCodeComponents()
    {
        return HashCode.Combine(Period, SmoothingPeriod);
    }
}