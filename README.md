# Ammar-Mohamed-30-Threads-Assignment

Assignment repo for assignment/1-8 (Threads Assignment).

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

Asynchronous file reading does not mean that no thread is ever involved. The implementation may use operating-system I/O facilities or involve threads internally, depending on the platform. The key benefit is avoiding the need to block the calling thread while waiting for the read to complete.

---

## Phase 2 — Concurrent Order Processing

### Q1. Why should independent order-processing operations run concurrently?

Orders can usually be processed independently. Running their asynchronous operations concurrently allows the application to make progress on other orders while an order is waiting for file access, payment processing, or another I/O operation.

This can reduce total elapsed time compared with processing every order sequentially.

### Q2. Why use `Task.WhenAll`?

`Task.WhenAll` allows the application to start multiple asynchronous operations and then await their completion together.

Instead of waiting for each order to finish before starting the next one, the application can start the independent operations and wait for all of them afterward.

### Q3. Does `Task.WhenAll` create a new thread for every task?

No. `Task.WhenAll` coordinates the completion of tasks; it does not create a dedicated thread for every task.

Asynchronous I/O operations can remain pending without blocking a thread while they wait. The runtime and the underlying operations determine how continuations and any CPU work are scheduled.

### Q4. What happens if multiple tasks fail?

`Task.WhenAll` completes only after all supplied tasks have completed. If tasks fail, the returned task records their exceptions.

Awaiting the combined task normally throws one of the exceptions. To inspect every failure, retain the combined task and inspect its `Exception` property after it has completed unsuccessfully. That property contains an `AggregateException` with the collected exceptions.

### Q5. Why should independent tasks be started before awaiting them?

If each task is awaited immediately inside a loop, the next operation does not start until the current operation finishes. This prevents the intended concurrency.

Starting the independent tasks first and then awaiting their combined completion allows the operations to overlap.

---

## Phase 3 — Thread Safety and Coordination

### Q1. What is a race condition?

A race condition occurs when multiple operations access or modify shared state and the result depends on their timing or execution order.

For example, two threads can read the same counter value and both calculate the same next value. One update may overwrite the other, causing an incorrect final count.

### Q2. Why is synchronization necessary when accessing shared mutable data?

When multiple threads access shared mutable state, synchronization may be required to prevent conflicting updates and preserve data consistency.

The appropriate mechanism depends on the operation. `lock` is useful for protecting a critical section, while `Interlocked` provides atomic operations for supported primitive updates.

### Q3. What does `lock` do?

The `lock` statement allows only one thread at a time to execute the protected critical section for the same lock object.

Other threads attempting to enter that critical section must wait until the lock is released.

A lock should protect only the necessary shared-state operations. It should not be held across an `await`.

### Q4. What is the difference between concurrency and parallelism?

Concurrency means multiple operations can make progress during overlapping periods, even if they are not executing simultaneously.

Parallelism means multiple operations are actually executing at the same time, typically on different CPU cores.

Asynchronous I/O commonly improves concurrency, while CPU-intensive work can benefit from parallel execution when multiple processor cores are available.

### Q5. Why can `Task.WhenAll` alone not prevent race conditions?

`Task.WhenAll` coordinates when tasks complete; it does not protect shared data from simultaneous access.

If multiple tasks modify the same non-thread-safe collection or shared variable, synchronization or a thread-safe data structure is still necessary.

---

## Phase 4 — Advanced Async, Threads and Thread Safety

### Task 10.1 — Concurrent Order Processing

The application processes multiple orders concurrently and collects their results through a `Channel<CheckoutResult>`. `Task.WhenAll` is used to await the processing operations, while the channel allows the results to be consumed as they become available.

Starting independent operations before awaiting them avoids the sequential bottleneck that occurs when each operation is awaited immediately inside a loop.

### Task 10.2 — Customer Dashboard

The dashboard loads the required customer-related data asynchronously. The successful load is demonstrated using a valid customer ID, and an invalid customer ID is used to demonstrate error handling.

When several independent asynchronous loads can fail, retaining and inspecting the combined task's `Exception` property after `Task.WhenAll` fails allows the application to examine all collected exceptions. A plain `await` on the combined task normally propagates one exception rather than directly presenting every exception to the caller.

### Task 10.3 — Fastest Shipping Quote

The application requests shipping quotes from multiple carriers concurrently and selects the first successful quote within the configured time limit.

A cancellation token is used to cancel losing operations after a suitable quote is obtained. If the losing operations were never cancelled, they could continue consuming resources and performing unnecessary work even though their results are no longer needed.

### Task 10.4 A — Fraud Scoring

The application scores 10,000 orders using both sequential and parallel execution. An order is flagged when its score is at least 90.

The scoring algorithm is CPU-intensive, so parallel execution can improve performance by distributing work across available processor cores. `Parallel.ForEachAsync` is configured with `Environment.ProcessorCount` as its maximum degree of parallelism and receives a cancellation token.

