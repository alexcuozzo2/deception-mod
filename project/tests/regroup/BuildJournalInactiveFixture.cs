namespace Deception.PolyModAdapter
{
    // Production Regroup UI is compiled by this focused fixture; only the unrelated journal stays inactive.
    internal static class NativeBuildRecordJournalDraft
    {
        internal static void BeforeMutation(object value)
        {if(value is null)throw new System.ArgumentNullException(nameof(value));}
    }
}
