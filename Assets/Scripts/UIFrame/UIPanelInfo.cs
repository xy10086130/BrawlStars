using System;
using System.Collections;
using System.Collections.Generic;
using System.Security;
using UnityEngine;

[Serializable]
public class UIPanelInfo : ISerializationCallbackReceiver
{
    public string PanelTypeString;
    public string path;
    public UIPanelType panelType;

    public void OnAfterDeserialize()
    {
        panelType = (UIPanelType)Enum.Parse(typeof(UIPanelType), PanelTypeString);
    }

    public void OnBeforeSerialize()
    {
    }
}

[Serializable]
public class UIPanelTypeJson
{
    public List<UIPanelInfo> PanelData;
}