**Observed results from a successful run:**

| Metric | Result |
|---|---:|
| Total orders | 10,000 |
| Sequential flagged count | 1,005 |
| Parallel flagged count | 1,005 |
| Sequential execution time | 6,106 ms |
| Parallel execution time | 476 ms |
| Speed-up | 12.80x |
| Distinct managed threads observed | 18 |
| `Environment.ProcessorCount` | 16 |
| Same flagged IDs in both runs | True |
| First five flagged IDs | 4, 8, 12, 29, 33 |

The matching flagged counts and IDs confirm that the parallel implementation produces the same results as the sequential implementation in this run.

The number of distinct managed thread IDs observed is not the same as the number of CPU cores. Threads may be scheduled and reused over the execution period.

### Task 10.4 B — Email Re-send

The application sends 30 test confirmation emails for order IDs 1001–1030 using `Parallel.ForEachAsync`, with `MaxDegreeOfParallelism` set to 4.

The concurrency limit is provided by the parallel-processing library. Counters are used only to measure the number of simultaneous sends, not to enforce the limit.

**Observed results from a successful run:**

| Metric | Result |
|---|---:|
| Test emails requested | 30 |
| Test emails saved | 30 |
| Highest concurrent sends | 4 |
| All emails saved exactly once | True |
| Total execution time | 15.73 seconds |

The email resend workload primarily involves asynchronous delays and repository I/O, so its performance is not expected to scale with CPU core count in the same way as the CPU-intensive fraud-scoring task.

The assignment's approximate eight-second estimate is a simplified estimate based on four simultaneous sends and approximately one second per send. The observed time can be longer because the implementation also performs repository operations and their simulated delays.

### Phase 4 Questions

#### Q1. Which tools are used in Tasks 10.1–10.4, and why is starting and awaiting tasks one by one insufficient?

- **Task 10.1:** `Task.WhenAll` and `Channel<T>` are used to coordinate concurrent order processing and collect results.
- **Task 10.2:** Asynchronous task coordination is used to load dashboard data, with exceptions handled for failed loads.
- **Task 10.3:** Concurrent carrier requests and cancellation are used to return the fastest successful quote and stop unnecessary work.
- **Task 10.4 A:** `Parallel.ForEachAsync` distributes CPU-intensive scoring work across available processing capacity.
- **Task 10.4 B:** `Parallel.ForEachAsync` with `MaxDegreeOfParallelism = 4` limits simultaneous email sends.

Starting and awaiting each operation one by one prevents independent operations from overlapping. The next operation cannot begin until the previous awaited operation completes.

#### Q2. In Task 10.2, if several loads fail, how can every exception be obtained? What does a plain `await` provide?

Keep a reference to the task returned by `Task.WhenAll`. If it fails, inspect its `Exception` property after completion to access the `AggregateException` and its inner exceptions.

A plain `await` normally throws one of the exceptions to the caller. It does not directly throw the entire collection of exceptions as an `AggregateException`.

#### Q3. What happens to losing shipping carriers if they are never cancelled?

The losing carrier operations may continue running after the winning quote has been selected. They can consume resources, perform unnecessary delays or I/O, and generate additional logs.

Cancellation allows operations that support the supplied token to stop cooperatively when their results are no longer required.

#### Q4. Why does Task 10.4 A benefit from more CPU cores, while Task 10.4 B does not depend on core count in the same way?

Task 10.4 A performs CPU-intensive calculations. Multiple cores can execute calculations simultaneously, potentially reducing the total execution time.

Task 10.4 B spends much of its time waiting for asynchronous operations and repository I/O. Its throughput is affected by the configured concurrency limit and the duration of those operations, rather than directly by the number of CPU cores.

#### Q5. What is the difference between parallel work in Task 10.4 A and concurrent asynchronous work in Task 10.1?

Task 10.4 A performs CPU-intensive calculations in parallel, allowing multiple workers to execute calculations simultaneously on available cores.

Task 10.1 overlaps asynchronous order-processing operations so that while one operation waits for I/O, other operations can continue making progress. Concurrency does not necessarily mean that all operations execute on different cores at the same instant.

#### Q6. What happens during a cancelled run using `--cancel-after`?

The `--cancel-after` option requests cancellation after the specified number of milliseconds. Cancellation is cooperative: operations that observe the cancellation token can stop, and the final outcome depends on which operations have completed and where cancellation is checked.

For order processing, cancellation handling must preserve the assignment's consistency requirements. A cancelled order should be saved with `Pending` status and `FailureReason = "Cancelled"` where required, and any reserved stock must be released and any applicable payment refunded. Completed external side effects cannot simply be assumed to have been undone.

A cancelled run should be demonstrated with the actual console output and the resulting JSON files. The exact orders, stock values, and payment outcomes depend on the cancellation timing and should be recorded from the run rather than assumed.
