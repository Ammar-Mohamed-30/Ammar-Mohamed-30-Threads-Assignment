
namespace OrderProcessing.Shared.Exceptions;

public class DataFileCorruptedException : Exception
{
    public DataFileCorruptedException(string filePath, Exception innerException)
        : base($"The data file '{filePath}' is corrupted or contains invalid JSON.", innerException)
    {
    }
}
