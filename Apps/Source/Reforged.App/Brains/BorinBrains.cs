using Reforged.App.Forge;

namespace Reforged.App.Brains;

/// <summary>
/// Compatibility adapter that routes the new provider-neutral brain contract to
/// the currently installed Forge brain. Stage 3 can replace this registration with
/// the direct local Qwen/GGUF brain while leaving Borin's UI unchanged.
/// </summary>
public sealed class BorinForgeBrainAdapter(IForgeBrain forgeBrain) : IBrain
{
    public async Task<BrainResponse> ThinkAsync(
        BrainContext context,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var response = await forgeBrain.RespondAsync(
            context.Request,
            context.Conversation.State,
            context.Conversation.RepositorySnapshot,
            token);

        var provider = string.IsNullOrWhiteSpace(context.Conversation.State.Configuration.ProviderName)
            ? "Forge brain"
            : context.Conversation.State.Configuration.ProviderName;

        return new BrainResponse(response, provider);
    }
}

/// <summary>
/// Safe fallback used when no model provider is registered. It never edits files,
/// starts tools, or pretends that a model request was completed.
/// </summary>
public sealed class NullBrain : IBrain
{
    public Task<BrainResponse> ThinkAsync(
        BrainContext context,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        token.ThrowIfCancellationRequested();

        var address = string.IsNullOrWhiteSpace(context.Conversation.State.Configuration.AddressUserAs)
            ? "lad"
            : context.Conversation.State.Configuration.AddressUserAs;

        return Task.FromResult(new BrainResponse(
            $"Aye, {address}, the local thinking turn could not be completed. Check the Local GGUF status for the exact fault and try the request again. I changed no files and started no tools.",
            "Safe local-brain fallback",
            UsedFallback: true));
    }
}
