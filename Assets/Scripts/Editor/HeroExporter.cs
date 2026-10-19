using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;


// 工具：英雄数值 → 服务端 heroes.txt（两端同源，服务端不硬编码数字）
// 用法：菜单 Tools/导出 1v1 英雄数据
// 第一列写【客户端英雄下标，0 起】(= 38/46/41 里传的 HeroId = CharacterInfo.json 的数组下标)，
// 不是 json 里的 id 字段（那个 1 起），口径错了服务端会取到别人的数值。
//
// ⚠ 本文件**不能**放回 Assets/Editor/：那里有个 Unity.AssetBundleBrowser.Editor.asmdef
// （references 空）把整个 Assets/Editor/ 圈成了一个编辑器程序集，看不见游戏的运行时脚本
// （CharacterInfoConfig 就属于这一层，放过去报 CS0103）。放这里 = Assembly-CSharp-Editor，能看见。
// 同理：以后往 Assets/Editor/ 放脚本，只用 UnityEditor/UnityEngine 的 API 才行。
public static class HeroExporter
{
    const string ServerTxt = "BrawlServer/Data/heroes.txt";   // 相对 Unity 项目根

    [MenuItem("Tools/导出 1v1 英雄数据")]
    static void Export()
    {
        // 这两行是"导出值不等于 json 里写的值"的根因，缺一不可：
        // Refresh = 外部编辑器改完 json 时先让 Unity 重新导入（Auto Refresh 关着它自己不会发现）；
        // Reload  = 再丢掉 CharacterInfoConfig 进程内缓存的那份 —— 它的 list 是静态的，本次会话只读一次
        AssetDatabase.Refresh();
        CharacterInfoConfig.Reload();

        int n = CharacterInfoConfig.Count;
        if (n == 0) { Debug.LogError("[英雄导出] CharacterInfo.json 没读到，中止"); return; }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("# 英雄数值表 —— 由 Unity 菜单「Tools/导出 1v1 英雄数据」从 Assets/Resources/Config/CharacterInfo.json 生成，勿手改");
        sb.AppendLine("# 第一列是【客户端英雄下标，0 起】，与 38/46/41 里传的 HeroId 同口径，不是 json 里的 id 字段（那个是 1 起）");
        sb.AppendLine("# id hp attack shape range size windupMs attackIntervalMs reloadMsPerShell");

        for (int i = 0; i < n; i++)
        {
            CharacterInfoData d = CharacterInfoConfig.Get(i);
            if (d == null) { Debug.LogError("[英雄导出] 第 " + i + " 个英雄读不到，中止"); return; }
            if (d.aim == null) { Debug.LogError("[英雄导出] " + d.name + " 没有 aim 节点（shape/range/size），中止"); return; }
            // json 里漏写 reloadMsPerShell 时 JsonUtility 给 0 → 服务端拿它当除数会炸/永不装填。
            // 兜底值必须非 0，跟 HeroTable 的兜底同口径（1500）
            if (d.reloadMsPerShell <= 0)
            {
                d.reloadMsPerShell = 1500;
                Debug.LogWarning("[英雄导出] " + d.name + " 的 reloadMsPerShell 没写或 ≤0，按 1500 导出");
            }
            string line = string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3} {4} {5} {6} {7} {8}",
                i, d.hp, d.attack, d.aim.shape, Num(d.aim.range), Num(d.aim.size),
                d.windupMs, d.attackIntervalMs, d.reloadMsPerShell);
            sb.AppendLine(line);
            Debug.Log("[英雄导出] " + line + "   (" + d.name + ")");
        }

        string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ServerTxt);
        string text = sb.ToString().TrimEnd('\n') + "\n";   // 末尾保留换行
        try
        {
            string old = File.Exists(path) ? File.ReadAllText(path) : null;
            File.WriteAllText(path, text);
            if (old == text) Debug.Log("[英雄导出] 完成：与旧文件完全一致（数值没改过）");
            else Debug.LogWarning("[英雄导出] 完成，但内容与旧文件不同 → 服务端需重新编译/重跑（heroes.txt 是 CopyToOutputDirectory）");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[英雄导出] 写文件失败：" + e);
        }
    }

    // 3 → "3"，2.7 → "2.7"（不用 "0.0" 之类；小数点固定 '.'，服务端按 InvariantCulture 解析）
    static string Num(float f) { return f.ToString("0.###", CultureInfo.InvariantCulture); }
}
