using System.Collections.Immutable;

namespace San14.Core;

/// <summary>A detached edit of one running-world item. ImageIndex is retained
/// from the current definition; this page does not provide an image picker.</summary>
public sealed record CurrentWorldItemEditDraft(int ItemId, ItemEditorValues Values,
    int? HolderOfficerId, bool TransferProhibited);

public sealed record EditCurrentWorldItemOrder(int ForceId, CurrentWorldItemEditDraft Draft,
    bool ConfirmedRemoval = false) : CurrentWorldEditOrder(ForceId);

/// <summary>The running-world batch page changes custody and the shared
/// prohibition only. Selection order is the original per-item operation order.</summary>
public sealed record EditCurrentWorldItemsOrder(int ForceId, ImmutableArray<int> ItemIds,
    int? HolderOfficerId, bool TransferProhibited, bool ConfirmedRemoval = false)
    : CurrentWorldEditOrder(ForceId);
