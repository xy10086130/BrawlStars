using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInfo
{
    public static PlayerInfo Instance = new PlayerInfo();

    public string NickName;
    public string UID;

    public void Init(string userID, string nickName)
    {
        UID = userID;
        NickName = nickName;
    }
}
