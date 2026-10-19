using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 普通按钮的点击音。挂到按钮所在的那个物体上（跟 Button 同物体，EventSystem 才派发得到）。
//
// ⚠ 用 IPointerClickHandler 而不是 onClick.AddListener：
//   面板自己的 OnOpen 里普遍有 onClick.RemoveAllListeners()（CharacterPanel 等），
//   而且面板被 panelDic 缓存、OnOpen 会跑多次 → 挂 listener 要么被清掉、要么越挂越多。
//   接口实现跟 UnityEvent 两条线，互不干扰。
//
// 批量挂：AudioManager.BindClicks(root)，按 GetComponent 去重，可重复调用。
public class ButtonSfx : MonoBehaviour, IPointerClickHandler
{
    public string Sfx = AudioManager.SfxNormalBtn;

    public void OnPointerClick(PointerEventData e)
    {
        // 不可点的按钮（房态下的 modelSelectBtn 等）也会被 EventSystem 派发过来，得自己拦
        Button btn = GetComponent<Button>();
        if (btn != null && !btn.IsInteractable()) return;

        AudioManager.PlaySfx(Sfx);
    }
}
