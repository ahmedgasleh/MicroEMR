namespace MicroEMR.Api.ClinicalUsers;

public static class ClinicalUserActorContext
{
    private static readonly object Key = new();

    public static void Set(HttpContext context, long userId) => context.Items[Key] = userId;

    public static long GetRequired(HttpContext context) =>
        context.Items.TryGetValue(Key, out var value) && value is long userId
            ? userId
            : throw new InvalidOperationException("The clinical user actor was not resolved for this mutation.");
}
