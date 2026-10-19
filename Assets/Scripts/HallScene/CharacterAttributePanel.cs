using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharacterAttributePanel : BasePanel
{
    public Button selectBtn;
    public Text nameText;        // 英雄名
    public Text descriptionText; // 简介
                                 // public Text hpValueText;     // attribute_Hp 行数值
                                 // public Text attackValueText; // attribute_attack 行数值


    public override void OnOpen()
    {
        base.OnOpen();
        int index = HallManager.Instance.pendingIndex;
        HallManager.Instance.ShowPanelPreview(index);  // 面板预览点选的英雄

        CharacterInfoData data = CharacterInfoConfig.Get(index);
        if (data != null)
        {
            nameText.text = data.name;
            descriptionText.text = data.description;
            // hpValueText.text = data.hp.ToString();
            //attackValueText.text = data.attack.ToString();
        }

        selectBtn.onClick.RemoveAllListeners();
        selectBtn.onClick.AddListener(OnClickSelect);
    }

    void OnClickSelect()
    {
        AudioManager.PlaySfx(AudioManager.SfxSelect);
        int heroId = HallManager.Instance.pendingIndex;
        HallManager.Instance.Show(heroId);  // 乐观更新自己模型（房间态/单人态都先换）

        // 房间态换英雄：host/member 统一发 39（服务端幂等 = 房内换英雄，回包刷 Members + 推对方）
        if (!string.IsNullOrEmpty(MainPanel.s_RoomCode))
        {
            ProtoManager.CSRoomJoin(MainPanel.s_RoomCode, heroId, msg =>
            {
                if (msg.Result == (int)LobbyRoomResult.Success)
                    MainPanel.RefreshFromMembers(msg.RoomCode, msg.Members);  // 刷新 IsHost/对手模型
            });
        }
        UIManager.Instance.CloseAll();
    }
}
