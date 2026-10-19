using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum UIPanelType
{
    MainPanel = 0,
    CharacterPanel = 1,
    CharacterAttributePanel = 2,
    FriendPanel = 3,
    InvitePanel = 4,
    InputTeamIDPanel = 5,
    InvitedPanel = 6,
    SettingPanel = 7,

    // ⚠ 只能往后加。插在中间 = 把后面所有面板的存值全挪一位
    UpdatePanel = 8,             // 登录场景·热更页
    LoginAndRegisterPanel = 9,   // 登录场景·登录注册
}