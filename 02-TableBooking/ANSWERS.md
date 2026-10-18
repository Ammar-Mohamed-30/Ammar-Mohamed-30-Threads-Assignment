# Assignment 02 — Answers

Student name: Ammar Mohamed  
Group / ID: 1

## Original Output

```text
Customer-29 booked a table. Left=-4
Customer-05 booked a table. Left=-4
Customer-04 booked a table. Left=-4
Customer-28 booked a table. Left=-4
Customer-25 booked a table. Left=-4
Customer-08 booked a table. Left=-4
Customer-20 booked a table. Left=-4
Customer-30 booked a table. Left=-4
Customer-18 booked a table. Left=-4
Customer-31 booked a table. Left=-4
Customer-03 booked a table. Left=-4
Customer-14 booked a table. Left=-4
Customer-12 booked a table. Left=-4
Customer-21 booked a table. Left=-4
Customer-37 booked a table. Left=-4
Customer-27 booked a table. Left=-4
Customer-13 booked a table. Left=-4
Customer-11 booked a table. Left=-4
Customer-06 booked a table. Left=-4
Customer-22 booked a table. Left=-4
Customer-10 booked a table. Left=-4
Customer-15 booked a table. Left=-4
Customer-09 booked a table. Left=-4
Customer-32 booked a table. Left=-4
Customer-19 booked a table. Left=-4
Customer-34 booked a table. Left=-4
Customer-39 booked a table. Left=-4
Customer-02 booked a table. Left=-4
Customer-38 booked a table. Left=-4
Customer-17 booked a table. Left=-4
Customer-26 booked a table. Left=-4
Customer-24 booked a table. Left=-4
Customer-07 booked a table. Left=-4
Customer-23 booked a table. Left=-4
Customer-35 booked a table. Left=-4
Customer-40 booked a table. Left=-4
Customer-16 booked a table. Left=-4
Customer-33 booked a table. Left=-4
Customer-01 booked a table. Left=-4
Customer-36 booked a table. Left=-4

Booked=36, TablesLeft=-4
```

## What Is Wrong

- Multiple threads access and update the shared table count concurrently without proper synchronization.
- Two or more threads can read the same available-table count and decrement it at the same time, causing a race condition.
- The application allows more bookings than the available capacity, resulting in a negative table count.

## How You Fixed It

- Added a private lock object to protect the shared booking state.
- Used `lock` around the availability check, simulated booking delay, counter updates, and console output.
- This ensures only one thread at a time can execute the critical section, preventing multiple customers from booking the same last table.
- Tested the updated application. The final output was:

```text
Booked=5, TablesLeft=0
```

The test confirms that the application booked exactly five tables and did not allow the remaining table count to become negative._
