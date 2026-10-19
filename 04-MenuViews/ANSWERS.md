# Assignment 04 — Answers

Student name: Ammar Mohamed  
Group / ID: 1

## 1. Why do the Expected and Actual numbers disagree?

The original implementation used `Views++`, which is not an atomic operation. It involves reading the current value, incrementing it, and writing the result back. When multiple threads execute these steps concurrently, some updates can be lost. This is a race condition.

## 2. How did you fix `RegisterView`?

I replaced `Views++` with `Interlocked.Increment(ref Views)`. This performs the increment atomically, ensuring that concurrent updates to the counter are not lost.

## 3. Why is `Interlocked` suitable for a single counter?

`Interlocked` is suitable because the operation is a simple atomic update to one integer. It avoids the extra locking code required by `lock` and provides a thread-safe way to increment the counter.

## 4. When would you protect this kind of update differently?

I would use `lock` when an operation needs to protect multiple related variables or several statements that must execute together as one critical section. For a simple counter increment, `Interlocked.Increment` is simpler.

## 5. Test result

Original output:

```text
Expected: 400000
Actual:   107483
```

Output after the fix:

```text
Expected: 400000
Actual:   400000
```

The final result confirms that all 400,000 increments were recorded correctly.

## Links

- [Microsoft Learn — Interlocked Class](https://learn.microsoft.com/en-us/dotnet/api/system.threading.interlocked)
- [Microsoft Learn — Interlocked.Increment Method](https://learn.microsoft.com/en-us/dotnet/api/system.threading.interlocked.increment)
