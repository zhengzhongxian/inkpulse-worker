using System.Collections.Generic;

namespace InkPulse.Worker.Features.Auth.Messages
{
    public record SendNumberChallengeEmailMessage(
        string Email,
        string Subject,
        int ChallengeNumber,
        List<int> Options,
        string SessionId
    );
}
