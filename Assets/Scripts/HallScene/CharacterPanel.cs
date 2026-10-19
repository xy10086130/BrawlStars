using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharacterPanel : BasePanel
{
    public Button character1Btn;
    public Button character2Btn;
    public Button character3Btn;
    public Button character4Btn;
    public override void OnOpen()
    {
        base.OnOpen();
        character1Btn.onClick.RemoveAllListeners();
        character1Btn.onClick.AddListener(() => OnClickHero(0));
        character2Btn.onClick.RemoveAllListeners();
        character2Btn.onClick.AddListener(() => OnClickHero(1));
        character3Btn.onClick.RemoveAllListeners();
        character3Btn.onClick.AddListener(() => OnClickHero(2));
        character4Btn.onClick.RemoveAllListeners();
        character4Btn.onClick.AddListener(() => OnClickHero(3));
    }

    void OnClickHero(int index)
    {
        HallManager.Instance.pendingIndex = index;
        UIManager.Instance.PushPanel(UIPanelType.CharacterAttributePanel);
    }
}
