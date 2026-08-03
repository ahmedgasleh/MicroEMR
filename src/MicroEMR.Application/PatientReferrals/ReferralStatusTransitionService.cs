namespace MicroEMR.Application.PatientReferrals;

public interface IReferralStatusTransitionService
{
    bool CanTransition(ReferralStatus currentStatus, ReferralStatus targetStatus);
    void EnsureCanTransition(ReferralStatus currentStatus, ReferralStatus targetStatus);
}

public sealed class ReferralStatusTransitionService : IReferralStatusTransitionService
{
    public bool CanTransition(ReferralStatus currentStatus, ReferralStatus targetStatus) =>
        (currentStatus, targetStatus) is
            (ReferralStatus.Draft, ReferralStatus.Sent) or
            (ReferralStatus.Sent, ReferralStatus.ResponseReceived) or
            (ReferralStatus.ResponseReceived, ReferralStatus.Closed);

    public void EnsureCanTransition(ReferralStatus currentStatus, ReferralStatus targetStatus)
    {
        if (!CanTransition(currentStatus, targetStatus))
            throw new PatientReferralTransitionException(currentStatus, targetStatus);
    }
}

public sealed class PatientReferralTransitionException : InvalidOperationException
{
    public PatientReferralTransitionException(ReferralStatus currentStatus, ReferralStatus targetStatus)
        : base($"A referral cannot transition from {currentStatus} to {targetStatus}.") { }

    public PatientReferralTransitionException(string message) : base(message) { }
}

public sealed class PatientReferralConcurrencyException()
    : Exception("The referral was changed by another user. Refresh and try again.");
