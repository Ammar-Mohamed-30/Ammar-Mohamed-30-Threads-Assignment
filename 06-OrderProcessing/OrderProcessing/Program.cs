
using OrderProcessing.Phase1;
using OrderProcessing.Phase2;
using OrderProcessing.Phase3;
using OrderProcessing.Phase4;

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

        int modeIndex = Array.FindIndex(
            args,
            arg => arg.Equals(
                "--cancel-after",
                StringComparison.OrdinalIgnoreCase));

        if (modeIndex >= 0)
        {
            if (modeIndex + 1 >= args.Length ||
                !int.TryParse(args[modeIndex + 1], out int milliseconds) ||
                milliseconds < 0)
            {
                Console.WriteLine(
                    "Usage: dotnet run -- phase4 --cancel-after <milliseconds>");
                return;
            }

            cts.CancelAfter(milliseconds);
        }

        string mode = args.Length > 0 &&
                      !args[0].Equals(
                          "--cancel-after",
                          StringComparison.OrdinalIgnoreCase)
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

                case "phase3":
                    await new Phase3Runner().RunAsync(cts.Token);
                    break;

                case "phase4":
                    await new Phase4Runner().RunAsync(cts.Token);
                    break;

                case "all":
                    await new Phase1Runner().RunAsync(cts.Token);
                    await new Phase2Runner().RunAsync(cts.Token);
                    await new Phase3Runner().RunAsync(cts.Token);
                    await new Phase4Runner().RunAsync(cts.Token);
                    break;

                case "menu":
                    Console.WriteLine(
                        "=== Order Processing System ===");
                    Console.WriteLine(
                        "1. Phase 1 - Sequential Validation");
                    Console.WriteLine(
                        "2. Phase 2 - Concurrent Processing");
                    Console.WriteLine(
                        "3. Phase 3 - Concurrent Order Processing");
                    Console.WriteLine(
                        "4. Phase 4 - Scale-Up");
                    Console.WriteLine("0. Exit");
                    Console.Write("Choose: ");

                    switch (Console.ReadLine())
                    {
                        case "1":
                            await new Phase1Runner()
                                .RunAsync(cts.Token);
                            break;

                        case "2":
                            await new Phase2Runner()
                                .RunAsync(cts.Token);
                            break;

                        case "3":
                            await new Phase3Runner()
                                .RunAsync(cts.Token);
                            break;

                        case "4":
                            await new Phase4Runner()
                                .RunAsync(cts.Token);
                            break;

                        case "0":
                            return;

                        default:
                            Console.WriteLine("Invalid choice.");
                            break;
                    }

                    break;

                default:
                    Console.WriteLine(
                        "Usage: phase1 | phase2 | phase3 | phase4 | all");
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Operation cancelled.");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Unexpected error: {ex.Message}");
        }
    }
}
