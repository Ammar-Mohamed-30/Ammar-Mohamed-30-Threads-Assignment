
using System.Text.Json;
using OrderProcessing.Shared.Exceptions;

namespace OrderProcessing.Shared.Storage;

public class JsonFileStore
{
    private readonly string _dataDirectory;

    private static readonly SemaphoreSlim FileLock = new(1, 1);

    public JsonFileStore()
    {
        _dataDirectory = DataFiles.DataDirectory;
        Directory.CreateDirectory(_dataDirectory);
    }

    public async Task<List<T>> ReadAsync<T>(
        string fileName,
        CancellationToken ct = default)
    {
        string filePath = GetFilePath(fileName);

        await FileLock.WaitAsync(ct);

        try
        {
            return await ReadCoreAsync<T>(filePath, ct);
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task WriteAsync<T>(
        string fileName,
        List<T> data,
        CancellationToken ct = default)
    {
        string filePath = GetFilePath(fileName);

        await FileLock.WaitAsync(ct);

        try
        {
            await WriteCoreAsync(filePath, data, ct);
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task<TResult> UpdateAsync<T, TResult>(
        string fileName,
        Func<List<T>, TResult> update,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        string filePath = GetFilePath(fileName);

        await FileLock.WaitAsync(ct);

        try
        {
            List<T> data = await ReadCoreAsync<T>(filePath, ct);

            TResult result = update(data);

            await WriteCoreAsync(filePath, data, ct);

            return result;
        }
        finally
        {
            FileLock.Release();
        }
    }

    private async Task<List<T>> ReadCoreAsync<T>(
        string filePath,
        CancellationToken ct)
    {
        if (!File.Exists(filePath))
        {
            return new List<T>();
        }

        try
        {
            await using FileStream stream = new(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous);

            List<T>? data = await JsonSerializer.DeserializeAsync<List<T>>(
                stream,
                JsonOptions.Default,
                ct);

            return data ?? new List<T>();
        }
        catch (JsonException ex)
        {
            throw new DataFileCorruptedException(filePath, ex);
        }
    }

    private async Task WriteCoreAsync<T>(
        string filePath,
        List<T> data,
        CancellationToken ct)
    {
        string tempPath =
            $"{filePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (FileStream stream = new(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    data,
                    JsonOptions.Default,
                    ct);

                await stream.FlushAsync(ct);
            }

            ct.ThrowIfCancellationRequested();

            File.Move(tempPath, filePath, overwrite: true);
        }
        catch (OperationCanceledException)
        {
            TryDelete(tempPath);
            throw;
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            JsonException)
        {
            TryDelete(tempPath);

            throw new DatabaseWriteException(
                $"Failed to write data file '{Path.GetFileName(filePath)}'.",
                ex);
        }
    }

    private string GetFilePath(string fileName)
    {
        if (Path.GetFileName(fileName) != fileName)
        {
            throw new ArgumentException(
                "Only file names are allowed.",
                nameof(fileName));
        }

        return Path.Combine(_dataDirectory, fileName);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
