# Assignment 01 — Answers

Student name: Ammar Mohamed  
Group / Group / ID: [1]

## Problems with the current design

- The application uses `Thread.Sleep`, which blocks the current thread during artificial waiting periods.
- Database operations and file reads and writes are synchronous, so the thread cannot perform other work while waiting for these operations to complete.
- The export process handles orders sequentially, causing waiting times to accumulate as the number of orders increases.
- The design reduces scalability and wastes thread resources during I/O-bound operations.

## What you changed

- Converted the database methods to asynchronous methods returning `Task<List<Order>>` and `Task<Order?>`.
- Replaced `Thread.Sleep` with `await Task.Delay` to simulate non-blocking waiting.
- Changed customer profile reading to use `File.ReadAllTextAsync`.
- Changed invoice writing to use `File.WriteAllTextAsync`.
- Updated the export pipeline to use `async` and `await` while preserving the business flow: load order, read customer profile, then write invoice.
- Updated `Program.cs` to await the asynchronous export operation.
- Kept the order-processing loop sequential, ensuring each order completes its required steps before moving to the next order.