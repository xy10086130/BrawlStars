using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

//进入即自动发 46 入队；收 48 成功 → 过渡场景加载战斗，48{1}取消 → 回大厅
public class MatchPanel : MonoBehaviour
{
    public static int HeroId;   // 大厅进入前写入
    public static string OpponentNickName;  // 48 携带，战斗开场画面用（本场景销毁前必须存下）
    public static int OpponentHeroId;   // 48 携带，预留给战斗表现层
    public Button cancelBtn;    // 取消按钮
    public Text matchedCountText;   // 已匹配人数（服务端 40 推送）
    public Text matchCountTotal;    // 分母那格（"/2"）；留空 = 取 matchedCountText 的父物体

    void Start()
    {
        // 分母跟着模式走：单人队列默认是 4 人模式 → /4；1v1（组队房不走这个场景）→ /2
        int need = MainPanel.s_Mode == BattleRoster.ModeSolo4 ? 4 : 2;
        if (matchCountTotal == null && matchedCountText != null)
        {
            Transform p = matchedCountText.transform.parent;   // 场景里那格静态 "/2" 挂在父物体上
            if (p != null) matchCountTotal = p.GetComponent<Text>();
        }
        if (matchCountTotal != null) matchCountTotal.text = "/" + need;

        cancelBtn.onClick.AddListener(CancelMatch);
        AudioManager.BindClicks(transform);
        // 48 监听已在 MainPanel 常驻（跨场景静态委托，组队 49 开战收 48 靠它，勿在本场景再注册）
        ProtoManager.AddMatchCountListener((msg) =>
        {
            if (matchedCountText != null)
            {
                matchedCountText.text = msg.Count.ToString();
            }
        });
        // 模式跟着大厅选中的图走（MainPanel.s_Mode；组队房那条线不经过这里，服务端恒 1v1）
        ProtoManager.CSMatch(MainPanel.s_Mode, HeroId, msg =>
        {
            if (msg.Result != 0)
            {
                FlyText.Show("进入匹配失败：" + msg.Result);
                SceneManager.LoadScene("HallScene");
            }
        });
    }

    void CancelMatch()
    {
        ProtoManager.CSMatchCancel(null);
        SceneManager.LoadScene("HallScene");
    }
}
