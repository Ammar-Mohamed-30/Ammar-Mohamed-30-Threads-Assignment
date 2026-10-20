# Ammar-Mohamed-30-Threads-Assignment
Assignment repo for assignment/1-8 (Threads Assignment)
## Phase 1 — Sequential Order Validation

### Q1. Why are the sync and async execution times nearly equal when using `await` sequentially?

When asynchronous operations are awaited one by one inside a `foreach` loop, each iteration waits for the previous operation to finish before starting the next one. This means there is no concurrency between iterations, so the total execution time is approximately the sum of all individual operation times.

Async/await does not automatically make sequential code faster. Its main benefit is that the calling thread can remain available while asynchronous I/O operations are in progress.

### Q2. Is the thread ID before and after `await` always the same? Why?

No. The thread ID before and after an `await` is not guaranteed to be the same. When an asynchronous operation completes, its continuation may run on a different thread, depending on the synchronization context and execution environment.

The actual behavior should be demonstrated using the application's console logs. Thread IDs observed in one run are evidence of that run, not a guarantee for every execution.

### Q3. What happens to a thread during `Task.Delay` compared with `Thread.Sleep`?

`Thread.Sleep` blocks the current thread for the specified duration, preventing it from doing other work during that time.

`Task.Delay` provides an asynchronous delay. When awaited, it allows the current method to suspend without blocking a thread for the duration of the delay. The method continues when the delay completes.

### Q4. Why do repository methods return `Task<T>` instead of `T`?

Repository methods often perform I/O operations, such as reading or writing files or querying a database. Returning `Task<T>` allows callers to await those operations without blocking a thread while the I/O is in progress.

This supports scalable asynchronous workflows and allows cancellation to be propagated when the operation supports it.

### Q5. What is the difference between `File.ReadAllText` and `File.ReadAllTextAsync`? Does asynchronous disk reading occupy a thread?

`File.ReadAllText` performs synchronous file reading and blocks the calling thread until the operation completes.

`File.ReadAllTextAsync` provides an asynchronous API that can be awaited, allowing the calling method to suspend instead of blocking its thread while waiting for I/O.

Asynchronous file reading does not mean that no thread is ever involved. The implementation may use operating-system I/O facilities or involve threads internally, depending on the platform. The key benefit is avoiding the need to block the calling thread while waiting for the read to