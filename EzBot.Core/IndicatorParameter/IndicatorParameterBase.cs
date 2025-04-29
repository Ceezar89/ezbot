namespace EzBot.Core.IndicatorParameter;

using System;
using System.Collections.Generic;

public abstract class IndicatorParameterBase(string name) : IIndicatorParameter
{
    // Factory dictionary to store type creation functions
    private static readonly Dictionary<byte, Func<string, byte[], IIndicatorParameter>> _typeRegistry = [];
    public string Name { get; } = name;

    // Register a parameter type with the factory
    public static void RegisterParameterType(byte typeId, Func<string, byte[], IIndicatorParameter> constructor)
    {
        if (_typeRegistry.ContainsKey(typeId))
        {
            throw new ArgumentException($"Type ID {typeId} is already registered");
        }
        _typeRegistry[typeId] = constructor;
    }

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
            if (newValue <= ((double)(object)range.Max))
            {
                // Round to one decimal place to avoid floating point precision issues
                newValue = Math.Round(newValue, 1);
                field = (T)(object)newValue;
                return true;
            }
        }
        return false;
    }

    // Utility method to calculate the number of steps in a range 
    // to find the theoretical maximum number of permutations
    protected static int CalculateSteps<T>(T min, T max, T step) where T : IComparable<T>
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

    /// <summary>
    /// Deserializes a byte array back into an IIndicatorParameter instance.
    /// Assumes the byte array was created using ToBinary().
    /// </summary>
    /// <param name="data">The byte array containing the serialized parameter.</param>
    /// <returns>An IIndicatorParameter instance.</returns>
    /// <exception cref="ArgumentException">Thrown if the type identifier is unknown or data is malformed.</exception>
    public static IIndicatorParameter FromBinaryStatic(byte[] data)
    {
        // Header structure: [TypeID (1 byte)][Name Length (4 bytes)][Name (variable bytes)][Parameter Data (variable bytes)]
        const int HeaderOffsetName = 1 + 4; // Offset after TypeID (1) and Name Length (4)

        if (data == null || data.Length < HeaderOffsetName) // Basic validation
        {
            throw new ArgumentException("Input byte array is null or too short to contain header.");
        }

        // Get type identifier (first byte)
        byte typeId = data[0];

        // Extract name length (4 bytes starting at index 1)
        int nameLength = BitConverter.ToInt32(data, 1);
        if (nameLength < 0 || HeaderOffsetName + nameLength > data.Length) // Validate nameLength
        {
            throw new ArgumentException("Invalid name length decoded from byte array.");
        }

        // Decode name string (variable length starting after the header)
        string name = System.Text.Encoding.UTF8.GetString(data, HeaderOffsetName, nameLength);

        // Calculate the starting position of the parameter-specific data
        int paramDataOffset = HeaderOffsetName + nameLength;
        // Calculate the length of the parameter-specific data
        int paramDataLength = data.Length - paramDataOffset;

        // Create parameter-specific data array
        byte[] paramData = new byte[paramDataLength];
        // Copy the parameter-specific data from the main byte array
        Array.Copy(data, paramDataOffset, paramData, 0, paramDataLength);

        // Look up the registered factory method based on the type identifier
        if (_typeRegistry.TryGetValue(typeId, out var creator))
        {
            // Call the factory method with name and specific data
            return creator(name, paramData);
        }
        // Handle unknown type
        throw new ArgumentException($"Unknown parameter type identifier: {typeId}");
    }

    // To be implemented by derived classes to provide parameter-specific binary data
    protected abstract byte[] GetParameterSpecificBinaryData();

    // To be implemented by derived classes to provide their type identifier
    protected abstract byte GetTypeIdentifier();

    // To be implemented by derived classes to update from parameter-specific binary data
    protected abstract void UpdateFromParameterSpecificBinaryData(byte[] data);

    /// <summary>
    /// Updates the current parameter instance from a binary representation.
    /// Assumes the byte array was created using ToBinary() for the same parameter type and name.
    /// </summary>
    /// <param name="data">The byte array containing the serialized parameter.</param>
    /// <exception cref="ArgumentException">Thrown if the type identifier or name doesn't match, or data is malformed.</exception>
    public void UpdateFromBinary(byte[] data)
    {
        // Header structure: [TypeID (1 byte)][Name Length (4 bytes)][Name (variable bytes)][Parameter Data (variable bytes)]
        const int HeaderOffsetName = 1 + 4; // Offset after TypeID (1) and Name Length (4)

        if (data == null || data.Length < HeaderOffsetName) // Basic validation
        {
            throw new ArgumentException("Input byte array is null or too short to contain header.", nameof(data));
        }

        // Get type identifier (first byte) and verify it matches
        byte typeId = data[0];
        if (typeId != GetTypeIdentifier())
        {
            throw new ArgumentException($"Type identifier mismatch. Expected {GetTypeIdentifier()}, got {typeId}.", nameof(data));
        }

        // Extract name length (4 bytes starting at index 1)
        int nameLength = BitConverter.ToInt32(data, 1);
        if (nameLength < 0 || HeaderOffsetName + nameLength > data.Length) // Validate nameLength
        {
            throw new ArgumentException("Invalid name length decoded from byte array.", nameof(data));
        }

        // Decode name string and verify it matches
        string decodedName = System.Text.Encoding.UTF8.GetString(data, HeaderOffsetName, nameLength);
        if (decodedName != Name)
        {
            throw new ArgumentException($"Parameter name mismatch. Expected '{Name}', got '{decodedName}'.", nameof(data));
        }

        // Calculate the starting position and length of the parameter-specific data
        int paramDataOffset = HeaderOffsetName + nameLength;
        int paramDataLength = data.Length - paramDataOffset;

        // Extract parameter-specific data
        byte[] paramData = new byte[paramDataLength];
        Array.Copy(data, paramDataOffset, paramData, 0, paramDataLength);

        // Call the derived class implementation to update state from specific data
        UpdateFromParameterSpecificBinaryData(paramData);
        // Potentially invalidate any cached state in the parameter itself if needed
        // InvalidateCache();
    }

    public abstract bool IncrementSingle();
    public abstract IIndicatorParameter DeepClone();
    public abstract List<ParameterDescriptor> GetProperties();
    public abstract IIndicatorParameter GetRandomNeighbor(Random random);
    public abstract void UpdateFromDescriptor(ParameterDescriptor descriptor);
    public abstract int GetPermutationCount();
    public abstract void Reset();
    protected abstract int GetAdditionalHashCodeComponents();

    /// <summary>
    /// Generates all possible permutations for this specific parameter type based on its ranges and steps.
    /// </summary>
    /// <returns>A list of byte arrays, each representing one parameter permutation.</returns>
    public virtual List<byte[]> GenerateAllPermutationsBinary()
    {
        var permutations = new List<byte[]>();
        var clone = DeepClone(); // Work on a clone
        clone.Reset(); // Start from the minimum values

        permutations.Add(clone.ToBinary()); // Add the initial state

        while (clone.IncrementSingle())
        {
            permutations.Add(clone.ToBinary());
        }

        return permutations;
    }

    // Base implementation of equality comparison
    public bool Equals(IIndicatorParameter? other)
    {
        if (other == null || GetType() != other.GetType())
            return false;

        return Name == other.Name;
    }

    // Base implementation of GetHashCode
    public override int GetHashCode()
    {
        return HashCode.Combine(Name, GetAdditionalHashCodeComponents());
    }
}