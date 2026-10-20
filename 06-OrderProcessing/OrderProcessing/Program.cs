
using OrderProcessing.Phase1;
using OrderProcessing.Phase2;

namespace OrderProcessing;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\nCancellation requested...");
        };

        if (args.Length > 0 &&
            args[0].Equals("--cancel-after", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 2 ||
                !int.TryParse(args[1], out int milliseconds) ||
                milliseconds < 0)
            {
                Console.WriteLine("Usage: --cancel-after <milliseconds>");
                return;
            }

            cts.CancelAfter(milliseconds);
        }

        string mode = args.Length > 0 &&
                      !args[0].Equals("--cancel-after", StringComparison.OrdinalIgnoreCase)
            ? args[0].ToLowerInvariant()
            : "menu";

        try
        {
            switch (mode)
            {
                case "phase1":
                    await new Phase1Runner().RunAsync(cts.Token);
                    break;

                case "phase2":
                    await new Phase2Runner().RunAsync(cts.Token);
                    break;

                case "menu":
                    Console.WriteLine("=== Order Processing System ===");
                    Console.WriteLine("1. Phase 1 - Sequential Validation");
                    Console.WriteLine("2. Phase 2 - Concurrent Processing");
                    Console.WriteLine("0. Exit");
                    Console.Write("Choose: ");

                    switch (Console.ReadLine())
                    {
                        case "1":
                            await new Phase1Runner().RunAsync(cts.Token);
                            break;

                        case "2":
                            await new Phase2Runner().RunAsync(cts.Token);
                            break;

                        case "0":
                            return;

                        default:
                            Console.WriteLine("Invalid choice.");
                            break;
                    }
                    break;

                case "phase3":
                case "phase4":
                case "all":
                    Console.WriteLine($"'{mode}' has not been implemented yet.");
                    break;

                default:
                    Console.WriteLine("Usage: phase1 | phase2 | phase3 | phase4 | all");
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Operation cancelled.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }
}
