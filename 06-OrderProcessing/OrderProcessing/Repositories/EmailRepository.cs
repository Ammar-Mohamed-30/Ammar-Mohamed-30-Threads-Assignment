
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Repositories;

public class EmailRepository
{
	private readonly JsonFileStore _store;

	public EmailRepository(JsonFileStore store)
	{
		_store = store;
	}

	public async Task<List<EmailMessage>> GetAllAsync(
		CancellationToken ct)
	{
		Log.Info("EmailRepository -> Opening connection...");
		await Task.Delay(200, ct);

		Log.Info("EmailRepository -> Executing emails query...");
		await Task.Delay(Random.Shared.Next(300, 601), ct);

		List<EmailMessage> emails =
			await _store.ReadAsync<EmailMessage>(
				DataFiles.Emails, ct);

		Log.Success(
			$"EmailRepository -> Retrieved {emails.Count} emails.");

		return emails;
	}

	public async Task SaveAsync(
		EmailMessage email,
		CancellationToken ct)
	{
		Log.Info(
			$"EmailRepository -> Opening connection for Order #{email.OrderId}...");
		await Task.Delay(200, ct);

		Log.Info(
			$"EmailRepository -> Beginning transaction for Order #{email.OrderId}...");
		await Task.Delay(100, ct);

		Log.Info(
			$"EmailRepository -> Inserting email for Order #{email.OrderId}...");
		await Task.Delay(400, ct);

		await _store.UpdateAsync<EmailMessage, bool>(
			DataFiles.Emails,
			emails =>
			{
				int existingIndex = emails.FindIndex(
					existingEmail => existingEmail.Id == email.Id);

				if (existingIndex >= 0)
				{
					emails[existingIndex] = email;
				}
				else
				{
					emails.Add(email);
				}

				return true;
			},
			ct);

		Log.Info(
			$"EmailRepository -> Committing email for Order #{email.OrderId}...");
		await Task.Delay(200, ct);

		Log.Success(
			$"EmailRepository -> Email for Order #{email.OrderId} saved.");
	}

	public async Task<List<EmailMessage>> GetRetryEmailsAsync(
		CancellationToken ct)
	{
		Log.Info("EmailRepository -> Opening connection for retry emails...");
		await Task.Delay(200, ct);

		Log.Info("EmailRepository -> Executing retry emails query...");
		await Task.Delay(Random.Shared.Next(300, 601), ct);

		List<EmailMessage> emails =
			await _store.ReadAsync<EmailMessage>(
				DataFiles.EmailRetry, ct);

		Log.Success(
			$"EmailRepository -> Retrieved {emails.Count} retry emails.");

		return emails;
	}

	public async Task SaveRetryAsync(
		EmailMessage email,
		CancellationToken ct)
	{
		Log.Info(
			$"EmailRepository -> Opening connection for retry Order #{email.OrderId}...");
		await Task.Delay(200, ct);

		Log.Info(
			$"EmailRepository -> Beginning transaction for retry Order #{email.OrderId}...");
		await Task.Delay(100, ct);

		Log.Info(
			$"EmailRepository -> Inserting retry email for Order #{email.OrderId}...");
		await Task.Delay(400, ct);

		await _store.UpdateAsync<EmailMessage, bool>(
			DataFiles.EmailRetry,
			emails =>
			{
				int existingIndex = emails.FindIndex(
					existingEmail => existingEmail.Id == email.Id);

				if (existingIndex >= 0)
				{
					emails[existingIndex] = email;
				}
				else
				{
					emails.Add(email);
				}

				return true;
			},
			ct);

		Log.Info(
			$"EmailRepository -> Committing retry email for Order #{email.OrderId}...");
		await Task.Delay(200, ct);

		Log.Success(
			$"EmailRepository -> Retry email for Order #{email.OrderId} saved.");
	}
}
	