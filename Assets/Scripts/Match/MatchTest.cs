using UnityEngine;

// 匹配协议联调测试（第2步）：M 发46入队，C 发47取消
// 挂大厅场景任意激活物体；HeroId 取大厅当前选中英雄
public class MatchTest : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M)) StartMatch();
        if (Input.GetKeyDown(KeyCode.C)) CancelMatch();
    }

    void StartMatch()
    {
        int heroId = HallManager.Instance != null ? HallManager.Instance.curIndex : 0;
        ProtoManager.CSMatch(1, heroId, (msg) =>
        {
            Debug.Log($"[MatchTest] 46入队回包 Result={msg.Result}");
        });
    }

    void CancelMatch()
    {
        ProtoManager.CSMatchCancel((msg) =>
        {
            Debug.Log($"[MatchTest] 47取消回包 Result={msg.Result}");
        });
    }
}
