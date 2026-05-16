/// <summary>
/// Interface für alle 3D-Objekte die per Mausklick interagierbar sind.
/// Wird von WorldClickHandler per Raycast gefunden.
/// </summary>
public interface IClickable
{
    void OnClick();
}
