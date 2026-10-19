using System.Collections.Generic;

// 一局的出战名单 + 模式（48 带过来，跨场景活到本局结束）。
// 为什么单开一个：以前跨场景扛英雄的是 MatchPanel.HeroId / OpponentHeroId / OpponentNickName 三个【单数】字段，
//   4 人模式下没有"那个对手" → 得有个能装 N 个人的地方。
// 谁填：MainPanel.OnMatchSuccess（48 的常驻监听在那）。谁读：BattleEntry（实例化）+ BattleSync（按 UID 认人）+ 结算面板。
// ⚠ 别提前清空：45 到达时结算面板还要按名单认人（清在下一局 48 覆盖时自然发生）
public static class BattleRoster
{
    public class Member
    {
        public string UID;
        public string NickName;
        public int HeroId;
        public float X, Z;   // 服务端抽好的出生点（48 直接带过来，客户端不再自己摆）
    }

    public const int ModeDuel1v1 = 1;   // 组队约战房恒用这个
    public const int ModeSolo4 = 2;     // 单人匹配：4 人混战
    public const int ModeNone = 0;      // 选到的模式还没做（点开始不给排队）

    public static readonly List<Member> Members = new List<Member>();
    public static int Mode = ModeDuel1v1;

    // members 传 null 也不炸（48 老字段兜底 / 异常包）
    public static void Set(int mode, List<BattleMember> members)
    {
        Mode = mode;
        Members.Clear();
        if (members == null) return;
        foreach (BattleMember m in members)
            Members.Add(new Member { UID = m.UID, NickName = m.NickName, HeroId = m.HeroId, X = m.X, Z = m.Z });
    }

    public static Member Find(string uid)
    {
        foreach (Member m in Members) if (m.UID == uid) return m;
        return null;
    }
}
