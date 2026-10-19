using MySQL;
using SimpleServer.Proto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleServer.Task
{
    public class TaskMonitor : Singleton<TaskMonitor>
    {

        //购买任务
        public void OnItemBought(int userID, int itemID, int count)
        {
            List<TaskConfigItem> matched = TaskConfig.AllTasks
                .Where(t => t.Type == "Buy" && t.ItemID == itemID)
                .ToList();

            foreach (TaskConfigItem c in matched)
                TaskManager.Instance.UpdateProgress(userID, c.ID, count);
        }


        ///击杀任务
        public void OnMonsterKilled(int userID, int monsterID)
        {
            List<TaskConfigItem> matched = TaskConfig.AllTasks
                .Where(t => t.Type == "Kill" && t.ItemID == monsterID)
                .ToList();

            foreach (TaskConfigItem c in matched)
                TaskManager.Instance.UpdateProgress(userID, c.ID, 1);   // 杀一只 +1
        }



    }
}
