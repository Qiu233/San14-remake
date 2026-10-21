/// <summary>Raw Canvas drawing that belongs to one ordered source segment,
/// independently of the layers needed to traverse its structural children.</summary>
internal interface IUiSourceDrawing
{
    void SetDrawingVisibilityLayer(uint layer);
}
