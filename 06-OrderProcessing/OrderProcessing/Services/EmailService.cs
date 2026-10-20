using OrderProcessing.Repositories;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Services;

public class EmailService
{
	private readonly EmailRepository _emailRepository;

	public EmailService(EmailRepository emailRepository)
	{
		_emailRepository = emailRepository;
	}

	public async Task SendConfirmationAsync(
		Order order,
		Customer customer,
		CancellationToken ct)
	{
		const int maxAttempts = 4;

		EmailMessage email = new()
		{
			Id = Guid.NewGuid(),
			OrderId = order.Id,
			To = customer.Email,
			Subject = $"Order #{order.Id} Confirmation",
			Body = $"Your order #{order.Id} has been completed. Total: {order.TotalAmount:F2}",
			SentAt = DateTime.Now,
			Attempts = 0,
			LastError = null
		};

		for (int attempt = 1; attempt <= maxAttempts; attempt++)
		{
			ct.ThrowIfCancellationRequested();

			try
			{
				Log.Info($"EmailService -> Rendering email for Order #{order.Id}, attempt {attempt}...");
				await Task.Delay(200, ct);

				Log.Info($"EmailService -> Connecting to SMTP for Order #{order.Id}...");
				await Task.Delay(300, ct);

				Log.Info($"EmailService -> Sending email for Order #{order.Id}...");
				await Task.Delay(500, ct);

				if (order.Id == 17 || (order.Id == 3 && attempt <= 2))
				{
					throw new Exception("Simulated SMTP failure");
				}

				email.Attempts = attempt;
				email.SentAt = DateTime.Now;
				email.LastError = null;

				await _emailRepository.SaveAsync(email, ct);

				Log.Success($"EmailService -> Confirmation email sent for Order #{order.Id}.");
				return;
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				email.Attempts = attempt;
				email.LastError = ex.Message;

				Log.Error($"EmailService -> Attempt {attempt} failed for Order #{order.Id}: {ex.Message}");

				if (attempt < maxAttempts)
				{
					int delayMs = attempt switch
					{
						1 => 500,
						2 => 1000,
						_ => 2000
					};

					Log.Info($"EmailService -> Retrying Order #{order.Id} in {delayMs} ms...");
					await Task.Delay(delayMs, ct);
				}
			}
		}

		await _emailRepository.SaveRetryAsync(email, ct);

		Log.Error($"EmailService -> All {maxAttempts} attempts failed. Order #{order.Id} email queued for retry.");
	}
}