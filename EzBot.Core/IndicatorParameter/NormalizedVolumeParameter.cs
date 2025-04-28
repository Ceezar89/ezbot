namespace EzBot.Core.IndicatorParameter;

public class NormalizedVolumeParameter : IndicatorParameterBase
{
    private int _volumePeriod = 50;
    private int _highVolume = 150;
    private int _lowVolume = 75;
    private int _normalHighVolumeRange = 100;

    // Ranges
    private static readonly (int Min, int Max) VolumePeriodRange = (10, 30);
    private static readonly (int Min, int Max) HighVolumeRange = (70, 90);
    private static readonly (int Min, int Max) LowVolumeRange = (70, 90);
    private static readonly (int Min, int Max) NormalHighVolumeRangeRange = (70, 90);

    // Steps
    private const int VolumePeriodRangeStep = 10;
    private const int HighVolumeRangeStep = 10;
    private const int LowVolumeRangeStep = 10;
    private const int NormalHighVolumeRangeRangeStep = 10;

    // Correctly calculate the number of steps for each parameter range
    private static readonly int VolumePeriodPermutations = CalculateSteps(VolumePeriodRange.Min, VolumePeriodRange.Max, VolumePeriodRangeStep);
    private static readonly int HighVolumePermutations = CalculateSteps(HighVolumeRange.Min, HighVolumeRange.Max, HighVolumeRangeStep);
    private static readonly int LowVolumePermutations = CalculateSteps(LowVolumeRange.Min, LowVolumeRange.Max, LowVolumeRangeStep);
    private static readonly int NormalHighVolumeRangePermutations = CalculateSteps(NormalHighVolumeRangeRange.Min, NormalHighVolumeRangeRange.Max, NormalHighVolumeRangeRangeStep);

    public static readonly int TotalPermutations = VolumePeriodPermutations * HighVolumePermutations * LowVolumePermutations * NormalHighVolumeRangePermutations;

    // Type identifier for NormalizedVolumeParameter (unique across all parameter types)
    private const byte TYPE_ID = 0x06;

    protected override byte GetTypeIdentifier() => TYPE_ID;

    protected override byte[] GetParameterSpecificBinaryData()
    {
        byte[] data = new byte[16]; // 4 bytes for each of the 4 int parameters

        // Store VolumePeriod (4 bytes)
        BitConverter.GetBytes(_volumePeriod).CopyTo(data, 0);

        // Store HighVolume (4 bytes)
        BitConverter.GetBytes(_highVolume).CopyTo(data, 4);

        // Store LowVolume (4 bytes)
        BitConverter.GetBytes(_lowVolume).CopyTo(data, 8);

        // Store NormalHighVolumeRange (4 bytes)
        BitConverter.GetBytes(_normalHighVolumeRange).CopyTo(data, 12);

        return data;
    }

    /// <summary>
    /// Updates the instance fields from the parameter-specific binary data.
    /// </summary>
    /// <param name="data">Byte array containing only the specific data for this parameter type.</param>
    /// <exception cref="ArgumentException">Thrown if data length is incorrect.</exception>
    protected override void UpdateFromParameterSpecificBinaryData(byte[] data)
    {
        if (data.Length != 16) // Ensure the data length matches serialization format
            throw new ArgumentException("Invalid data length for NormalizedVolumeParameter update", nameof(data));

        // Update fields using properties to leverage validation logic
        VolumePeriod = BitConverter.ToInt32(data, 0);
        HighVolume = BitConverter.ToInt32(data, 4);
        LowVolume = BitConverter.ToInt32(data, 8);
        NormalHighVolumeRange = BitConverter.ToInt32(data, 12);
    }

    // Static method to create NormalizedVolumeParameter from binary data
    public static NormalizedVolumeParameter FromBinary(string name, byte[] data)
    {
        if (data.Length != 16)
            throw new ArgumentException("Invalid data length for NormalizedVolumeParameter");

        int volumePeriod = BitConverter.ToInt32(data, 0);
        int highVolume = BitConverter.ToInt32(data, 4);
        int lowVolume = BitConverter.ToInt32(data, 8);
        int normalHighVolumeRange = BitConverter.ToInt32(data, 12);

        var param = new NormalizedVolumeParameter(volumePeriod, highVolume, lowVolume, normalHighVolumeRange);
        return param;
    }

    // Register the type with the factory method
    static NormalizedVolumeParameter()
    {
        // This will be called when the class is first used
        RegisterType();
    }

    private static void RegisterType()
    {
        // Register this type with the factory
        IndicatorParameterBase.RegisterParameterType(TYPE_ID, (name, data) => NormalizedVolumeParameter.FromBinary(name, data));
    }

    public override int GetPermutationCount() => TotalPermutations;

    public override List<ParameterDescriptor> GetProperties()
    {
        return [
            new ParameterDescriptor(_volumePeriod, VolumePeriodRange.Min, VolumePeriodRange.Max, VolumePeriodRangeStep, "Volume Period"),
            new ParameterDescriptor(_highVolume, HighVolumeRange.Min, HighVolumeRange.Max, HighVolumeRangeStep, "High Volume"),
            new ParameterDescriptor(_lowVolume, LowVolumeRange.Min, LowVolumeRange.Max, LowVolumeRangeStep, "Low Volume"),
            new ParameterDescriptor(_normalHighVolumeRange, NormalHighVolumeRangeRange.Min, NormalHighVolumeRangeRange.Max, NormalHighVolumeRangeRangeStep, "Normal High Volume Range")
        ];
    }

