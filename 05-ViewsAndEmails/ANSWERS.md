# Assignment 05 — Answers

Student name: Ammar Mohamed  
Group / ID: 1

## 1. What failures did you observe before the fix?

The original program produced two concurrency problems:

- **Dictionary:** Multiple threads accessed the `Views` dictionary concurrently. The check using `ContainsKey`, followed by `Add` and incrementing the value, was not atomic. This caused an `ArgumentException` when multiple threads tried to add the same product key.
- **Queue:** Multiple threads called `Enqueue`, `Count`, and `Dequeue` on a regular `Queue<string>` without synchronization. This caused an exception during queue resizing and could also lead to incorrect job processing.

## 2. How did you fix the view counting and email job paths?

I added a dedicated lock for the `Views` dictionary and protected the complete check-and-increment operation inside `RecordView`.

I also added a dedicated lock for the `EmailJobs` queue. The queue's enqueue operation and the worker's check-and-dequeue operations now use the same lock, preventing concurrent modifications from interfering with one another.

The existing lock for `SentEmails` remains in place to protect concurrent additions.

The workers exit only when the queue is empty and `ProducersDone` is true.

## 3. Why does making a collection thread-safe not automatically make the business rule safe?

Thread-safe collections protect individual collection operations, but a business rule may involve multiple operations that must happen together.

For example, checking whether a product exists and then adding or incrementing its view count is a compound operation. Making individual dictionary operations thread-safe would not necessarily make that entire sequence atomic.

Similarly, checking whether a queue contains jobs and then removing one must be coordinated with other workers and producers. The synchronization must protect the complete operation required by the business rule.

## 4. Test results

Output after the fix:

```text
Views expected=50000, actual=50000
Email jobs placed=20, unique sent=20, total sends=20
```

The test confirms that all 50,000 views were recorded and all 20 email jobs were processed exactly once in this run.

## Links

- [Microsoft Learn — Thread-Safe Collections](https://learn.microsoft.com/en-us/dotnet/standard/collections/thread-safe/)
- [Microsoft Learn — lock statement](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock)
