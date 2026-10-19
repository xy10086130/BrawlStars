using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class PreviewRotate : MonoBehaviour, IDragHandler
{
    public float speed = 3f;
    public void OnDrag(PointerEventData e)
    {
        HallManager.Instance.RotatePanelCharacter(-e.delta.x * speed);
    }


}
