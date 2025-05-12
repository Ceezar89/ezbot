namespace EzBot.Core.IndicatorParameter;

public class TrendiloParameter : IndicatorParameterBase
{
    private int _smoothing = 2;
    private int _lookback = 40;
    private double _almaOffset = 0.8;
    private int _almaSigma = 6;
    private double _bandMultiplier = 1.0;

    // Ranges
    private static readonly (int Min, int Max) SmoothingRange = (1, 1);
    private static readonly (int Min, int Max) LookbackRange = (50, 50);
    private static readonly (double Min, double Max) AlmaOffsetRange = (0.85, 0.85);
    private static readonly (int Min, int Max) AlmaSigmaRange = (6, 6);
    private static readonly (double Min, double Max) BandMultiplierRange = (1.0, 1.0);

    // Steps
    private const int SmoothingRangeStep = 5;
    private const int LookbackRangeStep = 50;
    private const double AlmaOffsetRangeStep = 0.1;
    private const int AlmaSigmaRangeStep = 1;
    private const double BandMultiplierRangeStep = 0.5;

    // Correctly calculate the number of steps for each parameter range
    private static readonly int SmoothingPermutations = CalculateSteps(SmoothingRange.Min, SmoothingRange.Max, SmoothingRangeStep);
    private static readonly int LookbackPermutations = CalculateSteps(LookbackRange.Min, LookbackRange.Max, LookbackRangeStep);
    private static readonly int AlmaOffsetPermutations = CalculateSteps(AlmaOffsetRange.Min, AlmaOffsetRange.Max, AlmaOffsetRangeStep);
    private static readonly int AlmaSigmaPermutations = CalculateSteps(AlmaSigmaRange.Min, AlmaSigmaRange.Max, AlmaSigmaRangeStep);
    private static readonly int BandMultiplierPermutations = CalculateSteps(BandMultiplierRange.Min, BandMultiplierRange.Max, BandMultiplierRangeStep);

    public static readonly int TotalPermutations = SmoothingPermutations * LookbackPermutations * AlmaOffsetPermutations * AlmaSigmaPermutations * BandMultiplierPermutations;

    // Type identifier for TrendiloParameter (unique across all parameter types)
    private const byte TYPE_ID = 0x07;

    protected override byte GetTypeIdentifier() => TYPE_ID;

    protected override byte[] GetParameterSpecificBinaryData()
    {
        byte[] data = new byte[24]; // 4 bytes for each of the 3 int parameters, 8 bytes for each of the 2 double parameters

        // Store Smoothing (4 bytes)
        BitConverter.GetBytes(_smoothing).CopyTo(data, 0);

        // Store Lookback (4 bytes)
        BitConverter.GetBytes(_lookback).CopyTo(data, 4);

        // Store AlmaOffset (8 bytes)
        BitConverter.GetBytes(_almaOffset).CopyTo(data, 8);

        // Store AlmaSigma (4 bytes)
        BitConverter.GetBytes(_almaSigma).CopyTo(data, 16);

        // Store BandMultiplier (8 bytes)
        BitConverter.GetBytes(_bandMultiplier).CopyTo(data, 20);

        return data;
    }

    /// <summary>
    /// Updates the instance fields from the parameter-specific binary data.
    /// </summary>
    /// <param name="data">Byte array containing only the specific data for this parameter type.</param>
    /// <exception cref="ArgumentException">Thrown if data length is incorrect.</exception>
    protected override void UpdateFromParameterSpecificBinaryData(byte[] data)
    {
        if (data.Length != 24) // Ensure the data length matches serialization format
            throw new ArgumentException("Invalid data length for TrendiloParameter update", nameof(data));

        // Update fields using properties to leverage validation logic, matching the order in GetParameterSpecificBinaryData
        Smoothing = BitConverter.ToInt32(data, 0);
        Lookback = BitConverter.ToInt32(data, 4);
        AlmaOffset = BitConverter.ToDouble(data, 8);
        AlmaSigma = BitConverter.ToInt32(data, 16);
        BandMultiplier = BitConverter.ToDouble(data, 20);
    }

    // Static method to create TrendiloParameter from binary data
    public static TrendiloParameter FromBinary(string name, byte[] data)
    {
        if (data.Length != 24)
            throw new ArgumentException("Invalid data length for TrendiloParameter");

        int smoothing = BitConverter.ToInt32(data, 0);
        int lookback = BitConverter.ToInt32(data, 4);
        double almaOffset = BitConverter.ToDouble(data, 8);
        int almaSigma = BitConverter.ToInt32(data, 16);
        double bandMultiplier = BitConverter.ToDouble(data, 20);

        return new TrendiloParameter(name, smoothing, lookback, almaOffset, almaSigma, bandMultiplier);
    }

    // Register the type with the factory method
    static TrendiloParameter()
    {
        // This will be called when the class is first used
        RegisterType();
    }

    private static void RegisterType()
    {
        // Register this type with the factory
        IndicatorParameterBase.RegisterParameterType(TYPE_ID, (name, data) => TrendiloParameter.FromBinary(name, data));
    }

    public override int GetPermutationCount()
    {
        return TotalPermutations;
    }

    public override List<ParameterDescriptor> GetProperties()
    {
        return [
            new ParameterDescriptor(_smoothing, SmoothingRange.Min, SmoothingRange.Max, SmoothingRangeStep, "Smoothing"),
            new ParameterDescriptor(_lookback, LookbackRange.Min, LookbackRange.Max, LookbackRangeStep, "Lookback"),
            new ParameterDescriptor(_almaOffset, AlmaOffsetRange.Min, AlmaOffsetRange.Max, AlmaOffsetRangeStep, "Alma Offset"),
            new ParameterDescriptor(_almaSigma, AlmaSigmaRange.Min, AlmaSigmaRange.Max, AlmaSigmaRangeStep, "Alma Sigma"),
            new ParameterDescriptor(_bandMultiplier, BandMultiplierRange.Min, BandMultiplierRange.Max, BandMultiplierRangeStep, "Band Multiplier")
        ];
    }

