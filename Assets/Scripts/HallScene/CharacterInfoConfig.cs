using UnityEngine;


[System.Serializable]
public class CharacterInfoData
{
    public int id;
    public string name;
    public string type;
    public string description;
    public int hp;
    public int attack;
    public string super;
    public AimInfo aim;             // 攻击形状参数：指示器画的 = 服务端判的（经 Tools/导出 1v1 英雄数据 同步到服务端）
    public int windupMs;            // 前摇：挥击开始 → 真正结算伤害。≈ 攻击动画的接触帧（动画 1.667s → 750）
    public int attackIntervalMs;    // 两次挥击最短间隔。≈ 攻击动画总时长——比动画短的话动画会一直被打断重播
                                    // 想更爽：把 Animator 里 Attack 状态的 Speed 调大（1.67 → 2 就是 0.83s），
                                    // 再把这里按同比例调小，两边一起改才对得上
    public int reloadMsPerShell;    // 弹药：回满 1 格要多少毫秒（3 格全空 → 满 = 3 × 这个数）
                                    // 必须 > attackIntervalMs，否则"连打时卡你的是装填不是冷却"不成立，弹药系统等于没做
}

// 攻击形状：shape="fan" 扇形（size=半角 度）；shape="rect" 矩形（size=半宽 米）
// 与 RangeIndicator 画出的网格同源同参，服务端 Heroes.txt 由同一份 json 导出
[System.Serializable]
public class AimInfo
{
    public string shape;
    public float range;             // 射程（米；地图提取后统一 格→米 换算）
    public float size;
}

[System.Serializable]
public class CharacterInfoRoot
{
    public CharacterInfoData[] CharacterInfo;   // 字段名与 json 顶层 key 一致
}

// 大厅英雄配置：懒加载一次，按数组下标取（与预览模型 / 选英雄按钮同序，勿乱排序）
public static class CharacterInfoConfig
{
    private static CharacterInfoData[] list;

    public static int Count { get { Ensure(); return list == null ? 0 : list.Length; } }

    // 丢掉进程内解析好的那份重新读。编辑器工具专用：json 改完静态缓存不会自己失效，
    // 不调它的话导出/查询拿到的还是本次会话第一次读到的旧值
    public static void Reload() { list = null; Ensure(); }

    public static CharacterInfoData Get(int index)
    {
        Ensure();
        if (list == null) return null;
        index = Mathf.Clamp(index, 0, list.Length - 1);
        return list[index];
    }

    static void Ensure()
    {
        if (list != null) return;
        TextAsset ta = Resources.Load<TextAsset>("Config/CharacterInfo");
        if (ta == null)
        {
            Debug.LogError("找不到 Config/CharacterInfo.json，请确认文件位于 Assets/Resources/Config/");
            return;
        }
        list = JsonUtility.FromJson<CharacterInfoRoot>(ta.text).CharacterInfo;
    }
}
