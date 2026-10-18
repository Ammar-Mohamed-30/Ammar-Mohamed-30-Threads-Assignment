# Assignment 03 — Answers

Student name: Ammar Mohamed  
Group / ID: 1

## 1. What is `System.Threading.Monitor` used for?

`System.Threading.Monitor` is used to synchronize multiple threads when they access shared data. It allows only one thread at a time to enter a critical section protected by the same lock object.

For example, `Monitor.Enter()` acquires the lock, and `Monitor.Exit()` releases it. This helps prevent race conditions when multiple threads read or modify shared data.

## 2. What does the C# `lock` statement compile to?

For a regular object used as the lock target, the C# `lock` statement is translated into code that uses `Monitor.Enter()` and `Monitor.Exit()`, with a `try-finally` block to ensure the lock is released even if an exception occurs.

In modern C# versions, when the lock target is a `System.Threading.Lock` object, the compiler can use a different optimized implementation.

## 3. List at least two differences between using `lock` and calling `Monitor` APIs directly.

- **Simplicity:** `lock` is shorter and easier to read, while using `Monitor` directly requires more code.
- **Automatic release:** `lock` automatically ensures the lock is released. With direct `Monitor` calls, the developer must use `try-finally` correctly.
- **Advanced control:** Direct `Monitor` APIs provide methods such as `TryEnter`, `Wait`, and `Pulse`, which allow more control over synchronization.

## 4. When might a developer use `Monitor.TryEnter`, `Monitor.Pulse`, or `Monitor.Wait` instead of a plain `lock`?

- **`Monitor.TryEnter`:** Use it when a thread should try to acquire a lock without waiting forever. It can also use a timeout and returns whether the lock was acquired.
- **`Monitor.Wait`:** Use it when a thread needs to wait for a condition to change. It temporarily releases the monitor and waits until it is signaled or times out, then reacquires the monitor before returning.
- **`Monitor.Pulse`:** Use it to signal one thread waiting on the same monitor that a relevant condition may have changed. The waiting thread must reacquire the lock before continuing.

These methods are useful when coordinating threads based on conditions, rather than simply protecting a block of code.

## 5. Write the same critical section twice: once with `lock`, once with `Monitor.Enter` / `Monitor.Exit`.

Both examples below protect the same operation: incrementing and printing a shared counter.

**Using `lock`:**

```csharp
private static readonly object _gate = new();
private static int _counter = 0;

lock (_gate)
{
    _counter++;
    Console.WriteLine(_counter);
}
```

**Using `Monitor.Enter` / `Monitor.Exit`:**

```csharp
private static readonly object _gate = new();
private static int _counter = 0;

bool lockTaken = false;

try
{
    Monitor.Enter(_gate, ref lockTaken);

    _counter++;
    Console.WriteLine(_counter);
}
finally
{
    if (lockTaken)
    {
        Monitor.Exit(_gate);
    }
}
```

Both versions protect the counter using the same lock object. The `Monitor` version requires more code to ensure the lock is released safely.

## Links

- [Microsoft Learn — Monitor Class](https://learn.microsoft.com/en-us/dotnet/api/system.threading.monitor)
- [Microsoft Learn — C# lock statement](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock)
