using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CMCS_Part2.Hubs;

[Authorize]
public sealed class ClaimHub : Hub
{
    // Claim changes are sent by the server to currently authorized user IDs.
    // Clients cannot join groups or publish messages to other users.
}

//OpenAI.2025.Chat-GPT(Version 5).[Large language model]Available at: https://chatgpt.com/c/68b46a0b-99f8-8326-98b4-5245e5c93d35[Accessed: 20 November 2025].
