namespace EzBot.Core.IndicatorParameter;

using System;
using System.Collections.Generic;

public abstract class IndicatorParameterBase(string name) : IIndicatorParameter
{
    // Factory dictionary to store type creation functions
    private static readonly Dictionary<byte, Func<string, byte[], IIndicatorParameter>> _typeRegistry = new();

    // Register a parameter type with the factory
    public static void RegisterParameterType(byte typeId, Func<string, byte[], IIndicatorParameter> creator)
    {
        if (_typeRegistry.ContainsKey(typeId))
        {
            throw new ArgumentException($"Type ID {typeId} is already registered");
        }
        _typeRegistry[typeId] = creator;
    }

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

    public bool Equals(IIndicatorParameter? other)
    {
        if (other == null || GetType() != other.GetType())
            return false;

        return Name == other.Name;
    }

    // Base implementation of binary serialization
    public virtual byte[] ToBinary()
    {
        // Get parameter-specific binary data from derived class
        byte[] paramData = GetParameterSpecificBinaryData();

        // Create byte array with space for type identifier byte and name
        byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(Name);
        byte[] result = new byte[1 + 4 + nameBytes.Length + paramData.Length];

        // Add type identifier byte (to be set by derived class)
        result[0] = GetTypeIdentifier();

        // Add name length (4 bytes) and name
        BitConverter.GetBytes(nameBytes.Length).CopyTo(result, 1);
        Array.Copy(nameBytes, 0, result, 5, nameBytes.Length);

        // Add parameter data
        Array.Copy(paramData, 0, result, 5 + nameBytes.Length, paramData.Length);

        return result;
    }

    // To be implemented by derived classes to provide parameter-specific binary data
    protected abstract byte[] GetParameterSpecificBinaryData();

    // To be implemented by derived classes to provide their type identifier
    protected abstract byte GetTypeIdentifier();

    // Static method for creating parameter instance from binary data
    public virtual IIndicatorParameter FromBinary(byte[] data)
    {
        // Get type identifier
        byte typeId = data[0];

        // Extract name
        int nameLength = BitConverter.ToInt32(data, 1);
        string name = System.Text.Encoding.UTF8.GetString(data, 5, nameLength);

        // Create parameter-specific data array
        byte[] paramData = new byte[data.Length - (5 + nameLength)];
        Array.Copy(data, 5 + nameLength, paramData, 0, paramData.Length);

        // Create appropriate parameter type based on type identifier
        return CreateParameterFromBinary(typeId, name, paramData);
    }

    // To be implemented by factory method to create appropriate parameter instance
    protected static IIndicatorParameter CreateParameterFromBinary(byte typeId, string name, byte[] paramData)
    {
        if (_typeRegistry.TryGetValue(typeId, out var creator))
        {
            return creator(name, paramData);
        }

        throw new ArgumentException($"Unknown parameter type identifier: {typeId}");
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

    /// <summary>
    /// Generates all possible permutations of parameters and returns them as binary data
    /// </summary>
    /// <typeparam name="T">The specific parameter type</typeparam>
    /// <param name="factory">Factory function to create an initial parameter instance</param>
    /// <returns>List of binary representations of all permutations</returns>
    protected static List<byte[]> GenerateAllPermutationsBinary<T>(Func<T> factory) where T : IndicatorParameterBase
    {
        var result = new List<byte[]>();

        // Create initial instance
        var parameter = factory();

        // Reset to ensure we start from the minimum values
        parameter.Reset();

        // Add first permutation
        result.Add(parameter.ToBinary());

        // Generate all permutations
        while (parameter.CanIncrement())
        {
            parameter.IncrementSingle();
            result.Add(parameter.ToBinary());
        }

        return result;
    }

    /// <summary>
    /// Deserializes all binary permutations back to parameter objects
    /// </summary>
    /// <param name="binaryPermutations">Collection of binary serialized parameters</param>
    /// <returns>List of deserialized parameter objects</returns>
    public List<IIndicatorParameter> DeserializeAllPermutations(IEnumerable<byte[]> binaryPermutations)
    {
        var result = new List<IIndicatorParameter>();

        foreach (var binary in binaryPermutations)
        {
            result.Add(FromBinary(binary));
        }

        return result;
    }
}