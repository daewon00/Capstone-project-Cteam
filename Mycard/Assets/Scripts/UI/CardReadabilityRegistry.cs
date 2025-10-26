using UnityEngine;

// Loads CardReadabilityProfile from Resources if present.
// If missing, silently returns null (no warnings) so callers can use defaults.
public static class CardReadabilityRegistry
{
    private static CardReadabilityProfile _profile;
    private static bool _triedLoad;

    public static CardReadabilityProfile Profile
    {
        get
        {
            if (_profile == null && !_triedLoad)
            {
                _triedLoad = true;
                _profile = Resources.Load<CardReadabilityProfile>("Cards/CardReadabilityProfile");
                // No logging if missing; keep console clean per project preference.
            }
            return _profile;
        }
    }

    public static void SetProfile(CardReadabilityProfile profile) => _profile = profile;
}

