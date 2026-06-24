using System;
using UnityEngine;

[Serializable]
public class DialogueLine
{
    [Tooltip("Name des Sprechers, z.B. 'Bauer Karl'")]
    public string speakerName;

    [TextArea(2, 6)]
    public string text;

    [Tooltip("Optional: Portrait des Sprechers")]
    public Sprite portrait;
}
