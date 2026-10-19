using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class PreviewRotator : MonoBehaviour, IDragHandler
{
    public float speed = 3f;
    public void OnDrag(PointerEventData e)
    {
        HallManager.Instance.RotateHallCharacter(-e.delta.x * speed);
    }


}