    public override void UpdateFromDescriptor(ParameterDescriptor descriptor)
    {
        switch (descriptor.Name)
        {
            case "Smoothing":
                Smoothing = (int)descriptor.Value;
                break;
            case "Lookback":
                Lookback = (int)descriptor.Value;
                break;
            case "Alma Offset":
                AlmaOffset = (double)descriptor.Value;
                break;
            case "Alma Sigma":
                AlmaSigma = (int)descriptor.Value;
                break;
            case "Band Multiplier":
                BandMultiplier = (double)descriptor.Value;
                break;
        }
    }

    public int Smoothing
    {
        get => _smoothing;
        set => ValidateAndSetValue(ref _smoothing, value, SmoothingRange);
    }

    public int Lookback
    {
        get => _lookback;
        set => ValidateAndSetValue(ref _lookback, value, LookbackRange);
    }

    public double AlmaOffset
    {
        get => _almaOffset;
        set => ValidateAndSetValue(ref _almaOffset, value, AlmaOffsetRange);
    }

    public int AlmaSigma
    {
        get => _almaSigma;
        set => ValidateAndSetValue(ref _almaSigma, value, AlmaSigmaRange);
    }

    public double BandMultiplier
    {
        get => _bandMultiplier;
        set => ValidateAndSetValue(ref _bandMultiplier, value, BandMultiplierRange);
    }

    public TrendiloParameter() : base("trendilo")
    {
        // Initialize to minimum values in the allowed ranges
        Smoothing = SmoothingRange.Min;
        Lookback = LookbackRange.Min;
        AlmaOffset = AlmaOffsetRange.Min;
        AlmaSigma = AlmaSigmaRange.Min;
        BandMultiplier = BandMultiplierRange.Min;
    }

    public TrendiloParameter(string name, int smoothing, int lookback, double almaOffset, int almaSigma, double bandMultiplier)
        : base(name)
    {
        Smoothing = smoothing;
        Lookback = lookback;
        AlmaOffset = almaOffset;
        AlmaSigma = almaSigma;
        BandMultiplier = bandMultiplier;
    }

    public override bool IncrementSingle()
    {
        if (IncrementValue(ref _bandMultiplier, BandMultiplierRangeStep, BandMultiplierRange))
            return true;
        _bandMultiplier = BandMultiplierRange.Min;

        if (IncrementValue(ref _almaSigma, AlmaSigmaRangeStep, AlmaSigmaRange))
            return true;
        _almaSigma = AlmaSigmaRange.Min;

        if (IncrementValue(ref _almaOffset, AlmaOffsetRangeStep, AlmaOffsetRange))
            return true;
        _almaOffset = AlmaOffsetRange.Min;

        if (IncrementValue(ref _lookback, LookbackRangeStep, LookbackRange))
            return true;
        _lookback = LookbackRange.Min;

        if (IncrementValue(ref _smoothing, SmoothingRangeStep, SmoothingRange))
            return true;

        return false;
    }

    public override void Reset()
    {
        Smoothing = SmoothingRange.Min;
        Lookback = LookbackRange.Min;
        AlmaOffset = AlmaOffsetRange.Min;
        AlmaSigma = AlmaSigmaRange.Min;
        BandMultiplier = BandMultiplierRange.Min;
    }

    public override TrendiloParameter DeepClone()
    {
        return new TrendiloParameter(Name, Smoothing, Lookback, AlmaOffset, AlmaSigma, BandMultiplier);
    }

    public override TrendiloParameter GetRandomNeighbor(Random random)
    {
        // Calculate how many steps are possible in each range
        int smoothingSteps = (SmoothingRange.Max - SmoothingRange.Min) / SmoothingRangeStep + 1;
        int lookbackSteps = (LookbackRange.Max - LookbackRange.Min) / LookbackRangeStep + 1;
        int almaSigmaSteps = (AlmaSigmaRange.Max - AlmaSigmaRange.Min) / AlmaSigmaRangeStep + 1;
        int almaOffsetSteps = (int)Math.Floor((AlmaOffsetRange.Max - AlmaOffsetRange.Min) / AlmaOffsetRangeStep) + 1;
        int bandMultiplierSteps = (int)Math.Floor((BandMultiplierRange.Max - BandMultiplierRange.Min) / BandMultiplierRangeStep) + 1;

        // Choose a random step for each parameter
        var smoothing = SmoothingRange.Min + (random.Next(smoothingSteps) * SmoothingRangeStep);
        var lookback = LookbackRange.Min + (random.Next(lookbackSteps) * LookbackRangeStep);
        var almaSigma = AlmaSigmaRange.Min + (random.Next(almaSigmaSteps) * AlmaSigmaRangeStep);
        var almaOffset = AlmaOffsetRange.Min + (random.Next(almaOffsetSteps) * AlmaOffsetRangeStep);
        var bandMultiplier = BandMultiplierRange.Min + (random.Next(bandMultiplierSteps) * BandMultiplierRangeStep);

        return new TrendiloParameter(Name, smoothing, lookback, almaOffset, almaSigma, bandMultiplier);
    }

    protected override int GetAdditionalHashCodeComponents()
    {
        return HashCode.Combine(Smoothing, Lookback, AlmaOffset, AlmaSigma, BandMultiplier);
    }
}