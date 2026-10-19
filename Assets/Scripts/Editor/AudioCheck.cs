using UnityEditor;
using UnityEngine;

// Tools/检查音频资源：把 AudioManager.AllPaths 逐个 Resources.Load 一遍。
// 这条路的失败模式是"文件少拷一个 / 名字拼错 → 静默没声"，跟"代码没接上"长得一模一样，
// 靠耳朵一条条排最费时间。
//
// ⚠ 放 Assets/Scripts/Editor/ 而不是 Assets/Editor/：后者有个 references 为空的 asmdef
//   把目录圈成独立程序集，看不见游戏的运行时脚本（会报 CS0103）。这里要引用 AudioManager。
public static class AudioCheck
{
    [MenuItem("Tools/检查音频资源")]
    static void Check()
    {
        int miss = 0;
        foreach (string name in AudioManager.AllPaths)
        {
            AudioClip clip = Resources.Load<AudioClip>("Audio/" + name);
            if (clip == null)
            {
                miss++;
                Debug.LogError("[音频] 缺 " + name + "（应为 Assets/Resources/Audio/" + name + ".ogg）");
            }
            else
            {
                Debug.Log("[音频] OK  " + name + "  " + clip.length.ToString("0.00") + "s");
            }
        }

        if (miss == 0) Debug.Log("[音频] " + AudioManager.AllPaths.Length + " 个全部找到");
    }
}
