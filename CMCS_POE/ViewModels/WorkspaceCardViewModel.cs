namespace CMCS_POE.ViewModels;

public sealed record WorkspaceCardViewModel(
    string Role, string Description, string Icon, string Theme, IReadOnlyList<string> Highlights);
