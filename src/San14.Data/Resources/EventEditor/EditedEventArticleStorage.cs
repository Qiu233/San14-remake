using San14.Core;

namespace San14.Data;

/// <summary>Original packed article storage, independent of this project's
/// JSON archive length and of the smaller 122000-byte UI usage scale.</summary>
public static class EditedEventArticleStorage
{
    public const int CapacityBytes = 128000;
    public const int UsageScaleBytes = 122000;

    public static int UsedStorageBytes(EditedEventLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        return checked(library.Messages.Sum(message => StorageBytes(message.Text)));
    }

    public static bool CanStoreMessage(EditedEventLibrary library, EditedEventMessage value)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(value);
        value.Validate();
        int used = checked(library.Messages.Where(message => message.Slot != value.Slot)
            .Sum(message => StorageBytes(message.Text)) + StorageBytes(value.Text));
        return used <= CapacityBytes;
    }

    private static int StorageBytes(string text) => text.Length == 0 ? 0 : checked(2 * (text.Length + 1));
}
