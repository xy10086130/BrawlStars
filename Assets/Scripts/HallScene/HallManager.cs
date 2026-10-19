using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HallManager : MonoBehaviour
{
    public static HallManager Instance;

    public Transform previewRoot;        // 大厅组模型
    public Transform panelPreviewRoot;   // 面板预览组模型
    public int curIndex;
    public int pendingIndex;

    public Vector3 guestOffset = new Vector3(2.6f, 0f, 0f); // 对手模型旁侧偏移（编辑器校准）
    public Vector3 ownRoomShift = new Vector3(-50f, 0f, 0f); // 房态：自己模型向左让位（编辑器校准）
    private GameObject guest;            // 房间对手模型（ShowGuest 克隆出的副本）
    private int guestHeroId = -1;
    private bool ownShifted;             // 房态主模型让位是否生效
    private Vector3[] heroBasePos;       // 每个英雄的原站位（未让位），懒分配

    void EnsureBasePos()
    {
        if (heroBasePos == null || heroBasePos.Length != previewRoot.childCount)
        {
            heroBasePos = new Vector3[previewRoot.childCount];
            for (int i = 0; i < heroBasePos.Length; i++)
                heroBasePos[i] = previewRoot.GetChild(i).position;
        }
    }

    // 房态开关：自己主模型向左让位（真平移），退房还原。Show 换英雄时保持让位
    public void SetOwnRoomShift(bool on)
    {
        if (on == ownShifted) return;
        ownShifted = on;
        EnsureBasePos();
        previewRoot.GetChild(curIndex).position = heroBasePos[curIndex] + (on ? ownRoomShift : Vector3.zero);
    }

    public Vector3 HeroBasePosition(int heroId)
    {
        EnsureBasePos();
        return heroBasePos[heroId];
    }

    void Awake()
    {
        Instance = this;
    }
    void Start()
    {
        Show(0);
    }

    public void Show(int index)
    {
        index = Mathf.Clamp(index, 0, previewRoot.childCount - 1);
        for (int i = 0; i < previewRoot.childCount; i++)
            previewRoot.GetChild(i).gameObject.SetActive(i == index);
        curIndex = index;
        // 激活即归位（防房态让位过的旧英雄残留偏移）：房态让位 + ownRoomShift，单人态回原站位
        EnsureBasePos();
        previewRoot.GetChild(curIndex).position =
            heroBasePos[curIndex] + (ownShifted ? ownRoomShift : Vector3.zero);
    }

    public void ShowPanelPreview(int index)
    {
        index = Mathf.Clamp(index, 0, panelPreviewRoot.childCount - 1);
        for (int i = 0; i < panelPreviewRoot.childCount; i++)
            panelPreviewRoot.GetChild(i).gameObject.SetActive(i == index);
    }

    public void RotateHallCharacter(float deltaY)
    {
        previewRoot.GetChild(curIndex).Rotate(0f, deltaY, 0f);
    }

    public void RotatePanelCharacter(float deltaY)
    {
        panelPreviewRoot.GetChild(pendingIndex).Rotate(0f, deltaY, 0f);
    }

    // 房间对手模型：克隆 previewRoot 里第 heroId 个英雄 → 挂 previewRoot.parent
    // Show() 的 SetActive 循环会误伤）→ 世界坐标站到自己旁边 guestOffset 处
    public void ShowGuest(int heroId)
    {
        if (guest != null && guestHeroId == heroId) return; // 没换英雄不重建
        HideGuest();
        if (heroId < 0 || heroId >= previewRoot.childCount) return;

        Transform src = previewRoot.GetChild(heroId);
        // 克隆：源可能是隐藏的（自己正显示别的英雄）→ 必须显式激活；摆到未让位的原站位旁侧
        guest = Instantiate(src.gameObject);
        guest.SetActive(true);
        guest.transform.SetPositionAndRotation(HeroBasePosition(heroId) + guestOffset, src.rotation);
        guestHeroId = heroId;
    }

    public void HideGuest()
    {
        if (guest != null)
        {
            Destroy(guest);
            guest = null;
        }
        guestHeroId = -1;
    }
}
