namespace ProPresenterObsBridge.Options;

public static class ConfigValidator
{
    public static List<string> Validate(ObsOptions obs, List<MappingEntry> mappings)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(obs.Host))
            errors.Add("Obs.Host must not be empty.");

        if (obs.Port is < 1 or > 65535)
            errors.Add($"Obs.Port must be 1..65535, got {obs.Port}.");

        if (mappings.Count == 0)
        {
            errors.Add("Mappings must contain at least one entry.");
            return errors;
        }

        for (int i = 0; i < mappings.Count; i++)
        {
            var m = mappings[i];
            var prefix = $"Mappings[{i}]";

            var noteType = m.NoteType?.Trim();
            if (!string.IsNullOrEmpty(noteType)
                && !noteType.Equals("on", StringComparison.OrdinalIgnoreCase)
                && !noteType.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{prefix}.NoteType must be 'On' or 'Off', got '{noteType}'.");
            }

            if (m.Channel is < 1 or > 16)
                errors.Add($"{prefix}.Channel must be 1..16, got {m.Channel}.");

            if (m.Note is < 0 or > 127)
                errors.Add($"{prefix}.Note must be 0..127, got {m.Note}.");

            if (m.Velocity is not -1 and (< 0 or > 127))
                errors.Add($"{prefix}.Velocity must be -1 or 0..127, got {m.Velocity}.");

            if (string.IsNullOrWhiteSpace(m.Scene))
                errors.Add($"{prefix}.Scene must not be empty.");
        }

        return errors;
    }
}
