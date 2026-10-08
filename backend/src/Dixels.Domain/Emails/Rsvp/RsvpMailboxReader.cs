using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace Dixels.Emails.Rsvp;

/// <summary>
/// Handles what's waiting in the rsvp@ mailbox, a batch at a time: each mail's answers are
/// saved in their own transaction, and only then is the mail marked handled — a crash in
/// between means reading the same answer again, which changes nothing. A mail that isn't an
/// answer, or whose guest or meeting is gone, is marked handled too, so junk never blocks the
/// queue. One instance lives as long as one mailbox connection (see <see cref="RsvpMailboxWorker"/>).
/// </summary>
public class RsvpMailboxReader : DomainService
{
    public const int BatchSize = 50;

    /// <summary>A mail that fails this often (not a closed meeting: a bug, bad data) is given up on.</summary>
    public const int MaxAttempts = 5;

    private readonly RsvpReplyApplier _applier;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly Dictionary<string, int> _failures = new();

    public RsvpMailboxReader(RsvpReplyApplier applier, IUnitOfWorkManager unitOfWorkManager)
    {
        _applier = applier;
        _unitOfWorkManager = unitOfWorkManager;
    }

    /// <summary>Handles everything waiting; returns how many answers were saved.</summary>
    public async Task<int> HandleWaitingAsync(IRsvpMailbox mailbox, CancellationToken cancellationToken)
    {
        var applied = 0;
        while (true)
        {
            var batch = await mailbox.GetUnhandledAsync(BatchSize, cancellationToken);
            var answers = batch.ToDictionary(m => m.Id, m => ItipReplyParser.Parse(m.Calendar, m.CalendarMethod));

            // A guest who changed their mind while the mail waited: only their last answer counts.
            // (Saving the first would make the later one look older than it — see RsvpReplyApplier.)
            var latest = answers.Values.SelectMany(a => a)
                .GroupBy(a => (a.Uid, a.Occurrence))
                .ToDictionary(g => g.Key, g => g.Max(a => a.AnsweredAtUtc));

            var handled = new List<string>();
            foreach (var message in batch)
            {
                try
                {
                    applied += await HandleAsync(answers[message.Id].Where(a => a.AnsweredAtUtc == latest[(a.Uid, a.Occurrence)]));
                    handled.Add(message.Id);
                    _failures.Remove(message.Id);
                }
                catch (Exception ex)
                {
                    var attempts = _failures[message.Id] = _failures.GetValueOrDefault(message.Id) + 1;
                    if (attempts >= MaxAttempts)
                    {
                        Logger.LogError(ex, "Gave up on rsvp mail {MessageId} after {Attempts} attempts.", message.Id, attempts);
                        handled.Add(message.Id);
                        _failures.Remove(message.Id);
                    }
                    else
                    {
                        // Most likely the database is away: leave it unread for the next round.
                        Logger.LogWarning(ex, "Could not handle rsvp mail {MessageId} (attempt {Attempts}).", message.Id, attempts);
                    }
                }
            }

            await mailbox.MarkHandledAsync(handled, cancellationToken);

            // A short batch was the last one; a failing mail stays unread, so don't spin on it.
            if (batch.Count < BatchSize || handled.Count < batch.Count)
            {
                return applied;
            }
        }
    }

    private async Task<int> HandleAsync(IEnumerable<ItipAnswer> answers)
    {
        var applied = 0;
        foreach (var answer in answers)
        {
            using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var outcome = await _applier.ApplyAsync(answer);
            await uow.CompleteAsync();

            if (outcome == RsvpReplyOutcome.Applied)
            {
                applied++;
            }
            else
            {
                Logger.LogInformation("An answer from a mail app was not applied: {Outcome}.", outcome);
            }
        }

        return applied;
    }
}
