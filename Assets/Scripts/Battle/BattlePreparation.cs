using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class BattlePreparation : MonoBehaviour
{
    // 一个英雄格：整块要滑进来的那个（HeroList/character1..4）+ 昵称 + 英雄头像
    [System.Serializable]
    public class HeroSlot
    {
        public RectTransform root;
        public Text nameText;
        public Image image;          // characterImage
    }

    public Image mask;          // 全屏黑场（倒计时结束淡出）

    public HeroSlot[] heroes;   // 场景里的 character1..4；留空 = 按名字自己找

    public Sprite[] heroIcons;  // 下标 = heroId（bibi/edgar/mari_geo/spike，跟 BattleEntry.PrefabNames 同序）。
                                // 没填 / 越界 → 不碰，留着场景里那张占位图

    public Transform countDown;         // 倒计时容器（含背景+数字）
    public Text countDownTime;
    public Text tips;

    public Text VS;

    public Text startTips;

    public Image startBattleBg;         // 开战横幅背景
    public Image battleTextImage;       // 开战横幅文字（

    // 节奏参数
    public float slideDur = 0.6f;       // 名单滑入时长
    public float vsDur = 0.3f;          // VS 弹出时长
    public float digitHold = 1f;        // 每个倒计时数字停留时长
    public float maskFadeDur = 0.3f;    // mask 淡出时长
    public float startTipsHold = 1f;    // startTips 停留时长
    public float bannerHold = 1f;       // 开战横幅停留时长
    public float bannerSpinSpeed = 90f; // startBattleBg 旋转速度（度/秒，慢慢转）
    public int countFrom = 3;           // 从几开始倒数

    const int SlotCount = 4;
    const int MySlot = 2;               // character3 是自己（跟老 friendList 一样站右边第一格）

    readonly bool[] m_Filled = new bool[SlotCount];   // 这一局有人的格子（滑入时才亮）

    // 名单 → 4 个格子：左边 character1/2 是对手，character3 是我，character4 是第三个对手。
    // 1v1 名单只有俩人 → 第 2、第 4 格是 null → 正好"左右各隐藏一个"
    BattleRoster.Member[] SlotsOrder()
    {
        string myUid = PlayerInfo.Instance.UID;
        List<BattleRoster.Member> others = new List<BattleRoster.Member>();
        foreach (BattleRoster.Member m in BattleRoster.Members)
            if (m.UID != myUid) others.Add(m);

        BattleRoster.Member[] order = new BattleRoster.Member[SlotCount];
        int k = 0;
        for (int i = 0; i < SlotCount; i++)
            if (i != MySlot) order[i] = k < others.Count ? others[k++] : null;
        order[MySlot] = Me();
        return order;
    }

    // 自己那一格：昵称来自 PlayerInfo，英雄来自大厅写下的 MatchPanel.HeroId（跟结算面板出英雄同一个来源）
    static BattleRoster.Member Me()
    {
        return new BattleRoster.Member
        {
            UID = PlayerInfo.Instance.UID,
            NickName = string.IsNullOrEmpty(PlayerInfo.Instance.NickName) ? "我" : PlayerInfo.Instance.NickName,
            HeroId = MatchPanel.HeroId,
        };
    }

    static string NickOf(BattleRoster.Member m)
    {
        return string.IsNullOrEmpty(m.NickName) ? "对手" : m.NickName;
    }

    // 头像表没填 / heroId 越界 → null（调用处不碰图，留着场景里那张）
    Sprite IconOf(int heroId)
    {
        if (heroIcons == null || heroId < 0 || heroId >= heroIcons.Length) return null;
        return heroIcons[heroId];
    }

    // 按名字找场景里的 character1..4（含未激活的），省得在 Inspector 里拖 4 次。
    // 找不到只报错不停流程：剩下三个照常演，坏的那格空着
    void BindSlots()
    {
        if (heroes == null || heroes.Length < SlotCount) heroes = new HeroSlot[SlotCount];
        for (int i = 0; i < SlotCount; i++)
        {
            if (heroes[i] == null) heroes[i] = new HeroSlot();
            if (heroes[i].root == null)
            {
                Transform t = FindDeep(transform, "character" + (i + 1));
                if (t == null)
                {
                    Debug.LogError("[BattlePreparation] 场景里没有 character" + (i + 1) + "（StartCanvas 下）");
                    continue;
                }
                heroes[i].root = (RectTransform)t;
            }
            if (heroes[i].nameText == null)
            {
                Transform t = FindDeep(heroes[i].root, "name");   // 昵称那格跟老 characterBg 一样叫 name
                if (t != null) heroes[i].nameText = t.GetComponent<Text>();
            }
            if (heroes[i].image == null)
            {
                Transform t = FindDeep(heroes[i].root, "characterImage");
                if (t != null) heroes[i].image = t.GetComponent<Image>();
            }
        }
    }

    static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    // 写名字/头像，但一个都不亮 —— 亮着站在终点位置上，滑入前会先闪一下（滑入那步才 SetActive）
    void FillSlots()
    {
        BattleRoster.Member[] order = SlotsOrder();
        for (int i = 0; i < SlotCount; i++)
        {
            if (heroes[i] == null || heroes[i].root == null) continue;
            heroes[i].root.gameObject.SetActive(false);
            BattleRoster.Member m = order[i];
            m_Filled[i] = m != null;   // 空槽 = 这一局没这个人
            if (m == null) continue;

            if (heroes[i].nameText != null) heroes[i].nameText.text = NickOf(m);
            Sprite icon = IconOf(m.HeroId);
            if (icon != null && heroes[i].image != null) heroes[i].image.sprite = icon;
        }
    }

    // 滑入起点要真在屏幕外：半个画布宽 + 半个格子宽（跟着分辨率走，1920 和 720 下都不会提前露头）
    float OffScreen(RectTransform slot)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        float half = canvas != null ? canvas.GetComponent<RectTransform>().rect.width * 0.5f : 960f;
        return half + slot.rect.width;
    }

    IEnumerator Start()
    {
        BindSlots();
        FillSlots();

        // 显式收拢初始状态，防场景残留闪现
        VS.gameObject.SetActive(false);
        tips.gameObject.SetActive(false);
        countDown.gameObject.SetActive(false);
        startTips.gameObject.SetActive(false);
        if (startBattleBg != null) startBattleBg.gameObject.SetActive(false);
        if (battleTextImage != null) battleTextImage.gameObject.SetActive(false);

        // 0. startTips 最先单独出现，停留后消失
        startTips.gameObject.SetActive(true);
        yield return new WaitForSeconds(startTipsHold);
        startTips.gameObject.SetActive(false);

        // 1. 四个格子滑入：character1/2 从左边、3/4 从右边（人不够的格子不参与）。
        //    编辑器位置 = 停下来的位置，所以先在屏幕外就位、再亮、再滑 —— 亮着站终点上会先闪一下
        Sequence seq = DOTween.Sequence();
        for (int i = 0; i < SlotCount; i++)
        {
            if (!m_Filled[i]) continue;
            RectTransform rt = heroes[i].root;
            Vector2 rest = rt.anchoredPosition;
            rt.anchoredPosition = rest + new Vector2(i < MySlot ? -OffScreen(rt) : OffScreen(rt), 0f);
            rt.gameObject.SetActive(true);
            seq.Join(rt.DOAnchorPos(rest, slideDur).SetEase(Ease.OutCubic));
        }
        yield return new WaitForSeconds(slideDur);

        // 2. VS 弹出（0→1 带一点过冲）
        VS.gameObject.SetActive(true);
        VS.transform.localScale = Vector3.zero;
        VS.transform.DOScale(1f, vsDur).SetEase(Ease.OutBack);
        yield return new WaitForSeconds(vsDur);

        // 3. tips + 倒计时出现，3→2→1
        tips.gameObject.SetActive(true);
        countDown.gameObject.SetActive(true);
        for (int i = countFrom; i >= 1; i--)
        {
            countDownTime.text = i.ToString();
            yield return new WaitForSeconds(digitHold);
        }
        AudioManager.PlaySfx(AudioManager.SfxGo);   // 倒计时一结束就喊（不是等横幅那步）
        countDown.gameObject.SetActive(false);

        // 4. 倒计时完 → 开场 UI 整体退场 + mask 揭幕（黑场未散时隐藏，无感）
        for (int i = 0; i < SlotCount; i++)
            if (heroes[i] != null && heroes[i].root != null) heroes[i].root.gameObject.SetActive(false);
        VS.gameObject.SetActive(false);
        tips.gameObject.SetActive(false);
        if (mask != null && mask.gameObject.activeInHierarchy)
        {
            mask.DOFade(0f, maskFadeDur);
            yield return new WaitForSeconds(maskFadeDur);
            mask.gameObject.SetActive(false);
        }

        // 黑场刚揭、自己的英雄第一次露脸 → 喊一句开场语音。
        // 不跟 SfxGo 挤在同一帧（那是"GO!"的爆点，两条音叠一起互相糊）；只放自己的，对方的听不到
        AudioManager.PlayHeroSfx(Me().HeroId, AudioManager.HeroSfx.StartVo);

        // 5. 开战横幅：文字与背景同现 bannerHold 秒，期间 startBattleBg 匀速慢转 → 一起消失，战斗正式开始
        if (startBattleBg != null && battleTextImage != null)
        {
            startBattleBg.gameObject.SetActive(true);
            battleTextImage.gameObject.SetActive(true);
            float t = 0f;
            while (t < bannerHold)
            {
                t += Time.deltaTime;
                startBattleBg.transform.Rotate(0f, 0f, bannerSpinSpeed * Time.deltaTime);
                yield return null;
            }
            startBattleBg.gameObject.SetActive(false);
            battleTextImage.gameObject.SetActive(false);
        }
    }
}