    public override void UpdateFromDescriptor(ParameterDescriptor descriptor)
    {
        switch (descriptor.Name)
        {
            case "Volume Period":
                VolumePeriod = (int)descriptor.Value;
                break;
            case "High Volume":
                HighVolume = (int)descriptor.Value;
                break;
            case "Low Volume":
                LowVolume = (int)descriptor.Value;
                break;
            case "Normal High Volume Range":
                NormalHighVolumeRange = (int)descriptor.Value;
                break;
        }
    }

    public int VolumePeriod
    {
        get => _volumePeriod;
        set => ValidateAndSetValue(ref _volumePeriod, value, VolumePeriodRange);
    }

    public int HighVolume
    {
        get => _highVolume;
        set => ValidateAndSetValue(ref _highVolume, value, HighVolumeRange);
    }

    public int LowVolume
    {
        get => _lowVolume;
        set => ValidateAndSetValue(ref _lowVolume, value, LowVolumeRange);
    }

    public int NormalHighVolumeRange
    {
        get => _normalHighVolumeRange;
        set => ValidateAndSetValue(ref _normalHighVolumeRange, value, NormalHighVolumeRangeRange);
    }

    public NormalizedVolumeParameter() : base("normalized_volume")
    {
        // Always initialize to the minimum values of the range
        VolumePeriod = VolumePeriodRange.Min;
        HighVolume = HighVolumeRange.Min;
        LowVolume = LowVolumeRange.Min;
        NormalHighVolumeRange = NormalHighVolumeRangeRange.Min;
    }

    public NormalizedVolumeParameter(int volumePeriod, int highVolume, int lowVolume, int normalHighVolumeRange)
        : base("normalized_volume")
    {
        VolumePeriod = volumePeriod;
        HighVolume = highVolume;
        LowVolume = lowVolume;
        NormalHighVolumeRange = normalHighVolumeRange;
    }

    public override bool IncrementSingle()
    {
        if (IncrementValue(ref _normalHighVolumeRange, NormalHighVolumeRangeRangeStep, NormalHighVolumeRangeRange))
            return true;
        _normalHighVolumeRange = NormalHighVolumeRangeRange.Min;

        if (IncrementValue(ref _lowVolume, LowVolumeRangeStep, LowVolumeRange))
            return true;
        _lowVolume = LowVolumeRange.Min;

        if (IncrementValue(ref _highVolume, HighVolumeRangeStep, HighVolumeRange))
            return true;
        _highVolume = HighVolumeRange.Min;

        if (IncrementValue(ref _volumePeriod, VolumePeriodRangeStep, VolumePeriodRange))
            return true;

        return false;
    }

    public override void Reset()
    {
        VolumePeriod = VolumePeriodRange.Min;
        HighVolume = HighVolumeRange.Min;
        LowVolume = LowVolumeRange.Min;
        NormalHighVolumeRange = NormalHighVolumeRangeRange.Min;
    }

    public override NormalizedVolumeParameter DeepClone()
    {
        return new NormalizedVolumeParameter(VolumePeriod, HighVolume, LowVolume, NormalHighVolumeRange);
    }

    public override NormalizedVolumeParameter GetRandomNeighbor(Random random)
    {
        // Calculate how many steps are possible in each range
        int volumePeriodSteps = (VolumePeriodRange.Max - VolumePeriodRange.Min) / VolumePeriodRangeStep + 1;
        int highVolumeSteps = (HighVolumeRange.Max - HighVolumeRange.Min) / HighVolumeRangeStep + 1;
        int lowVolumeSteps = (LowVolumeRange.Max - LowVolumeRange.Min) / LowVolumeRangeStep + 1;
        int normalHighVolumeRangeSteps = (NormalHighVolumeRangeRange.Max - NormalHighVolumeRangeRange.Min) / NormalHighVolumeRangeRangeStep + 1;

        // Choose a random step for each parameter
        var volumePeriod = VolumePeriodRange.Min + (random.Next(volumePeriodSteps) * VolumePeriodRangeStep);
        var highVolume = HighVolumeRange.Min + (random.Next(highVolumeSteps) * HighVolumeRangeStep);
        var lowVolume = LowVolumeRange.Min + (random.Next(lowVolumeSteps) * LowVolumeRangeStep);
        var normalHighVolumeRange = NormalHighVolumeRangeRange.Min + (random.Next(normalHighVolumeRangeSteps) * NormalHighVolumeRangeRangeStep);

        return new NormalizedVolumeParameter(volumePeriod, highVolume, lowVolume, normalHighVolumeRange);
    }

    protected override int GetAdditionalHashCodeComponents()
    {
        return HashCode.Combine(VolumePeriod, HighVolume, LowVolume, NormalHighVolumeRange);
    }
}
