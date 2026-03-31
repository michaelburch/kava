namespace Kava.Core.Models;

/// <summary>
/// The type of calendar/contact provider backing an account.
/// </summary>
public enum ProviderType
{
    CalDav,
    IcsSubscription,
    // Future: MicrosoftGraph, Google, ICloud
}
