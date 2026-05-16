using UnityEngine;

/// <summary>
/// Wird automatisch von GridManager.SpawnTile auf jedes Tile-GameObject gesetzt.
/// Speichert den TileType, damit LoadPrebuiltTiles beim Start den Typ auslesen kann.
/// </summary>
public class TileMarker : MonoBehaviour
{
    public TileType tileType = TileType.Grass;
}
